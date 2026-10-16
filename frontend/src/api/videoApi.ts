import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse, toApiError } from './apiError'

type VideoStatus = 'Uploaded' | 'Processing' | 'Ready' | 'Failed'

interface Resolution {
  width: number
  height: number
}

interface VideoUploadResponse {
  id: string
  fileName: string
  sizeBytes: number
  status: string
}

interface VideoResponse {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  durationSeconds: number | null
  resolution: Resolution | null
  codec: string | null
  status: VideoStatus
  createdAt: string
}

function isVideoUploadResponse(value: unknown): value is VideoUploadResponse {
  const obj = value as Partial<VideoUploadResponse> | null
  return (
    typeof obj?.id === 'string' &&
    typeof obj?.fileName === 'string' &&
    typeof obj?.sizeBytes === 'number' &&
    typeof obj?.status === 'string'
  )
}

function isVideoResponse(value: unknown): value is VideoResponse {
  const obj = value as Partial<VideoResponse> | null
  return (
    typeof obj?.id === 'string' &&
    typeof obj?.fileName === 'string' &&
    typeof obj?.contentType === 'string' &&
    typeof obj?.sizeBytes === 'number' &&
    typeof obj?.status === 'string' &&
    typeof obj?.createdAt === 'string'
  )
}

interface UploadVideoOptions {
  onProgress?: (progressPercent: number) => void
  signal?: AbortSignal
}

function uploadVideo(file: File, options?: UploadVideoOptions): Promise<VideoUploadResponse> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    const url = apiUrl('/api/videos')

    if (options?.signal) {
      if (options.signal.aborted) {
        reject(new DOMException('Upload aborted', 'AbortError'))
        return
      }

      const onAbort = () => {
        try {
          xhr.abort?.()
        } catch {
          // ignore abort failure
        }
        reject(new DOMException('Upload aborted', 'AbortError'))
      }

      options.signal.addEventListener('abort', onAbort, { once: true })

      xhr.addEventListener?.('loadend', () => {
        options.signal?.removeEventListener('abort', onAbort)
      })
    }

    if (xhr.upload && options?.onProgress) {
      xhr.upload.onprogress = (event: ProgressEvent) => {
        if (event.lengthComputable && event.total > 0) {
          const percent = Math.min(100, Math.round((event.loaded / event.total) * 100))
          options.onProgress?.(percent)
        }
      }
    }

    xhr.open('POST', url)

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const parsed: unknown = JSON.parse(xhr.responseText)
          if (isVideoUploadResponse(parsed)) {
            resolve(parsed)
            return
          }
          reject(new ApiError('Unexpected response format from server', ClientErrorCodes.UnexpectedResponse))
        } catch {
          reject(new ApiError('Failed to parse server response', ClientErrorCodes.UnexpectedResponse))
        }
      } else {
        let errBody: unknown = null
        try {
          errBody = JSON.parse(xhr.responseText)
        } catch {
          // Body was empty or not JSON
        }
        reject(toApiError(errBody, xhr.status, `Upload failed: HTTP ${xhr.status}`))
      }
    }

    xhr.onerror = () => {
      reject(new ApiError('Network error during upload', ClientErrorCodes.NetworkError))
    }

    xhr.ontimeout = () => {
      reject(new ApiError('Upload request timed out', ClientErrorCodes.RequestTimeout))
    }

    const formData = new FormData()
    formData.append('file', file)

    xhr.send(formData)
  })
}

async function getVideoStatus(id: string, signal?: AbortSignal): Promise<VideoResponse> {
  const response = await fetch(apiUrl(`/api/videos/${encodeURIComponent(id)}`), { signal })
  if (!response.ok) {
    throw await readErrorResponse(response, `Failed to fetch video status: HTTP ${response.status}`)
  }

  const data: unknown = await response.json()
  if (!isVideoResponse(data)) {
    throw new ApiError('Failed to fetch video status: unexpected response shape', ClientErrorCodes.UnexpectedResponse)
  }

  return data
}

export { uploadVideo, getVideoStatus }
export type { VideoUploadResponse, VideoResponse, VideoStatus, Resolution, UploadVideoOptions }
