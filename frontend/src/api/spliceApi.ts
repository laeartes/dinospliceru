import { apiUrl } from './apiClient'

export type SpliceMode = 'ByLength' | 'BySize'

export type SpliceJobStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed'

export interface SpliceRequest {
  mode: SpliceMode
  value: number
}

export interface SpliceJobResponse {
  jobId: string
  status: string
}

export interface SpliceSegment {
  name: string
  duration: number
  size: number
}

export interface SpliceJobDetailsResponse {
  id: string
  status: SpliceJobStatus
  segments: SpliceSegment[]
  error?: string
}

function parseErrorMessage(errBody: unknown, defaultMessage: string): string {
  if (!errBody || typeof errBody !== 'object') {
    return defaultMessage
  }

  const obj = errBody as {
    detail?: string
    title?: string
    error?: string
    errors?: Record<string, string[] | string>
  }

  if (obj.errors && typeof obj.errors === 'object') {
    const values = Object.values(obj.errors).flat()
    if (values.length > 0 && typeof values[0] === 'string') {
      return values[0]
    }
  }

  if (obj.detail && typeof obj.detail === 'string') {
    return obj.detail
  }

  if (obj.title && typeof obj.title === 'string') {
    return obj.title
  }

  if (obj.error && typeof obj.error === 'string') {
    return obj.error
  }

  return defaultMessage
}

function isSpliceJobResponse(value: unknown): value is { jobId?: string; id?: string; status: string } {
  if (!value || typeof value !== 'object') return false
  const obj = value as { jobId?: unknown; id?: unknown; status?: unknown }
  const hasId = typeof obj.jobId === 'string' || typeof obj.id === 'string'
  return hasId && typeof obj.status === 'string'
}

function normalizeSpliceJobResponse(raw: { jobId?: string; id?: string; status: string }): SpliceJobResponse {
  return {
    jobId: raw.jobId ?? raw.id ?? '',
    status: raw.status,
  }
}

interface RawSpliceJobDetails {
  id?: string
  jobId?: string
  status?: string
  error?: string
  message?: string
  detail?: string
  segments?: Array<{
    name?: string
    fileName?: string
    duration?: number
    durationSeconds?: number
    size?: number
    sizeBytes?: number
  }>
  outputs?: Array<{
    name?: string
    fileName?: string
    duration?: number
    durationSeconds?: number
    size?: number
    sizeBytes?: number
  }>
}

function normalizeSpliceJobDetails(raw: RawSpliceJobDetails, fallbackId: string): SpliceJobDetailsResponse {
  const id = raw.id ?? raw.jobId ?? fallbackId
  const rawStatus = (raw.status ?? 'Pending') as SpliceJobStatus
  const status: SpliceJobStatus = ['Pending', 'Processing', 'Ready', 'Failed'].includes(rawStatus)
    ? rawStatus
    : 'Pending'

  const rawSegments = raw.segments ?? raw.outputs ?? []
  const segments: SpliceSegment[] = rawSegments.map((item, index) => ({
    name: item.name ?? item.fileName ?? `Segment ${index + 1}`,
    duration: item.duration ?? item.durationSeconds ?? 0,
    size: item.size ?? item.sizeBytes ?? 0,
  }))

  const error = raw.error ?? raw.detail ?? raw.message

  return {
    id,
    status,
    segments,
    ...(error ? { error } : {}),
  }
}

export async function createSpliceJob(
  videoId: string,
  request: SpliceRequest,
  signal?: AbortSignal
): Promise<SpliceJobResponse> {
  const url = apiUrl(`/api/videos/${encodeURIComponent(videoId)}/splice`)

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    let message = `Splice request failed: HTTP ${response.status}`
    try {
      const errBody: unknown = await response.json()
      message = parseErrorMessage(errBody, message)
    } catch {
      // Fallback message
    }
    throw new Error(message)
  }

  const data: unknown = await response.json()
  if (!isSpliceJobResponse(data)) {
    throw new Error('Unexpected response format from server')
  }

  return normalizeSpliceJobResponse(data)
}

export async function getSpliceJob(jobId: string, signal?: AbortSignal): Promise<SpliceJobDetailsResponse> {
  const url = apiUrl(`/api/splice-jobs/${encodeURIComponent(jobId)}`)

  const response = await fetch(url, {
    method: 'GET',
    signal,
  })

  if (!response.ok) {
    let message = `Failed to fetch splice job status: HTTP ${response.status}`
    try {
      const errBody: unknown = await response.json()
      message = parseErrorMessage(errBody, message)
    } catch {
      // Fallback message
    }
    throw new Error(message)
  }

  const data = (await response.json()) as RawSpliceJobDetails
  if (!data || typeof data !== 'object') {
    throw new Error('Failed to fetch splice job status: unexpected response shape')
  }

  return normalizeSpliceJobDetails(data, jobId)
}
