import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse } from './apiError'

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
  id: string
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
    id?: string
    name?: string
    fileName?: string
    duration?: number
    durationSeconds?: number
    size?: number
    sizeBytes?: number
  }>
  outputs?: Array<{
    id?: string
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
    id: item.id ?? '',
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
    throw await readErrorResponse(response, `Splice request failed: HTTP ${response.status}`)
  }

  const data: unknown = await response.json()
  if (!isSpliceJobResponse(data)) {
    throw new ApiError('Unexpected response format from server', ClientErrorCodes.UnexpectedResponse)
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
    throw await readErrorResponse(response, `Failed to fetch splice job status: HTTP ${response.status}`)
  }

  const data = (await response.json()) as RawSpliceJobDetails
  if (!data || typeof data !== 'object') {
    throw new ApiError('Failed to fetch splice job status: unexpected response shape', ClientErrorCodes.UnexpectedResponse)
  }

  return normalizeSpliceJobDetails(data, jobId)
}

export function getSpliceOutputDownloadUrl(outputId: string): string {
  return apiUrl(`/api/splice-outputs/${encodeURIComponent(outputId)}/download`)
}

export async function downloadSpliceOutput(
  outputId: string,
  fileName: string,
  onProgress?: (percent: number) => void
): Promise<void> {
  const response = await fetch(getSpliceOutputDownloadUrl(outputId))
  if (!response.ok) {
    throw await readErrorResponse(response, `Download failed: HTTP ${response.status}`)
  }

  saveBlob(await readBlobWithProgress(response, onProgress), fileName)
}

async function readBlobWithProgress(
  response: Response,
  onProgress?: (percent: number) => void
): Promise<Blob> {
  // Without a callback or a streamed body there is nothing to report, so take the simple path
  if (!onProgress || !response.body) {
    return response.blob()
  }

  const total = Number(response.headers.get('content-length')) || 0
  if (!total) {
    return response.blob()
  }

  const reader = response.body.getReader()
  const chunks: BlobPart[] = []
  let received = 0
  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    chunks.push(value)
    received += value.length
    onProgress(Math.min(100, Math.round((received / total) * 100)))
  }

  return new Blob(chunks, { type: response.headers.get('content-type') ?? undefined })
}

function saveBlob(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob)
  try {
    const link = document.createElement('a')
    link.href = objectUrl
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
  } finally {
    setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)
  }
}
