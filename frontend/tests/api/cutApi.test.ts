import { describe, it, expect, vi, afterEach } from 'vitest'
import {
  startCut,
  getCutJob,
  deleteCutJob,
  getCutDownloadUrl,
  type CutJobResponse,
  type CutRequest,
} from '../../src/api/cutApi'

const sampleJob: CutJobResponse = {
  jobId: '11111111-2222-3333-4444-555555555555',
  mode: 'FastCopy',
  startSeconds: 2.5,
  endSeconds: 12.5,
  status: 'Pending',
  progressPercent: 0,
  error: null,
  outputFileName: null,
  outputDurationSeconds: null,
  outputSizeBytes: null,
}

describe('cutApi', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  describe('startCut', () => {
    it('sends POST /api/videos/{id}/cut with startSeconds, endSeconds, and mode', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 202,
        json: () => Promise.resolve(sampleJob),
      })
      vi.stubGlobal('fetch', fetchMock)

      const request: CutRequest = {
        startSeconds: 2.5,
        endSeconds: 12.5,
        mode: 'FastCopy',
      }

      const result = await startCut('video-123', request)

      expect(result).toEqual(sampleJob)
      expect(fetchMock).toHaveBeenCalledTimes(1)
      const [url, options] = fetchMock.mock.calls[0]
      expect(url).toMatch(/\/api\/videos\/video-123\/cut$/)
      expect(options.method).toBe('POST')
      expect(options.headers).toEqual({ 'Content-Type': 'application/json' })
      expect(JSON.parse(options.body)).toEqual(request)
    })

    it('extracts backend error message on 400 response', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () =>
          Promise.resolve({
            error: 'EndSeconds must be greater than StartSeconds.',
            code: 'INVALID_CUT_RANGE',
          }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(
        startCut('video-123', { startSeconds: 10, endSeconds: 5, mode: 'FastCopy' })
      ).rejects.toThrow('EndSeconds must be greater than StartSeconds.')
    })

    it('throws when server returns unexpected response shape', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 202,
        json: () => Promise.resolve({ unexpected: true }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(
        startCut('video-123', { startSeconds: 0, endSeconds: 5, mode: 'FrameAccurate' })
      ).rejects.toThrow('Unexpected response format from server')
    })
  })

  describe('getCutJob', () => {
    it('fetches GET /api/cut-jobs/{id} and returns normalized CutJobResponse', async () => {
      const readyJob: CutJobResponse = {
        ...sampleJob,
        status: 'Ready',
        progressPercent: 100,
        outputFileName: 'clip_cut.mp4',
        outputDurationSeconds: 10.0,
        outputSizeBytes: 2048000,
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(readyJob),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getCutJob(sampleJob.jobId)

      expect(result).toEqual(readyJob)
      const [url, options] = fetchMock.mock.calls[0]
      expect(url).toMatch(new RegExp(`/api/cut-jobs/${sampleJob.jobId}$`))
      expect(options.method).toBe('GET')
    })

    it('throws API error on 404 not found', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: () =>
          Promise.resolve({
            error: 'No cut job exists.',
            code: 'CUT_JOB_NOT_FOUND',
          }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(getCutJob('missing-id')).rejects.toThrow('No cut job exists.')
    })
  })

  describe('deleteCutJob', () => {
    it('sends DELETE /api/cut-jobs/{id} and resolves on 204', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(deleteCutJob('job-123')).resolves.toBeUndefined()
      const [url, options] = fetchMock.mock.calls[0]
      expect(url).toMatch(/\/api\/cut-jobs\/job-123$/)
      expect(options.method).toBe('DELETE')
    })

    it('throws API error when job is still processing (409)', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        json: () =>
          Promise.resolve({
            error: 'The cut job cannot be deleted while it is being processed.',
            code: 'CUT_JOB_PROCESSING',
          }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(deleteCutJob('job-123')).rejects.toThrow(
        'The cut job cannot be deleted while it is being processed.'
      )
    })
  })

  describe('getCutDownloadUrl', () => {
    it('builds the download URL with encoded job id', () => {
      expect(getCutDownloadUrl('job-123')).toMatch(/\/api\/cut-jobs\/job-123\/download$/)
      expect(getCutDownloadUrl('job/with space')).toMatch(/\/api\/cut-jobs\/job%2Fwith%20space\/download$/)
    })
  })
})
