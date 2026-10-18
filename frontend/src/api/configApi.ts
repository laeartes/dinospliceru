import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse } from './apiError'

interface VideoUploadConfig {
  maxFileSizeBytes: number
  allowedExtensions: string[]
  allowedContentTypes: string[]
}

interface AppConfig {
  videoUpload: VideoUploadConfig
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string')
}

function isAppConfig(value: unknown): value is AppConfig {
  const videoUpload = (value as Partial<AppConfig> | null)?.videoUpload
  return (
    typeof videoUpload?.maxFileSizeBytes === 'number' &&
    isStringArray(videoUpload.allowedExtensions) &&
    isStringArray(videoUpload.allowedContentTypes)
  )
}

async function fetchConfig(signal?: AbortSignal): Promise<AppConfig> {
  const response = await fetch(apiUrl('/api/config'), { signal })
  if (!response.ok) {
    throw await readErrorResponse(response, `Failed to load config: HTTP ${response.status}`)
  }

  const body: unknown = await response.json()
  if (!isAppConfig(body)) {
    throw new ApiError('Failed to load config: unexpected response shape', ClientErrorCodes.UnexpectedResponse)
  }

  return body
}

export { fetchConfig }
export type { AppConfig, VideoUploadConfig }
