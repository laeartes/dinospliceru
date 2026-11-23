import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse } from './apiError'

export type OutputContainerFormat = 'Mp4' | 'Webm'
export type VideoCodecOption = 'H264' | 'Vp9'
export type EncodingPresetOption = 'Ultrafast' | 'Fast' | 'Medium' | 'Slow'
export type CompressionJobStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed'

export interface CompressionRequest {
  format: OutputContainerFormat
  videoCodec: VideoCodecOption
  crf: number
  targetWidth: number | null
  targetHeight: number | null
  audioBitrateKbps: number | null
  preset: EncodingPresetOption
}

export interface CompressionJobResponse {
  jobId: string
  format: OutputContainerFormat
  videoCodec: VideoCodecOption
  crf: number
  targetWidth: number | null
  targetHeight: number | null
  audioBitrateKbps: number | null
  preset: EncodingPresetOption
  status: CompressionJobStatus
  progressPercent: number | null
  error: string | null
  outputFileName: string | null
  outputDurationSeconds: number | null
  outputSizeBytes: number | null
}

const VALID_FORMATS: ReadonlyArray<OutputContainerFormat> = ['Mp4', 'Webm']
const VALID_CODECS: ReadonlyArray<VideoCodecOption> = ['H264', 'Vp9']
const VALID_PRESETS: ReadonlyArray<EncodingPresetOption> = ['Ultrafast', 'Fast', 'Medium', 'Slow']
const VALID_STATUSES: ReadonlyArray<CompressionJobStatus> = ['Pending', 'Processing', 'Ready', 'Failed']

function isCompressionJobResponse(value: unknown): value is CompressionJobResponse {
  if (!value || typeof value !== 'object') return false
  const obj = value as Record<string, unknown>
  return (
    typeof obj.jobId === 'string' &&
    VALID_FORMATS.includes(obj.format as OutputContainerFormat) &&
    VALID_CODECS.includes(obj.videoCodec as VideoCodecOption) &&
    typeof obj.crf === 'number' &&
    VALID_PRESETS.includes(obj.preset as EncodingPresetOption) &&
    VALID_STATUSES.includes(obj.status as CompressionJobStatus)
  )
}

function normalizeResponse(raw: CompressionJobResponse): CompressionJobResponse {
  return {
    jobId: raw.jobId,
    format: raw.format,
    videoCodec: raw.videoCodec,
    crf: raw.crf,
    targetWidth: typeof raw.targetWidth === 'number' ? raw.targetWidth : null,
    targetHeight: typeof raw.targetHeight === 'number' ? raw.targetHeight : null,
    audioBitrateKbps: typeof raw.audioBitrateKbps === 'number' ? raw.audioBitrateKbps : null,
    preset: raw.preset,
    status: raw.status,
    progressPercent: typeof raw.progressPercent === 'number' ? raw.progressPercent : null,
    error: typeof raw.error === 'string' ? raw.error : null,
    outputFileName: typeof raw.outputFileName === 'string' ? raw.outputFileName : null,
    outputDurationSeconds: typeof raw.outputDurationSeconds === 'number' ? raw.outputDurationSeconds : null,
    outputSizeBytes: typeof raw.outputSizeBytes === 'number' ? raw.outputSizeBytes : null,
  }
}

/** Triggers a new compression job for a ready source video. */
export async function startCompression(
  videoId: string,
  request: CompressionRequest,
  signal?: AbortSignal
): Promise<CompressionJobResponse> {
  const response = await fetch(apiUrl(`/api/videos/${encodeURIComponent(videoId)}/compress`), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(response, `Compression request failed: HTTP ${response.status}`)
  }

  const data: unknown = await response.json()
  if (!isCompressionJobResponse(data)) {
    throw new ApiError('Unexpected response format from server', ClientErrorCodes.UnexpectedResponse)
  }

  return normalizeResponse(data)
}

/** Fetches the current status, progress, and output metadata for a compression job. */
export async function getCompressionJob(
  jobId: string,
  signal?: AbortSignal
): Promise<CompressionJobResponse> {
  const response = await fetch(apiUrl(`/api/compression-jobs/${encodeURIComponent(jobId)}`), {
    method: 'GET',
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(
      response,
      `Failed to fetch compression job status: HTTP ${response.status}`
    )
  }

  const data: unknown = await response.json()
  if (!isCompressionJobResponse(data)) {
    throw new ApiError(
      'Failed to fetch compression job status: unexpected response shape',
      ClientErrorCodes.UnexpectedResponse
    )
  }

  return normalizeResponse(data)
}

/** Deletes a compression job and its generated output file from the server. */
export async function deleteCompressionJob(jobId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(apiUrl(`/api/compression-jobs/${encodeURIComponent(jobId)}`), {
    method: 'DELETE',
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(response, `Failed to delete compression job: HTTP ${response.status}`)
  }
}

/** Builds the download URL for a completed compression job. */
export function getCompressionDownloadUrl(jobId: string): string {
  return apiUrl(`/api/compression-jobs/${encodeURIComponent(jobId)}/download`)
}
