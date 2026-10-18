import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useSpliceJob } from '../../src/hooks/useSpliceJob'
import * as spliceApi from '../../src/api/spliceApi'
import { ApiError } from '../../src/api/apiError'

describe('useSpliceJob', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
  })

  it('initializes with idle state', () => {
    const { result } = renderHook(() => useSpliceJob('video-1'))
    expect(result.current.spliceState).toEqual({ status: 'idle' })
  })

  it('does nothing if startSplice is called with null videoId', async () => {
    const createSpy = vi.spyOn(spliceApi, 'createSpliceJob')
    const { result } = renderHook(() => useSpliceJob(null))

    await act(async () => {
      await result.current.startSplice('ByLength', 30)
    })

    expect(createSpy).not.toHaveBeenCalled()
    expect(result.current.spliceState).toEqual({ status: 'idle' })
  })

  it('triggers splice and polls through Pending -> Processing -> Ready, then stops polling', async () => {
    const createSpy = vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-123',
      status: 'Pending',
    })

    const getSpy = vi.spyOn(spliceApi, 'getSpliceJob')
      .mockResolvedValueOnce({
        id: 'job-123',
        status: 'Pending',
        segments: [],
      })
      .mockResolvedValueOnce({
        id: 'job-123',
        status: 'Processing',
        segments: [],
      })
      .mockResolvedValueOnce({
        id: 'job-123',
        status: 'Ready',
        segments: [
          { name: 'part1.mp4', duration: 30, size: 5000000 },
          { name: 'part2.mp4', duration: 30, size: 5000000 },
        ],
      })

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 30)
    })

    expect(createSpy).toHaveBeenCalledWith('video-1', { mode: 'ByLength', value: 30 }, expect.any(Object))
    expect(result.current.spliceState).toEqual({
      status: 'polling',
      jobId: 'job-123',
      jobStatus: 'Pending',
      mode: 'ByLength',
      value: 30,
    })
    expect(getSpy).toHaveBeenCalledTimes(1)

    // Advance 2s: should fetch Processing
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })

    expect(result.current.spliceState).toEqual({
      status: 'polling',
      jobId: 'job-123',
      jobStatus: 'Processing',
      mode: 'ByLength',
      value: 30,
    })
    expect(getSpy).toHaveBeenCalledTimes(2)

    // Advance 2s: should fetch Ready
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })

    expect(result.current.spliceState).toEqual({
      status: 'ready',
      jobId: 'job-123',
      segments: [
        { name: 'part1.mp4', duration: 30, size: 5000000 },
        { name: 'part2.mp4', duration: 30, size: 5000000 },
      ],
    })
    expect(getSpy).toHaveBeenCalledTimes(3)

    // Advance 4s: polling must have stopped
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(getSpy).toHaveBeenCalledTimes(3)
  })

  it('stops polling and transitions to failed state when status becomes Failed', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-err',
      status: 'Pending',
    })

    const getSpy = vi.spyOn(spliceApi, 'getSpliceJob').mockResolvedValueOnce({
      id: 'job-err',
      status: 'Failed',
      segments: [],
      error: 'FFmpeg segmentation error',
    })

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('BySize', 1048576)
    })

    expect(result.current.spliceState).toEqual({
      status: 'failed',
      jobId: 'job-err',
      error: 'FFmpeg segmentation error',
      code: 'SPLICE_JOB_FAILED',
    })
    expect(getSpy).toHaveBeenCalledTimes(1)

    // Ensure polling does not continue
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(getSpy).toHaveBeenCalledTimes(1)
  })

  it('sets error state if trigger request fails', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockRejectedValue(
      new ApiError('Video is not ready to be spliced', 'VIDEO_NOT_READY', 409)
    )

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 15)
    })

    expect(result.current.spliceState).toEqual({
      status: 'error',
      error: 'Video is not ready to be spliced',
      code: 'VIDEO_NOT_READY',
    })
  })

  it('retries transient poll errors and sets error on reaching max failures', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-retry',
      status: 'Pending',
    })

    const getSpy = vi.spyOn(spliceApi, 'getSpliceJob').mockRejectedValue(new Error('Network glitch'))

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 20)
    })

    // Initial check failed (1st failure)
    expect(getSpy).toHaveBeenCalledTimes(1)
    expect(result.current.spliceState.status).toBe('polling')

    // Advance 2s (2nd failure)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })
    expect(getSpy).toHaveBeenCalledTimes(2)
    expect(result.current.spliceState.status).toBe('polling')

    // Advance 2s (3rd failure -> error state)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })
    expect(getSpy).toHaveBeenCalledTimes(3)
    expect(result.current.spliceState).toEqual({
      status: 'error',
      error: 'Network glitch',
      code: 'UNKNOWN_ERROR',
    })
  })

  it('cleans up timers and aborts in-flight request on unmount', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-unmount',
      status: 'Pending',
    })
    const getSpy = vi.spyOn(spliceApi, 'getSpliceJob').mockResolvedValue({
      id: 'job-unmount',
      status: 'Pending',
      segments: [],
    })

    const { result, unmount } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 20)
    })

    expect(getSpy).toHaveBeenCalledTimes(1)

    unmount()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })

    expect(getSpy).toHaveBeenCalledTimes(1)
  })

  it('resets when reset() is called or videoId prop changes', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-reset',
      status: 'Pending',
    })
    vi.spyOn(spliceApi, 'getSpliceJob').mockResolvedValue({
      id: 'job-reset',
      status: 'Ready',
      segments: [{ name: 'res.mp4', duration: 10, size: 1000 }],
    })

    const { result, rerender } = renderHook(({ id }) => useSpliceJob(id), {
      initialProps: { id: 'video-1' as string | null },
    })

    await act(async () => {
      await result.current.startSplice('ByLength', 10)
    })

    expect(result.current.spliceState.status).toBe('ready')

    // Calling reset()
    act(() => {
      result.current.reset()
    })
    expect(result.current.spliceState).toEqual({ status: 'idle' })

    // When videoId prop changes
    await act(async () => {
      await result.current.startSplice('ByLength', 10)
    })
    expect(result.current.spliceState.status).toBe('ready')

    rerender({ id: 'video-2' })
    expect(result.current.spliceState).toEqual({ status: 'idle' })
  })

  it('re-attempts splice with same parameters when retry() is called', async () => {
    const createSpy = vi.spyOn(spliceApi, 'createSpliceJob')
      .mockRejectedValueOnce(new Error('First attempt failed'))
      .mockResolvedValueOnce({
        jobId: 'job-success',
        status: 'Pending',
      })

    vi.spyOn(spliceApi, 'getSpliceJob').mockResolvedValue({
      id: 'job-success',
      status: 'Ready',
      segments: [{ name: 'out.mp4', duration: 10, size: 100 }],
    })

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 25)
    })

    expect(result.current.spliceState.status).toBe('error')

    await act(async () => {
      result.current.retry()
    })

    expect(createSpy).toHaveBeenCalledTimes(2)
    expect(createSpy.mock.calls[1][1]).toEqual({ mode: 'ByLength', value: 25 })
    expect(result.current.spliceState.status).toBe('ready')
  })

  it('stops polling and transitions to error state when job exceeds 1 minute timeout', async () => {
    vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
      jobId: 'job-timeout',
      status: 'Pending',
    })

    const getSpy = vi.spyOn(spliceApi, 'getSpliceJob').mockResolvedValue({
      id: 'job-timeout',
      status: 'Processing',
      segments: [],
    })

    const { result } = renderHook(() => useSpliceJob('video-1'))

    await act(async () => {
      await result.current.startSplice('ByLength', 20)
    })

    expect(result.current.spliceState.status).toBe('polling')
    expect(getSpy).toHaveBeenCalledTimes(1)

    // Advance by 60 seconds (60000ms)
    await act(async () => {
      await vi.advanceTimersByTimeAsync(60000)
    })

    expect(result.current.spliceState).toEqual({
      status: 'error',
      error: 'Splice job timed out after 1 minute (；´Д｀)',
      code: 'REQUEST_TIMEOUT',
    })
  })
})
