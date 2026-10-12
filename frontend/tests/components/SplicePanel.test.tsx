import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import SplicePanel from '../../src/components/SplicePanel'
import type { VideoResponse } from '../../src/api/videoApi'
import * as spliceApi from '../../src/api/spliceApi'

const mockReadyVideo: VideoResponse = {
  id: 'vid-ready-123',
  fileName: 'nature_clip.mp4',
  contentType: 'video/mp4',
  sizeBytes: 20 * 1024 * 1024, // 20 MB
  durationSeconds: 60, // 60 seconds
  resolution: { width: 1920, height: 1080 },
  codec: 'h264',
  status: 'Ready',
  createdAt: '2026-10-12T10:00:00Z',
}

describe('SplicePanel', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.clearAllTimers()
    vi.useRealTimers()
  })

  describe('mode and value form validation', () => {
    it('renders mode buttons with ByLength selected by default and displays bounds', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      expect(screen.getByTestId('mode-by-length')).toHaveAttribute('aria-checked', 'true')
      expect(screen.getByTestId('mode-by-size')).toHaveAttribute('aria-checked', 'false')
      expect(screen.getByTestId('unit-seconds')).toBeInTheDocument()
      expect(screen.getByTestId('unit-mmss')).toBeInTheDocument()
      expect(screen.getByTestId('splice-helper-bounds')).toHaveTextContent(/Min: 1s · Max: 60.0s/i)
    })

    it('switches to BySize mode and shows size units with bounds', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('mode-by-size'))

      expect(screen.getByTestId('mode-by-size')).toHaveAttribute('aria-checked', 'true')
      expect(screen.getByTestId('mode-by-length')).toHaveAttribute('aria-checked', 'false')
      expect(screen.getByTestId('unit-mb')).toBeInTheDocument()
      expect(screen.getByTestId('unit-kb')).toBeInTheDocument()
      expect(screen.getByTestId('unit-b')).toBeInTheDocument()
      expect(screen.getByTestId('splice-helper-bounds')).toHaveTextContent(/Min: 1MB · Max: 20MB/i)
    })

    it('validates required non-empty input', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('start-splice-btn'))

      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Please enter a splice segment value/i
      )
    })

    it('validates ByLength minimum segment length (at least 1s)', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '0.5' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))

      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Segment length must be at least 1 second/i
      )
    })

    it('validates ByLength maximum segment length (cannot exceed video duration)', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '75' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))

      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Segment length cannot exceed source video duration/i
      )
    })

    it('validates ByLength mm:ss unit format and boundaries', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('unit-mmss'))
      const input = screen.getByTestId('splice-value-input')

      // Invalid format
      fireEvent.change(input, { target: { value: 'invalid-time' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))
      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Invalid time format\. Please use mm:ss/i
      )

      // Time exceeding duration: 02:00 = 120s > 60s
      fireEvent.change(input, { target: { value: '02:00' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))
      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Segment length cannot exceed source video duration/i
      )
    })

    it('validates BySize minimum size (at least 1 MiB)', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('mode-by-size'))
      const input = screen.getByTestId('splice-value-input')

      // 0.5 MB is under 1 MiB
      fireEvent.change(input, { target: { value: '0.5' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))

      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Segment size must be at least 1 MiB/i
      )
    })

    it('validates BySize maximum size (cannot exceed source video size)', () => {
      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('mode-by-size'))
      const input = screen.getByTestId('splice-value-input')

      // 25 MB exceeds 20 MB source
      fireEvent.change(input, { target: { value: '25' } })
      fireEvent.click(screen.getByTestId('start-splice-btn'))

      expect(screen.getByTestId('splice-validation-error')).toHaveTextContent(
        /Segment size cannot exceed source video size/i
      )
    })
  })

  describe('trigger call with correct payload', () => {
    it('triggers POST with ByLength in seconds', async () => {
      const createSpy = vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
        jobId: 'job-length-1',
        status: 'Pending',
      })
      vi.spyOn(spliceApi, 'getSpliceJob').mockImplementation(() => new Promise(() => {}))

      render(<SplicePanel video={mockReadyVideo} />)

      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '20' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-splice-btn'))
      })

      expect(createSpy).toHaveBeenCalledTimes(1)
      expect(createSpy).toHaveBeenCalledWith(
        'vid-ready-123',
        { mode: 'ByLength', value: 20 },
        expect.any(Object)
      )
      expect(screen.getByTestId('splice-progress')).toBeInTheDocument()
    })

    it('converts mm:ss input to seconds and triggers POST', async () => {
      const createSpy = vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
        jobId: 'job-mmss-1',
        status: 'Pending',
      })
      vi.spyOn(spliceApi, 'getSpliceJob').mockImplementation(() => new Promise(() => {}))

      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('unit-mmss'))
      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '00:45' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-splice-btn'))
      })

      expect(createSpy).toHaveBeenCalledWith(
        'vid-ready-123',
        { mode: 'ByLength', value: 45 },
        expect.any(Object)
      )
    })

    it('converts MB input to bytes and triggers POST', async () => {
      const createSpy = vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
        jobId: 'job-size-1',
        status: 'Pending',
      })
      vi.spyOn(spliceApi, 'getSpliceJob').mockImplementation(() => new Promise(() => {}))

      render(<SplicePanel video={mockReadyVideo} />)

      fireEvent.click(screen.getByTestId('mode-by-size'))
      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '5' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-splice-btn'))
      })

      expect(createSpy).toHaveBeenCalledWith(
        'vid-ready-123',
        { mode: 'BySize', value: 5 * 1024 * 1024 },
        expect.any(Object)
      )
    })
  })

  describe('polling updates UI through Pending → Ready/Failed', () => {
    it('updates UI through Pending -> Processing -> Ready and displays segments', async () => {
      vi.spyOn(spliceApi, 'createSpliceJob').mockResolvedValue({
        jobId: 'job-poll-test',
        status: 'Pending',
      })

      const getSpy = vi.spyOn(spliceApi, 'getSpliceJob')
        .mockResolvedValueOnce({
          id: 'job-poll-test',
          status: 'Pending',
          segments: [],
        })
        .mockResolvedValueOnce({
          id: 'job-poll-test',
          status: 'Processing',
          segments: [],
        })
        .mockResolvedValueOnce({
          id: 'job-poll-test',
          status: 'Ready',
          segments: [
            { name: 'nature_clip_part1.mp4', duration: 30, size: 10485760 },
            { name: 'nature_clip_part2.mp4', duration: 30, size: 10485760 },
          ],
        })

      render(<SplicePanel video={mockReadyVideo} />)

      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '30' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-splice-btn'))
      })

      // State is Pending
      expect(screen.getByTestId('splice-job-status')).toHaveTextContent(/Pending/i)
      expect(getSpy).toHaveBeenCalledTimes(1)

      // Advance 2s: becomes Processing
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000)
      })
      expect(screen.getByTestId('splice-job-status')).toHaveTextContent(/Processing/i)
      expect(getSpy).toHaveBeenCalledTimes(2)

      // Advance 2s: becomes Ready
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000)
      })
      expect(screen.getByTestId('splice-results')).toBeInTheDocument()
      expect(screen.getByTestId('splice-ready-badge')).toHaveTextContent('Generated 2 segments')

      // Verify segment items
      const segmentNames = screen.getAllByTestId('segment-name')
      expect(segmentNames[0]).toHaveTextContent('nature_clip_part1.mp4')
      expect(segmentNames[1]).toHaveTextContent('nature_clip_part2.mp4')

      const segmentDurations = screen.getAllByTestId('segment-duration')
      expect(segmentDurations[0]).toHaveTextContent('0:30 (30.0s)')

      const segmentSizes = screen.getAllByTestId('segment-size')
      expect(segmentSizes[0]).toHaveTextContent('10MB')

      // Splice again button resets back to parameter form for same video
      fireEvent.click(screen.getByTestId('splice-again-btn'))
      expect(screen.getByTestId('splice-form')).toBeInTheDocument()
      expect(screen.getByTestId('mode-by-length')).toBeInTheDocument()
    })

    it('updates UI to Failed state and allows Retry', async () => {
      const createSpy = vi.spyOn(spliceApi, 'createSpliceJob')
        .mockResolvedValueOnce({
          jobId: 'job-fail-test',
          status: 'Pending',
        })
        .mockResolvedValueOnce({
          jobId: 'job-fail-test-2',
          status: 'Pending',
        })

      vi.spyOn(spliceApi, 'getSpliceJob')
        .mockResolvedValueOnce({
          id: 'job-fail-test',
          status: 'Failed',
          segments: [],
          error: 'Codec unsupported for fast splitting',
        })
        .mockResolvedValueOnce({
          id: 'job-fail-test-2',
          status: 'Ready',
          segments: [{ name: 'out.mp4', duration: 30, size: 5000000 }],
        })

      render(<SplicePanel video={mockReadyVideo} />)

      const input = screen.getByTestId('splice-value-input')
      fireEvent.change(input, { target: { value: '30' } })

      await act(async () => {
        fireEvent.click(screen.getByTestId('start-splice-btn'))
      })

      expect(screen.getByTestId('splice-error-message')).toHaveTextContent(
        'Codec unsupported for fast splitting'
      )

      // Click Retry button
      await act(async () => {
        fireEvent.click(screen.getByTestId('splice-retry-btn'))
      })

      expect(createSpy).toHaveBeenCalledTimes(2)
      expect(screen.getByTestId('splice-results')).toBeInTheDocument()
    })
  })
})
