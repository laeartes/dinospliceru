import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse, toApiError } from './apiError'

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

export interface DownloadSpliceOutputOptions {
  onProgress?: (percent: number) => void
  signal?: AbortSignal
}

export function downloadSpliceOutput(
  outputId: string,
  fileName: string,
  options: DownloadSpliceOutputOptions = {}
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('GET', getSpliceOutputDownloadUrl(outputId))
    // Let the browser own the response buffer instead of piling every chunk into the JS heap
    xhr.responseType = 'blob'

    if (options.signal) {
      if (options.signal.aborted) {
        reject(new DOMException('Download aborted', 'AbortError'))
        return
      }

      options.signal.addEventListener(
        'abort',
        () => {
          xhr.abort()
          reject(new DOMException('Download aborted', 'AbortError'))
        },
        { once: true }
      )
    }

    if (options.onProgress) {
      xhr.onprogress = (event: ProgressEvent) => {
        if (event.lengthComputable && event.total > 0) {
          options.onProgress?.(Math.min(100, Math.round((event.loaded / event.total) * 100)))
        }
      }
    }

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        saveBlob(xhr.response as Blob, fileName)
        resolve()
      } else {
        void rejectFromXhr(xhr, reject)
      }
    }

    xhr.onerror = () => {
      reject(new ApiError('Network error during download', ClientErrorCodes.NetworkError))
    }

    xhr.ontimeout = () => {
      reject(new ApiError('Download request timed out', ClientErrorCodes.RequestTimeout))
    }

    xhr.send()
  })
}

async function rejectFromXhr(xhr: XMLHttpRequest, reject: (reason: unknown) => void): Promise<void> {
  let body: unknown = null
  try {
    body = JSON.parse(await (xhr.response as Blob).text())
  } catch {
    // Body was empty or not JSON
  }
  reject(toApiError(body, xhr.status, `Download failed: HTTP ${xhr.status}`))
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
