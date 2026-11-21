import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import CutPanel from '../../src/components/CutPanel'
import type { VideoResponse } from '../../src/api/videoApi'
import * as cutApi from '../../src/api/cutApi'

const mockReadyVideo: VideoResponse = {
  id: 'vid-cut-123',
  fileName: 'miku_live.mp4',
  contentType: 'video/mp4',
  sizeBytes: 15 * 1024 * 1024,
  durationSeconds: 60,
  resolution: { width: 1920, height: 1080 },
  codec: 'h264',
  status: 'Ready',
  createdAt: '2026-11-21T19:00:00Z',
}

describe('CutPanel', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
    vi.useRealTimers()
  })

  describe('timestamp parsing and form validation', () => {
    it('initializes with 00:00.0 to video duration and FastCopy mode', () => {
      render(<CutPanel video={mockReadyVideo} />)

      expect(screen.getByTestId('cut-start-input')).toHaveValue('00:00.0')
      expect(screen.getByTestId('cut-end-input')).toHaveValue('01:00.0')
      expect(screen.getByTestId('cut-mode-fast')).toHaveAttribute('aria-checked', 'true')
      expect(screen.getByTestId('cut-mode-accurate')).toHaveAttribute('aria-checked', 'false')
      expect(screen.getByTestId('start-cut-btn')).not.toBeDisabled()
    })

    it('blocks submission when start >= end', () => {
      const startCutSpy = vi.spyOn(cutApi, 'startCut')
      render(<CutPanel video={mockReadyVideo} />)

      fireEvent.change(screen.getByTestId('cut-start-input'), { target: { value: '00:30.0' } })
      fireEvent.change(screen.getByTestId('cut-end-input'), { target: { value: '00:20.0' } })

      expect(screen.getByTestId('cut-validation-error')).toHaveTextContent(
        /start time must be less than end time/i
      )
      expect(screen.getByTestId('start-cut-btn')).toBeDisabled()

      fireEvent.submit(screen.getByTestId('cut-form'))
      expect(startCutSpy).not.toHaveBeenCalled()
    })

    it('blocks submission for negative timestamps', () => {
      const startCutSpy = vi.spyOn(cutApi, 'startCut')
      render(<CutPanel video={mockReadyVideo} />)

      fireEvent.change(screen.getByTestId('cut-start-input'), { target: { value: '-00:05.0' } })

      expect(screen.getByTestId('cut-validation-error')).toHaveTextContent(/cannot be negative/i)
      expect(screen.getByTestId('start-cut-btn')).toBeDisabled()

      fireEvent.submit(screen.getByTestId('cut-form'))
      expect(startCutSpy).not.toHaveBeenCalled()
    })

    it('blocks submission when end exceeds video duration', () => {
      const startCutSpy = vi.spyOn(cutApi, 'startCut')
      render(<CutPanel video={mockReadyVideo} />)

      fireEvent.change(screen.getByTestId('cut-end-input'), { target: { value: '01:15.0' } })

      expect(screen.getByTestId('cut-validation-error')).toHaveTextContent(
        /cannot exceed video duration/i
      )
      expect(screen.getByTestId('start-cut-btn')).toBeDisabled()

      fireEvent.submit(screen.getByTestId('cut-form'))
      expect(startCutSpy).not.toHaveBeenCalled()
    })

    it('renders local video preview and sets start/end from currentTime when localFile is provided', () => {
      const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock-video')
      const revokeObjectURLSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
      const localFile = new File(['video-bytes'], 'miku_live.mp4', { type: 'video/mp4' })

      const { unmount } = render(<CutPanel video={mockReadyVideo} localFile={localFile} />)

      const videoEl = screen.getByTestId('cut-video-preview') as HTMLVideoElement
      expect(videoEl).toHaveAttribute('src', 'blob:mock-video')

      Object.defineProperty(videoEl, 'currentTime', { value: 12.4, writable: true })
      fireEvent.click(screen.getByTestId('set-start-from-video-btn'))
      expect(screen.getByTestId('cut-start-input')).toHaveValue('00:12.4')

      Object.defineProperty(videoEl, 'currentTime', { value: 45.8, writable: true })
      fireEvent.click(screen.getByTestId('set-end-from-video-btn'))
      expect(screen.getByTestId('cut-end-input')).toHaveValue('00:45.8')

      unmount()
      expect(createObjectURLSpy).toHaveBeenCalledWith(localFile)
      expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:mock-video')
    })
  })

  describe('submitting cut request', () => {
    it('fires POST /api/videos/{id}/cut with expected startSeconds, endSeconds, and mode', async () => {
      const startCutSpy = vi.spyOn(cutApi, 'startCut').mockResolvedValue({
        jobId: 'job-cut-1',
        mode: 'FrameAccurate',
        startSeconds: 5.5,
        endSeconds: 20.5,
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })
      vi.spyOn(cutApi, 'getCutJob').mockResolvedValue({
        jobId: 'job-cut-1',
        mode: 'FrameAccurate',
        startSeconds: 5.5,
        endSeconds: 20.5,
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })

      render(<CutPanel video={mockReadyVideo} />)

      fireEvent.change(screen.getByTestId('cut-start-input'), { target: { value: '00:05.5' } })
      fireEvent.change(screen.getByTestId('cut-end-input'), { target: { value: '00:20.5' } })
      fireEvent.click(screen.getByTestId('cut-mode-accurate'))

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-cut-btn'))
      })

      expect(startCutSpy).toHaveBeenCalledTimes(1)
      expect(startCutSpy).toHaveBeenCalledWith(
        'vid-cut-123',
        { startSeconds: 5.5, endSeconds: 20.5, mode: 'FrameAccurate' },
        expect.any(AbortSignal)
      )
    })
  })

  describe('polling progress and terminal states', () => {
    it('updates UI through Pending -> Processing -> Ready and renders download link', async () => {
      vi.spyOn(cutApi, 'startCut').mockResolvedValue({
        jobId: 'job-poll-1',
        mode: 'FastCopy',
        startSeconds: 0,
        endSeconds: 15,
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })

      vi.spyOn(cutApi, 'getCutJob')
        .mockResolvedValueOnce({
          jobId: 'job-poll-1',
          mode: 'FastCopy',
          startSeconds: 0,
          endSeconds: 15,
          status: 'Pending',
          progressPercent: 0,
          error: null,
          outputFileName: null,
          outputDurationSeconds: null,
          outputSizeBytes: null,
        })
        .mockResolvedValueOnce({
          jobId: 'job-poll-1',
          mode: 'FastCopy',
          startSeconds: 0,
          endSeconds: 15,
          status: 'Processing',
          progressPercent: 64,
          error: null,
          outputFileName: null,
          outputDurationSeconds: null,
          outputSizeBytes: null,
        })
        .mockResolvedValueOnce({
          jobId: 'job-poll-1',
          mode: 'FastCopy',
          startSeconds: 0,
          endSeconds: 15,
          status: 'Ready',
          progressPercent: 100,
          error: null,
          outputFileName: 'miku_live_cut.mp4',
          outputDurationSeconds: 15,
          outputSizeBytes: 3 * 1024 * 1024,
        })

      render(<CutPanel video={mockReadyVideo} />)

      fireEvent.change(screen.getByTestId('cut-end-input'), { target: { value: '00:15.0' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-cut-btn'))
      })

      // Pending
      expect(screen.getByTestId('cut-job-status')).toHaveTextContent(/pending/i)
      expect(screen.getByTestId('cut-progress-percent')).toHaveTextContent('0%')

      // Processing
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
      expect(screen.getByTestId('cut-job-status')).toHaveTextContent(/processing/i)
      expect(screen.getByTestId('cut-progress-percent')).toHaveTextContent('64%')
      expect(screen.getByTestId('cut-progress-bar')).toHaveAttribute('aria-valuenow', '64')

      // Ready
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
      expect(screen.getByTestId('cut-results')).toBeInTheDocument()
      expect(screen.getByTestId('cut-output-filename')).toHaveTextContent('miku_live_cut.mp4')
      expect(screen.getByTestId('cut-output-duration')).toHaveTextContent('00:15.0 (15.0s)')
      expect(screen.getByTestId('cut-output-size')).toHaveTextContent('3MB')

      const downloadLink = screen.getByTestId('cut-download-link')
      expect(downloadLink).toHaveAttribute('href', expect.stringMatching(/\/api\/cut-jobs\/job-poll-1\/download$/))
      expect(downloadLink).toHaveAttribute('download', 'miku_live_cut.mp4')
    })

    it('updates UI through Pending -> Failed and displays error message', async () => {
      vi.spyOn(cutApi, 'startCut').mockResolvedValue({
        jobId: 'job-fail-1',
        mode: 'FastCopy',
        startSeconds: 0,
        endSeconds: 10,
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })

      vi.spyOn(cutApi, 'getCutJob')
        .mockResolvedValueOnce({
          jobId: 'job-fail-1',
          mode: 'FastCopy',
          startSeconds: 0,
          endSeconds: 10,
          status: 'Pending',
          progressPercent: 0,
          error: null,
          outputFileName: null,
          outputDurationSeconds: null,
          outputSizeBytes: null,
        })
        .mockResolvedValueOnce({
          jobId: 'job-fail-1',
          mode: 'FastCopy',
          startSeconds: 0,
          endSeconds: 10,
          status: 'Failed',
          progressPercent: null,
          error: 'Corrupted stream near 00:04',
          outputFileName: null,
          outputDurationSeconds: null,
          outputSizeBytes: null,
        })

      render(<CutPanel video={mockReadyVideo} />)

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-cut-btn'))
      })
      expect(screen.getByTestId('cut-job-status')).toHaveTextContent(/pending/i)

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
      expect(screen.getByTestId('cut-error')).toBeInTheDocument()
      expect(screen.getByTestId('cut-error-message')).toHaveTextContent('Corrupted stream near 00:04')
    })
  })
})
