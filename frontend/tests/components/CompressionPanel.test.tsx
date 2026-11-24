import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import CompressionPanel from '../../src/components/CompressionPanel'
import type { VideoResponse } from '../../src/api/videoApi'
import * as compressionApi from '../../src/api/compressionApi'

const mockReadyVideo: VideoResponse = {
  id: 'vid-comp-123',
  fileName: 'miku_concert.mp4',
  contentType: 'video/mp4',
  sizeBytes: 20 * 1024 * 1024,
  durationSeconds: 90,
  resolution: { width: 1920, height: 1080 },
  codec: 'h264',
  status: 'Ready',
  createdAt: '2026-11-23T12:00:00Z',
}

describe('CompressionPanel', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
    vi.useRealTimers()
  })

  describe('target size MB mode and advanced settings drawer', () => {
    it('keeps advanced settings collapsed by default and sends targetSizeBytes when a MB target is set', async () => {
      const startSpy = vi.spyOn(compressionApi, 'startCompression').mockResolvedValue({
        jobId: 'job-target-8mb',
        format: 'Mp4',
        videoCodec: 'H264',
        crf: 23,
        targetWidth: null,
        targetHeight: null,
        audioBitrateKbps: 128,
        targetSizeBytes: 8 * 1024 * 1024,
        preset: 'Medium',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })
      vi.spyOn(compressionApi, 'getCompressionJob').mockResolvedValue({
        jobId: 'job-target-8mb',
        format: 'Mp4',
        videoCodec: 'H264',
        crf: 23,
        targetWidth: null,
        targetHeight: null,
        audioBitrateKbps: 128,
        targetSizeBytes: 8 * 1024 * 1024,
        preset: 'Medium',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })

      render(<CompressionPanel video={mockReadyVideo} />)

      expect(screen.queryByTestId('compression-advanced-settings')).not.toBeInTheDocument()
      expect(screen.getByTestId('target-chip-25mb')).toBeDisabled()
      expect(screen.getByTestId('target-chip-8mb')).toBeEnabled()

      fireEvent.click(screen.getByTestId('target-chip-8mb'))
      expect(screen.getByTestId('target-size-input')).toHaveValue('8')
      expect(screen.getByTestId('compression-active-summary')).toHaveTextContent(/2-Pass Target: 8MB/)

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-compression-btn'))
      })

      expect(startSpy).toHaveBeenCalledWith(
        'vid-comp-123',
        expect.objectContaining({
          targetSizeBytes: 8 * 1024 * 1024,
        }),
        expect.any(AbortSignal)
      )
    })
  })

  describe('form validation and format/codec pairing', () => {
    it('auto-selects compatible codec when switching between MP4 and WebM and clamps CRF', () => {
      render(<CompressionPanel video={mockReadyVideo} />)
      fireEvent.click(screen.getByTestId('toggle-advanced-btn'))

      expect(screen.getByTestId('selected-codec-info')).toHaveTextContent('Active codec: H264 (CRF 0–51)')

      fireEvent.click(screen.getByTestId('format-webm'))
      expect(screen.getByTestId('selected-codec-info')).toHaveTextContent('Active codec: Vp9 (CRF 0–63)')

      fireEvent.change(screen.getByTestId('crf-input'), { target: { value: '60' } })
      expect(screen.getByTestId('start-compression-btn')).not.toBeDisabled()

      // Switching back to MP4 auto-selects H264 and clamps CRF to 51
      fireEvent.click(screen.getByTestId('format-mp4'))
      expect(screen.getByTestId('selected-codec-info')).toHaveTextContent('Active codec: H264 (CRF 0–51)')
      expect(screen.getByTestId('crf-input')).toHaveValue('51')
      expect(screen.getByTestId('resolution-720p')).toHaveTextContent('Downscale to 1280x720')
      expect(screen.getByTestId('preset-hint')).toHaveTextContent('Balanced')
      expect(screen.getByTestId('audio-bitrate-hint')).toHaveTextContent('Standard')
    })

    it('blocks submission on odd custom dimensions and out-of-range CRF', () => {
      const startSpy = vi.spyOn(compressionApi, 'startCompression')
      render(<CompressionPanel video={mockReadyVideo} />)
      fireEvent.click(screen.getByTestId('toggle-advanced-btn'))

      fireEvent.click(screen.getByTestId('resolution-custom'))
      fireEvent.change(screen.getByTestId('custom-width-input'), { target: { value: '1281' } })
      fireEvent.change(screen.getByTestId('custom-height-input'), { target: { value: '720' } })

      expect(screen.getByTestId('compression-validation-error')).toHaveTextContent(/even number/i)
      expect(screen.getByTestId('start-compression-btn')).toBeDisabled()

      fireEvent.change(screen.getByTestId('custom-width-input'), { target: { value: '1280' } })
      fireEvent.change(screen.getByTestId('crf-input'), { target: { value: '55' } })

      expect(screen.getByTestId('compression-validation-error')).toHaveTextContent(/between 0 and 51/i)
      expect(screen.getByTestId('start-compression-btn')).toBeDisabled()

      fireEvent.submit(screen.getByTestId('compression-form'))
      expect(startSpy).not.toHaveBeenCalled()
    })
  })

  describe('submitting compression request', () => {
    it('triggers POST /api/videos/{id}/compress with expected payload', async () => {
      const startSpy = vi.spyOn(compressionApi, 'startCompression').mockResolvedValue({
        jobId: 'job-comp-1',
        format: 'Webm',
        videoCodec: 'Vp9',
        crf: 32,
        targetWidth: 1280,
        targetHeight: 720,
        audioBitrateKbps: 96,
        preset: 'Fast',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })
      vi.spyOn(compressionApi, 'getCompressionJob').mockResolvedValue({
        jobId: 'job-comp-1',
        format: 'Webm',
        videoCodec: 'Vp9',
        crf: 32,
        targetWidth: 1280,
        targetHeight: 720,
        audioBitrateKbps: 96,
        preset: 'Fast',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      })

      render(<CompressionPanel video={mockReadyVideo} />)
      fireEvent.click(screen.getByTestId('toggle-advanced-btn'))

      fireEvent.click(screen.getByTestId('format-webm'))
      fireEvent.click(screen.getByTestId('resolution-720p'))
      fireEvent.change(screen.getByTestId('crf-input'), { target: { value: '32' } })
      fireEvent.change(screen.getByTestId('preset-select'), { target: { value: 'Fast' } })
      fireEvent.change(screen.getByTestId('audio-bitrate-select'), { target: { value: '96' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-compression-btn'))
      })

      expect(startSpy).toHaveBeenCalledWith(
        'vid-comp-123',
        {
          format: 'Webm',
          videoCodec: 'Vp9',
          crf: 32,
          targetWidth: 1280,
          targetHeight: 720,
          audioBitrateKbps: 96,
          targetSizeBytes: null,
          preset: 'Fast',
        },
        expect.any(AbortSignal)
      )
    })
  })

  describe('polling progress and terminal states', () => {
    it('updates progress bar and renders size savings + download link on Ready', async () => {
      const pendingJob: compressionApi.CompressionJobResponse = {
        jobId: 'job-comp-ready',
        format: 'Mp4',
        videoCodec: 'H264',
        crf: 23,
        targetWidth: null,
        targetHeight: null,
        audioBitrateKbps: 128,
        preset: 'Medium',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      }

      vi.spyOn(compressionApi, 'startCompression').mockResolvedValue(pendingJob)
      vi.spyOn(compressionApi, 'getCompressionJob')
        .mockResolvedValueOnce(pendingJob)
        .mockResolvedValueOnce({ ...pendingJob, status: 'Processing', progressPercent: 72 })
        .mockResolvedValueOnce({
          ...pendingJob,
          status: 'Ready',
          progressPercent: 100,
          outputFileName: 'miku_concert_compressed.mp4',
          outputDurationSeconds: 90,
          outputSizeBytes: 5 * 1024 * 1024,
        })

      render(<CompressionPanel video={mockReadyVideo} />)

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-compression-btn'))
      })
      expect(screen.getByTestId('compression-job-status')).toHaveTextContent(/pending/i)

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
      expect(screen.getByTestId('compression-job-status')).toHaveTextContent(/processing/i)
      expect(screen.getByTestId('compression-progress-percent')).toHaveTextContent('72%')
      expect(screen.getByTestId('compression-progress-bar')).toHaveAttribute('aria-valuenow', '72')

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
      expect(screen.getByTestId('compression-results')).toBeInTheDocument()
      expect(screen.getByTestId('compression-output-filename')).toHaveTextContent('miku_concert_compressed.mp4')
      expect(screen.getByTestId('compression-output-duration')).toHaveTextContent('01:30.0 (90.0s)')
      expect(screen.getByTestId('compression-output-size')).toHaveTextContent('5MB vs 20MB')
      expect(screen.getByTestId('compression-size-savings')).toHaveTextContent('75% saved (15MB)')

      const downloadLink = screen.getByTestId('compression-download-link')
      expect(downloadLink).toHaveAttribute(
        'href',
        expect.stringMatching(/\/api\/compression-jobs\/job-comp-ready\/download$/)
      )
      expect(downloadLink).toHaveAttribute('download', 'miku_concert_compressed.mp4')
    })

    it('renders server error banner on Failed', async () => {
      const pendingJob: compressionApi.CompressionJobResponse = {
        jobId: 'job-comp-fail',
        format: 'Mp4',
        videoCodec: 'H264',
        crf: 23,
        targetWidth: null,
        targetHeight: null,
        audioBitrateKbps: 128,
        preset: 'Medium',
        status: 'Pending',
        progressPercent: 0,
        error: null,
        outputFileName: null,
        outputDurationSeconds: null,
        outputSizeBytes: null,
      }

      vi.spyOn(compressionApi, 'startCompression').mockResolvedValue(pendingJob)
      vi.spyOn(compressionApi, 'getCompressionJob').mockResolvedValue({
        ...pendingJob,
        status: 'Failed',
        progressPercent: null,
        error: 'libx264 encoding error',
      })

      render(<CompressionPanel video={mockReadyVideo} />)

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-compression-btn'))
      })

      expect(screen.getByTestId('compression-error')).toBeInTheDocument()
      expect(screen.getByTestId('compression-error-message')).toHaveTextContent('libx264 encoding error')
    })
  })
})
