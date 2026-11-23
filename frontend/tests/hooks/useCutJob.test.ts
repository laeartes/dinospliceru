import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useCutJob } from '../../src/hooks/useCutJob'
import * as cutApi from '../../src/api/cutApi'
import type { CutJobResponse } from '../../src/api/cutApi'

const baseJob: CutJobResponse = {
  jobId: 'cut-job-1',
  mode: 'FastCopy',
  startSeconds: 5,
  endSeconds: 15,
  status: 'Pending',
  progressPercent: 0,
  error: null,
  outputFileName: null,
  outputDurationSeconds: null,
  outputSizeBytes: null,
}

describe('useCutJob', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
    vi.useRealTimers()
  })

  it('transitions through Pending -> Processing -> Ready while polling', async () => {
    vi.spyOn(cutApi, 'startCut').mockResolvedValue(baseJob)
    const getCutJobSpy = vi
      .spyOn(cutApi, 'getCutJob')
      .mockResolvedValueOnce({ ...baseJob, status: 'Pending', progressPercent: 0 })
      .mockResolvedValueOnce({ ...baseJob, status: 'Processing', progressPercent: 55 })
      .mockResolvedValueOnce({
        ...baseJob,
        status: 'Ready',
        progressPercent: 100,
        outputFileName: 'clip_cut.mp4',
        outputDurationSeconds: 10,
        outputSizeBytes: 512000,
      })

    const { result } = renderHook(() => useCutJob('vid-1'))

    await act(async () => {
      await result.current.startCut({ startSeconds: 5, endSeconds: 15, mode: 'FastCopy' })
    })

    expect(result.current.cutState.status).toBe('polling')
    if (result.current.cutState.status === 'polling') {
      expect(result.current.cutState.job.status).toBe('Pending')
    }

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })

    expect(result.current.cutState.status).toBe('polling')
    if (result.current.cutState.status === 'polling') {
      expect(result.current.cutState.job.status).toBe('Processing')
      expect(result.current.cutState.job.progressPercent).toBe(55)
    }

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })

    expect(result.current.cutState.status).toBe('ready')
    if (result.current.cutState.status === 'ready') {
      expect(result.current.cutState.job.outputFileName).toBe('clip_cut.mp4')
      expect(result.current.cutState.job.outputSizeBytes).toBe(512000)
    }
    expect(getCutJobSpy).toHaveBeenCalledTimes(3)
  })

  it('transitions to failed when backend reports Failed', async () => {
    vi.spyOn(cutApi, 'startCut').mockResolvedValue(baseJob)
    vi.spyOn(cutApi, 'getCutJob').mockResolvedValue({
      ...baseJob,
      status: 'Failed',
      progressPercent: null,
      error: 'FFmpeg failed to cut video.',
    })

    const { result } = renderHook(() => useCutJob('vid-1'))

    await act(async () => {
      await result.current.startCut({ startSeconds: 5, endSeconds: 15, mode: 'FrameAccurate' })
    })

    expect(result.current.cutState.status).toBe('failed')
    if (result.current.cutState.status === 'failed') {
      expect(result.current.cutState.error).toBe('FFmpeg failed to cut video.')
    }
  })

  it('retries transient poll errors and surfaces error after 3 failures', async () => {
    vi.spyOn(cutApi, 'startCut').mockResolvedValue(baseJob)
    vi.spyOn(cutApi, 'getCutJob').mockRejectedValue(new TypeError('Network failure'))

    const { result } = renderHook(() => useCutJob('vid-1'))

    await act(async () => {
      await result.current.startCut({ startSeconds: 5, endSeconds: 15, mode: 'FastCopy' })
    })

    expect(result.current.cutState.status).toBe('polling')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
      await vi.advanceTimersByTimeAsync(1000)
    })

    expect(result.current.cutState.status).toBe('error')
  })

  it('resets state to idle and aborts in-flight poll requests when reset() is called', async () => {
    vi.spyOn(cutApi, 'startCut').mockResolvedValue(baseJob)
    let capturedSignal: AbortSignal | undefined
    vi.spyOn(cutApi, 'getCutJob').mockImplementation((_jobId, signal) => {
      capturedSignal = signal
      return Promise.resolve({ ...baseJob, status: 'Processing', progressPercent: 25 })
    })

    const { result } = renderHook(() => useCutJob('vid-1'))

    await act(async () => {
      await result.current.startCut({ startSeconds: 5, endSeconds: 15, mode: 'FastCopy' })
    })
    expect(result.current.cutState.status).toBe('polling')
    expect(capturedSignal?.aborted).toBe(false)

    act(() => {
      result.current.reset()
    })
    expect(capturedSignal?.aborted).toBe(true)
    expect(result.current.cutState.status).toBe('idle')
  })
})
