import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import {
  createSpliceJob,
  getSpliceJob,
  downloadSpliceOutput,
  type SpliceRequest,
} from '../../src/api/spliceApi'

describe('spliceApi', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  describe('createSpliceJob', () => {
    it('sends POST request with correct payload and returns job info', async () => {
      const mockResponse = {
        jobId: '11111111-2222-3333-4444-555555555555',
        status: 'Pending',
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 202,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const request: SpliceRequest = {
        mode: 'ByLength',
        value: 45,
      }

      const result = await createSpliceJob('video-123', request)

      expect(result).toEqual({
        jobId: '11111111-2222-3333-4444-555555555555',
        status: 'Pending',
      })
      expect(fetchMock).toHaveBeenCalledTimes(1)
      const [url, options] = fetchMock.mock.calls[0]
      expect(url).toMatch(/\/api\/videos\/video-123\/splice$/)
      expect(options.method).toBe('POST')
      expect(options.headers).toEqual({ 'Content-Type': 'application/json' })
      expect(JSON.parse(options.body)).toEqual({ mode: 'ByLength', value: 45 })
    })

    it('supports response with "id" instead of "jobId"', async () => {
      const mockResponse = {
        id: '22222222-3333-4444-5555-666666666666',
        status: 'Pending',
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 202,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await createSpliceJob('video-456', { mode: 'BySize', value: 2097152 })

      expect(result).toEqual({
        jobId: '22222222-3333-4444-5555-666666666666',
        status: 'Pending',
      })
    })

    it('extracts the error message from a 400 error response', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () =>
          Promise.resolve({
            error: 'Value must be at least 1 seconds.',
            code: 'INVALID_SPLICE_VALUE',
          }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(createSpliceJob('video-123', { mode: 'ByLength', value: 0.5 })).rejects.toThrow(
        'Value must be at least 1 seconds.'
      )
    })

    it('extracts the error message on 409 conflict when video is not ready', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        json: () =>
          Promise.resolve({
            error: 'The video is Processing; it must be Ready before it can be spliced.',
            code: 'VIDEO_NOT_READY',
          }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(createSpliceJob('video-123', { mode: 'ByLength', value: 30 })).rejects.toThrow(
        'The video is Processing; it must be Ready before it can be spliced.'
      )
    })

    it('throws when server returns unexpected response structure', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ unexpected: true }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(createSpliceJob('video-123', { mode: 'ByLength', value: 30 })).rejects.toThrow(
        'Unexpected response format from server'
      )
    })
  })

  describe('getSpliceJob', () => {
    it('fetches status and segments when job is Ready', async () => {
      const mockResponse = {
        id: 'job-abc',
        status: 'Ready',
        segments: [
          { id: 'out-1', name: 'segment_01.mp4', duration: 30, size: 5242880 },
          { id: 'out-2', name: 'segment_02.mp4', duration: 25.5, size: 4194304 },
        ],
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getSpliceJob('job-abc')

      expect(result).toEqual({
        id: 'job-abc',
        status: 'Ready',
        segments: [
          { id: 'out-1', name: 'segment_01.mp4', duration: 30, size: 5242880 },
          { id: 'out-2', name: 'segment_02.mp4', duration: 25.5, size: 4194304 },
        ],
      })
      expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/splice-jobs\/job-abc$/)
    })

    it('maps backend outputs property with fileName/durationSeconds/sizeBytes', async () => {
      const mockResponse = {
        id: 'job-def',
        status: 'Ready',
        outputs: [
          { id: 'out-a', fileName: 'part_001.mp4', durationSeconds: 60, sizeBytes: 10485760 },
        ],
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getSpliceJob('job-def')

      expect(result).toEqual({
        id: 'job-def',
        status: 'Ready',
        segments: [
          { id: 'out-a', name: 'part_001.mp4', duration: 60, size: 10485760 },
        ],
      })
    })

    it('fetches pending/processing status with empty segments list', async () => {
      const mockResponse = {
        id: 'job-processing',
        status: 'Processing',
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getSpliceJob('job-processing')

      expect(result).toEqual({
        id: 'job-processing',
        status: 'Processing',
        segments: [],
      })
    })

    it('handles failed status with error message', async () => {
      const mockResponse = {
        id: 'job-failed',
        status: 'Failed',
        error: 'FFmpeg segmentation failed due to corrupt source packet',
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getSpliceJob('job-failed')

      expect(result).toEqual({
        id: 'job-failed',
        status: 'Failed',
        segments: [],
        error: 'FFmpeg segmentation failed due to corrupt source packet',
      })
    })

    it('throws error with server details when 404 not found occurs', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ error: 'No job with id exists.', code: 'SPLICE_JOB_NOT_FOUND' }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(getSpliceJob('non-existent')).rejects.toThrow('No job with id exists.')
    })
  })

  describe('downloadSpliceOutput', () => {
    class FakeXhr {
      static instances: FakeXhr[] = []
      method = ''
      url = ''
      status = 0
      response: unknown = null
      responseType = ''
      onprogress: ((event: ProgressEvent) => void) | null = null
      onload: (() => void) | null = null
      onerror: (() => void) | null = null
      ontimeout: (() => void) | null = null
      aborted = false

      constructor() {
        FakeXhr.instances.push(this)
      }

      open(method: string, url: string) {
        this.method = method
        this.url = url
      }

      send() {}

      abort() {
        this.aborted = true
      }

      respond(status: number, body: unknown) {
        this.status = status
        this.response = body
        this.onload?.()
      }

      emitProgress(loaded: number, total: number) {
        this.onprogress?.({ lengthComputable: true, loaded, total } as ProgressEvent)
      }
    }

    beforeEach(() => {
      FakeXhr.instances = []
      vi.stubGlobal('XMLHttpRequest', FakeXhr)
    })

    it('downloads the output blob with the segment file name', async () => {
      const createObjectURL = vi.fn().mockReturnValue('blob:mock-url')
      const revokeObjectURL = vi.fn()
      vi.stubGlobal('URL', { createObjectURL, revokeObjectURL })

      const clicked: HTMLAnchorElement[] = []
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: unknown) {
        clicked.push(this as HTMLAnchorElement)
      })

      const promise = downloadSpliceOutput('out-1', 'holiday_part01.mp4')
      const xhr = FakeXhr.instances[0]
      expect(xhr.method).toBe('GET')
      expect(xhr.url).toMatch(/\/api\/splice-outputs\/out-1\/download$/)
      expect(xhr.responseType).toBe('blob')

      const blob = new Blob(['segment bytes'], { type: 'video/mp4' })
      xhr.respond(200, blob)
      await promise

      expect(createObjectURL).toHaveBeenCalledWith(blob)
      expect(clicked).toHaveLength(1)
      expect(clicked[0].download).toBe('holiday_part01.mp4')
      expect(clicked[0].href).toBe('blob:mock-url')
      await vi.waitFor(() => expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url'), {
        timeout: 2000,
      })
    })

    it('reports download progress as the body streams', async () => {
      const createObjectURL = vi.fn().mockReturnValue('blob:mock-url')
      vi.stubGlobal('URL', { createObjectURL, revokeObjectURL: vi.fn() })
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

      const progress: number[] = []
      const promise = downloadSpliceOutput('out-1', 'holiday_part01.mp4', {
        onProgress: (percent) => progress.push(percent),
      })
      const xhr = FakeXhr.instances[0]
      xhr.emitProgress(5, 10)
      xhr.emitProgress(10, 10)
      xhr.respond(200, new Blob(['segment bytes']))
      await promise

      expect(progress).toEqual([50, 100])
      expect(createObjectURL).toHaveBeenCalledTimes(1)
    })

    it('throws the server error when the download request fails', async () => {
      const promise = downloadSpliceOutput('out-1', 'holiday_part01.mp4')
      const xhr = FakeXhr.instances[0]
      xhr.respond(
        409,
        new Blob(
          [
            JSON.stringify({
              error: 'The splice job is Processing; it must be Ready before its outputs can be downloaded.',
              code: 'SPLICE_JOB_NOT_READY',
            }),
          ],
          { type: 'application/json' }
        )
      )

      await expect(promise).rejects.toThrow(
        'The splice job is Processing; it must be Ready before its outputs can be downloaded.'
      )
    })

    it('rejects when the download is aborted', async () => {
      const controller = new AbortController()
      const promise = downloadSpliceOutput('out-1', 'holiday_part01.mp4', {
        signal: controller.signal,
      })
      controller.abort()

      await expect(promise).rejects.toThrow(/aborted/i)
      expect(FakeXhr.instances[0].aborted).toBe(true)
    })
  })
})
