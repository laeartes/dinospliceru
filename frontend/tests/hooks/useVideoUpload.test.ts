import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useVideoUpload } from '../../src/hooks/useVideoUpload'
import * as videoApi from '../../src/api/videoApi'

describe('useVideoUpload', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
  })

  it('initializes with idle state', () => {
    const { result } = renderHook(() => useVideoUpload())
    expect(result.current.uploadState).toEqual({ status: 'idle' })
  })

  it('updates upload progress during upload phase', async () => {
    let capturedOnProgress: ((percent: number) => void) | undefined
    const uploadSpy = vi.spyOn(videoApi, 'uploadVideo').mockImplementation((_file, options) => {
      capturedOnProgress = options?.onProgress
      return new Promise(() => {}) // never resolves in this test
    })

    const { result } = renderHook(() => useVideoUpload())
    const file = new File(['data'], 'test.mp4', { type: 'video/mp4' })

    act(() => {
      void result.current.startUpload(file)
    })

    expect(result.current.uploadState).toEqual({
      status: 'uploading',
      fileName: 'test.mp4',
      sizeBytes: 4,
      progress: 0,
    })

    act(() => {
      capturedOnProgress?.(45)
    })

    expect(result.current.uploadState).toEqual({
      status: 'uploading',
      fileName: 'test.mp4',
      sizeBytes: 4,
      progress: 45,
    })

    expect(uploadSpy).toHaveBeenCalledWith(file, expect.any(Object))
  })

  it('polls status and transitions from Processing to Ready and then stops polling', async () => {
    vi.spyOn(videoApi, 'uploadVideo').mockResolvedValue({
      id: 'test-video-id',
      fileName: 'clip.mp4',
      sizeBytes: 1000,
      status: 'Uploaded',
    })

    const getStatusSpy = vi.spyOn(videoApi, 'getVideoStatus')
      .mockResolvedValueOnce({
        id: 'test-video-id',
        fileName: 'clip.mp4',
        contentType: 'video/mp4',
        sizeBytes: 1000,
        durationSeconds: null,
        resolution: null,
        codec: null,
        status: 'Processing',
        createdAt: '2026-10-11T12:00:00Z',
      })
      .mockResolvedValueOnce({
        id: 'test-video-id',
        fileName: 'clip.mp4',
        contentType: 'video/mp4',
        sizeBytes: 1000,
        durationSeconds: 15.5,
        resolution: { width: 1280, height: 720 },
        codec: 'h264',
        status: 'Ready',
        createdAt: '2026-10-11T12:00:00Z',
      })

    const { result } = renderHook(() => useVideoUpload())
    const file = new File(['video'], 'clip.mp4', { type: 'video/mp4' })

    await act(async () => {
      await result.current.startUpload(file)
    })

    // After upload finishes and first poll check returns 'Processing'
    expect(result.current.uploadState.status).toBe('polling')
    if (result.current.uploadState.status === 'polling') {
      expect(result.current.uploadState.videoStatus).toBe('Processing')
    }
    expect(getStatusSpy).toHaveBeenCalledTimes(1)

    // Advance 2 seconds for next poll iteration
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })

    // Second poll check returned 'Ready'
    expect(result.current.uploadState.status).toBe('ready')
    if (result.current.uploadState.status === 'ready') {
      expect(result.current.uploadState.video.durationSeconds).toBe(15.5)
      expect(result.current.uploadState.video.status).toBe('Ready')
    }
    expect(getStatusSpy).toHaveBeenCalledTimes(2)

    // Ensure polling has stopped: advancing more time should not call getVideoStatus again
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(getStatusSpy).toHaveBeenCalledTimes(2)
  })

  it('stops polling when status becomes Failed', async () => {
    vi.spyOn(videoApi, 'uploadVideo').mockResolvedValue({
      id: 'failed-video-id',
      fileName: 'corrupt.mp4',
      sizeBytes: 500,
      status: 'Uploaded',
    })

    const getStatusSpy = vi.spyOn(videoApi, 'getVideoStatus').mockResolvedValueOnce({
      id: 'failed-video-id',
      fileName: 'corrupt.mp4',
      contentType: 'video/mp4',
      sizeBytes: 500,
      durationSeconds: null,
      resolution: null,
      codec: null,
      status: 'Failed',
      createdAt: '2026-10-11T12:00:00Z',
    })

    const { result } = renderHook(() => useVideoUpload())
    const file = new File(['corrupt'], 'corrupt.mp4', { type: 'video/mp4' })

    await act(async () => {
      await result.current.startUpload(file)
    })

    expect(result.current.uploadState).toEqual({
      status: 'failed',
      fileName: 'corrupt.mp4',
      sizeBytes: 500,
      error: 'Processing failed on the server',
    })
    expect(getStatusSpy).toHaveBeenCalledTimes(1)

    // Ensure no more polls are scheduled
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(getStatusSpy).toHaveBeenCalledTimes(1)
  })

  it('handles upload errors gracefully into error state', async () => {
    vi.spyOn(videoApi, 'uploadVideo').mockRejectedValue(new Error('Network error during upload'))

    const { result } = renderHook(() => useVideoUpload())
    const file = new File(['fail'], 'fail.mp4', { type: 'video/mp4' })

    await act(async () => {
      await result.current.startUpload(file)
    })

    expect(result.current.uploadState).toEqual({
      status: 'error',
      fileName: 'fail.mp4',
      sizeBytes: 4,
      error: 'Network error during upload',
    })
  })

  it('resets state back to idle on reset()', async () => {
    vi.spyOn(videoApi, 'uploadVideo').mockRejectedValue(new Error('Failed'))

    const { result } = renderHook(() => useVideoUpload())
    const file = new File(['x'], 'x.mp4', { type: 'video/mp4' })

    await act(async () => {
      await result.current.startUpload(file)
    })

    expect(result.current.uploadState.status).toBe('error')

    act(() => {
      result.current.reset()
    })

    expect(result.current.uploadState).toEqual({ status: 'idle' })
  })
})
