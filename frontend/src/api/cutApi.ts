import { apiUrl } from './apiClient'
import { ApiError, ClientErrorCodes, readErrorResponse } from './apiError'

export type CutMode = 'FastCopy' | 'FrameAccurate'

export type CutJobStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed'

export interface CutRequest {
  startSeconds: number
  endSeconds: number
  mode: CutMode
}

export interface CutJobResponse {
  jobId: string
  mode: CutMode
  startSeconds: number
  endSeconds: number
  status: CutJobStatus
  progressPercent: number | null
  error: string | null
  outputFileName: string | null
  outputDurationSeconds: number | null
  outputSizeBytes: number | null
}

const VALID_CUT_MODES: ReadonlyArray<CutMode> = ['FastCopy', 'FrameAccurate']
const VALID_CUT_STATUSES: ReadonlyArray<CutJobStatus> = ['Pending', 'Processing', 'Ready', 'Failed']

function isCutJobResponse(value: unknown): value is CutJobResponse {
  if (!value || typeof value !== 'object') return false
  const obj = value as Record<string, unknown>
  return (
    typeof obj.jobId === 'string' &&
    typeof obj.mode === 'string' &&
    VALID_CUT_MODES.includes(obj.mode as CutMode) &&
    typeof obj.startSeconds === 'number' &&
    typeof obj.endSeconds === 'number' &&
    typeof obj.status === 'string' &&
    VALID_CUT_STATUSES.includes(obj.status as CutJobStatus)
  )
}

function normalizeCutJobResponse(raw: CutJobResponse): CutJobResponse {
  return {
    jobId: raw.jobId,
    mode: raw.mode,
    startSeconds: raw.startSeconds,
    endSeconds: raw.endSeconds,
    status: raw.status,
    progressPercent: typeof raw.progressPercent === 'number' ? raw.progressPercent : null,
    error: typeof raw.error === 'string' ? raw.error : null,
    outputFileName: typeof raw.outputFileName === 'string' ? raw.outputFileName : null,
    outputDurationSeconds: typeof raw.outputDurationSeconds === 'number' ? raw.outputDurationSeconds : null,
    outputSizeBytes: typeof raw.outputSizeBytes === 'number' ? raw.outputSizeBytes : null,
  }
}

/** Triggers a new cut job for a ready source video. */
export async function startCut(
  videoId: string,
  request: CutRequest,
  signal?: AbortSignal
): Promise<CutJobResponse> {
  const url = apiUrl(`/api/videos/${encodeURIComponent(videoId)}/cut`)

  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(response, `Cut request failed: HTTP ${response.status}`)
  }

  const data: unknown = await response.json()
  if (!isCutJobResponse(data)) {
    throw new ApiError('Unexpected response format from server', ClientErrorCodes.UnexpectedResponse)
  }

  return normalizeCutJobResponse(data)
}

/** Fetches the current status, progress, and output metadata for a cut job. */
export async function getCutJob(jobId: string, signal?: AbortSignal): Promise<CutJobResponse> {
  const url = apiUrl(`/api/cut-jobs/${encodeURIComponent(jobId)}`)

  const response = await fetch(url, {
    method: 'GET',
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(response, `Failed to fetch cut job status: HTTP ${response.status}`)
  }

  const data: unknown = await response.json()
  if (!isCutJobResponse(data)) {
    throw new ApiError(
      'Failed to fetch cut job status: unexpected response shape',
      ClientErrorCodes.UnexpectedResponse
    )
  }

  return normalizeCutJobResponse(data)
}

/** Deletes a cut job and its generated output file from the server. */
export async function deleteCutJob(jobId: string, signal?: AbortSignal): Promise<void> {
  const url = apiUrl(`/api/cut-jobs/${encodeURIComponent(jobId)}`)

  const response = await fetch(url, {
    method: 'DELETE',
    signal,
  })

  if (!response.ok) {
    throw await readErrorResponse(response, `Failed to delete cut job: HTTP ${response.status}`)
  }
}

/** Builds the download URL for a completed cut job clip. */
export function getCutDownloadUrl(jobId: string): string {
  return apiUrl(`/api/cut-jobs/${encodeURIComponent(jobId)}/download`)
}
