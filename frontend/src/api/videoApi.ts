import { apiUrl } from './apiClient'

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
          reject(new Error('Unexpected response format from server'))
        } catch {
          reject(new Error('Failed to parse server response'))
        }
      } else {
        let message = `Upload failed: HTTP ${xhr.status}`
        try {
          const errBody = JSON.parse(xhr.responseText) as { detail?: string; title?: string }
          if (errBody.detail) {
            message = errBody.detail
          } else if (errBody.title) {
            message = errBody.title
          }
        } catch {
          // Fall back to default status string
        }
        reject(new Error(message))
      }
    }

    xhr.onerror = () => {
      reject(new Error('Network error during upload'))
    }

    xhr.ontimeout = () => {
      reject(new Error('Upload request timed out'))
    }

    const formData = new FormData()
    formData.append('file', file)

    xhr.send(formData)
  })
}

async function getVideoStatus(id: string, signal?: AbortSignal): Promise<VideoResponse> {
  const response = await fetch(apiUrl(`/api/videos/${encodeURIComponent(id)}`), { signal })
  if (!response.ok) {
    let message = `Failed to fetch video status: HTTP ${response.status}`
    try {
      const errBody = (await response.json()) as { detail?: string; title?: string }
      if (errBody.detail) {
        message = errBody.detail
      } else if (errBody.title) {
        message = errBody.title
      }
    } catch {
      // Fallback
    }
    throw new Error(message)
  }

  const data: unknown = await response.json()
  if (!isVideoResponse(data)) {
    throw new Error('Failed to fetch video status: unexpected response shape')
  }

  return data
}

export { uploadVideo, getVideoStatus }
export type { VideoUploadResponse, VideoResponse, VideoStatus, Resolution, UploadVideoOptions }
