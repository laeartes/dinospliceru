import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useCompressionJob } from '../../src/hooks/useCompressionJob'
import * as compressionApi from '../../src/api/compressionApi'
import type { CompressionJobResponse, CompressionRequest } from '../../src/api/compressionApi'

const sampleRequest: CompressionRequest = {
  format: 'Mp4',
  videoCodec: 'H264',
  crf: 23,
  targetWidth: null,
  targetHeight: null,
  audioBitrateKbps: 128,
  preset: 'Medium',
}

const baseJob: CompressionJobResponse = {
  jobId: 'comp-job-1',
  ...sampleRequest,
  status: 'Pending',
  progressPercent: 0,
  error: null,
  outputFileName: null,
  outputDurationSeconds: null,
  outputSizeBytes: null,
}

describe('useCompressionJob', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
    vi.useRealTimers()
  })

  it('polls through Pending -> Processing -> Ready', async () => {
    vi.spyOn(compressionApi, 'startCompression').mockResolvedValue(baseJob)
    vi.spyOn(compressionApi, 'getCompressionJob')
      .mockResolvedValueOnce({ ...baseJob, status: 'Pending', progressPercent: 0 })
      .mockResolvedValueOnce({ ...baseJob, status: 'Processing', progressPercent: 48 })
      .mockResolvedValueOnce({
        ...baseJob,
        status: 'Ready',
        progressPercent: 100,
        outputFileName: 'video_compressed.mp4',
        outputDurationSeconds: 45,
        outputSizeBytes: 4_000_000,
      })

    const { result } = renderHook(() => useCompressionJob('vid-1'))

    await act(async () => {
      await result.current.startCompression(sampleRequest)
    })
    expect(result.current.compressionState.status).toBe('polling')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
    if (result.current.compressionState.status === 'polling') {
      expect(result.current.compressionState.job.progressPercent).toBe(48)
    }

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
    expect(result.current.compressionState.status).toBe('ready')
  })

  it('transitions to failed when backend reports Failed', async () => {
    vi.spyOn(compressionApi, 'startCompression').mockResolvedValue(baseJob)
    vi.spyOn(compressionApi, 'getCompressionJob').mockResolvedValue({
      ...baseJob,
      status: 'Failed',
      progressPercent: null,
      error: 'Encoder crashed.',
    })

    const { result } = renderHook(() => useCompressionJob('vid-1'))

    await act(async () => {
      await result.current.startCompression(sampleRequest)
    })

    expect(result.current.compressionState.status).toBe('failed')
    if (result.current.compressionState.status === 'failed') {
      expect(result.current.compressionState.error).toBe('Encoder crashed.')
    }
  })

  it('aborts in-flight poll requests when reset() is called', async () => {
    vi.spyOn(compressionApi, 'startCompression').mockResolvedValue(baseJob)
    let capturedSignal: AbortSignal | undefined
    vi.spyOn(compressionApi, 'getCompressionJob').mockImplementation((_id, signal) => {
      capturedSignal = signal
      return Promise.resolve({ ...baseJob, status: 'Processing', progressPercent: 20 })
    })

    const { result } = renderHook(() => useCompressionJob('vid-1'))

    await act(async () => {
      await result.current.startCompression(sampleRequest)
    })
    expect(capturedSignal?.aborted).toBe(false)

    act(() => {
      result.current.reset()
    })
    expect(capturedSignal?.aborted).toBe(true)
    expect(result.current.compressionState.status).toBe('idle')
  })
})
