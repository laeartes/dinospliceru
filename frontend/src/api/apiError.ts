interface ApiErrorPayload {
    error: string
    code: string
}

const ClientErrorCodes = {
    NetworkError: 'NETWORK_ERROR',
    RequestTimeout: 'REQUEST_TIMEOUT',
    UnexpectedResponse: 'UNEXPECTED_RESPONSE',
    VideoProcessingFailed: 'VIDEO_PROCESSING_FAILED',
    SpliceJobFailed: 'SPLICE_JOB_FAILED',
    UnknownError: 'UNKNOWN_ERROR',
} as const

function isApiErrorPayload(value: unknown): value is ApiErrorPayload{
    if(!value || typeof value !== 'object') return false
    const obj = value as { error?: unknown; code?: unknown }
    return (
        typeof obj.error === 'string' && obj.error.length > 0 &&
        typeof obj.code === 'string' && obj.code.length > 0
    )
}

class ApiError extends Error {
    readonly code: string
    readonly status?: number

    constructor(message: string, code: string, status?: number) {
        super(message)
        this.name = 'ApiError'
        this.code = code
        this.status = status
    }
}

function toApiError(body: unknown, status: number, fallbackMessage: string): ApiError {
    if (isApiErrorPayload(body)) {
        return new ApiError(body.error, body.code, status)
    }

    return new ApiError(fallbackMessage, ClientErrorCodes.UnexpectedResponse, status)
}

async function readErrorResponse(response: Response, fallbackMessage: string): Promise<ApiError> {
    let body: unknown = null
    try {
        body = await response.json()
    } catch {
        // Body empty or not JSON
    }
    return toApiError(body, response.status, fallbackMessage)
}

function toErrorPayload(err: unknown, fallbackMessage: string): ApiErrorPayload {
    if (err instanceof ApiError) {
        return { error: err.message, code: err.code }
    }
    if (err instanceof TypeError) {
        return { error: 'Could not reach the server. Check your connection and try again.', code: ClientErrorCodes.NetworkError }
    }
    if (err instanceof Error && err.message) {
        return { error: err.message, code: ClientErrorCodes.UnknownError }
    }
    return { error: fallbackMessage, code : ClientErrorCodes.UnknownError }
}

export { ApiError, ClientErrorCodes, isApiErrorPayload, toApiError, readErrorResponse, toErrorPayload }
export type { ApiErrorPayload }