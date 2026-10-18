import { describe, it, expect } from 'vitest'
import {
    ApiError,
    isApiErrorPayload,
    readErrorResponse,
    toApiError,
    toErrorPayload,
} from '../../src/api/apiError'

describe('isApiErrorPayload', () => {
    it('accepts the standard shape', () => {
        expect(isApiErrorPayload({ error: 'Video not found', code: 'VIDEO_NOT_FOUND' })).toBe(true)
    })

    it('rejects values that are not the standard shape', () => {
        expect(isApiErrorPayload(null)).toBe(false)
        expect(isApiErrorPayload('Video not found')).toBe(false)
        expect(isApiErrorPayload({ title: 'Video not found', detail: 'No video' })).toBe(false)
        expect(isApiErrorPayload({ error: 'Video not found' })).toBe(false)
        expect(isApiErrorPayload({ error: 'Video not found', code: '' })).toBe(false)
        expect(isApiErrorPayload({ error: '', code: 'VIDEO_NOT_FOUND' })).toBe(false)
        expect(isApiErrorPayload({ error: 'Bad', code: 42 })).toBe(false)
  })
})

describe('toApiError', () => {
    it('uses the server message, code and status when the body has the standard shape', () => {
        const error = toApiError({ error: 'File too large', code: 'FILE_TOO_LARGE' }, 413, 'fallback')

        expect(error).toBeInstanceOf(ApiError)
        expect(error.message).toBe('File too large')
        expect(error.code).toBe('FILE_TOO_LARGE')
        expect(error.status).toBe(413)
    })

    it('falls back to the given message when the body is not the standard shape', () => {
        const error = toApiError('<html>Bad Gateway</html>', 502, 'Upload failed: HTTP 502')

        expect(error.message).toBe('Upload failed: HTTP 502')
        expect(error.code).toBe('UNEXPECTED_RESPONSE')
        expect(error.status).toBe(502)
    })
})

describe('readErrorResponse', () => {
    it('reads the JSON body of a failed response', async () => {
        const response = {
            status: 404,
            json: () => Promise.resolve({ error: 'Video not found', code: 'VIDEO_NOT_FOUND' }),
        } as Response

        const error = await readErrorResponse(response, 'fallback')

        expect(error.message).toBe('Video not found')
        expect(error.code).toBe('VIDEO_NOT_FOUND')
    })

  it('falls back when the body is empty or not JSON', async () => {
        const response = {
            status: 500,
            json: () => Promise.reject(new SyntaxError('Unexpected end of JSON input')),
        } as unknown as Response

        const error = await readErrorResponse(response, 'Request failed: HTTP 500')

        expect(error.message).toBe('Request failed: HTTP 500')
        expect(error.status).toBe(500)
    })
})

describe('toErrorPayload', () => {
    it('keeps the code from an ApiError', () => {
        expect(toErrorPayload(new ApiError('Video not found', 'VIDEO_NOT_FOUND', 404), 'fallback')).toEqual({
            error: 'Video not found',
            code: 'VIDEO_NOT_FOUND',
        })
    })

    it('gives a fetch network failure to the NETWORK_ERROR code', () => {
        expect(toErrorPayload(new TypeError('Failed to fetch'), 'fallback')).toEqual({
            error: 'Could not reach the server. Check your connection and try again.',
            code: 'NETWORK_ERROR',
        })
    })

    it('gives any other Error the UNKNOWN_ERROR code and keeps its message', () => {
        expect(toErrorPayload(new Error('Something broke'), 'fallback')).toEqual({
            error: 'Something broke',
            code: 'UNKNOWN_ERROR',
        })
    })

    it('uses the fallback message for anything else', () => {
        expect(toErrorPayload('oops', 'Upload failed')).toEqual({ error: 'Upload failed', code: 'UNKNOWN_ERROR' })
    })
})