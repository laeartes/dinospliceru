import { renderHook, waitFor, act } from '@testing-library/react'
import { describe, it, expect, vi, afterEach } from 'vitest'
import { useUploadConfig } from '../../src/hooks/useUploadConfig'

const videoUpload = {
  maxFileSizeBytes: 2048,
  allowedExtensions: ['.mkv'],
  allowedContentTypes: ['video/x-matroska'],
}

function okResponse() {
  return { ok: true, status: 200, json: () => Promise.resolve({ videoUpload }) }
}

describe('useUploadConfig', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts in the loading state and then exposes the videoUpload config', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(okResponse()))

    const { result } = renderHook(() => useUploadConfig())

    expect(result.current.status).toBe('loading')
    await waitFor(() => expect(result.current.status).toBe('ready'))
    expect(result.current).toMatchObject({ status: 'ready', config: videoUpload })
  })

  it('switches to the error state when loading fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const { result } = renderHook(() => useUploadConfig())

    await waitFor(() => expect(result.current.status).toBe('error'))
  })

  it('fetches again on retry and recovers', async () => {
    const fetchMock = vi.fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce(okResponse())
    vi.stubGlobal('fetch', fetchMock)

    const { result } = renderHook(() => useUploadConfig())
    await waitFor(() => expect(result.current.status).toBe('error'))

    act(() => result.current.retry())

    expect(result.current.status).toBe('loading')
    await waitFor(() => expect(result.current.status).toBe('ready'))
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})
