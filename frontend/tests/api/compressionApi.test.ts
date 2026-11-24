import { describe, it, expect, vi, afterEach } from 'vitest'
import {
  startCompression,
  getCompressionJob,
  deleteCompressionJob,
  getCompressionDownloadUrl,
  type CompressionJobResponse,
  type CompressionRequest,
} from '../../src/api/compressionApi'

const sampleJob: CompressionJobResponse = {
  jobId: '11111111-2222-3333-4444-555555555555',
  format: 'Mp4',
  videoCodec: 'H264',
  crf: 23,
  targetWidth: 1280,
  targetHeight: 720,
  audioBitrateKbps: 128,
  targetSizeBytes: null,
  preset: 'Medium',
  status: 'Pending',
  progressPercent: 0,
  error: null,
  outputFileName: null,
  outputDurationSeconds: null,
  outputSizeBytes: null,
}

describe('compressionApi', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('startCompression sends POST /api/videos/{id}/compress with payload', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 202,
      json: () => Promise.resolve(sampleJob),
    })
    vi.stubGlobal('fetch', fetchMock)

    const request: CompressionRequest = {
      format: 'Mp4',
      videoCodec: 'H264',
      crf: 23,
      targetWidth: 1280,
      targetHeight: 720,
      audioBitrateKbps: 128,
      preset: 'Medium',
    }

    const result = await startCompression('vid-1', request)
    expect(result).toEqual(sampleJob)
    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/api\/videos\/vid-1\/compress$/)
    expect(options.method).toBe('POST')
    expect(JSON.parse(options.body)).toEqual(request)
  })

  it('startCompression throws backend error message on 400', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'CRF must be between 0 and 51.', code: 'INVALID_COMPRESSION_CRF' }),
      })
    )

    await expect(
      startCompression('vid-1', {
        format: 'Mp4',
        videoCodec: 'H264',
        crf: 99,
        targetWidth: null,
        targetHeight: null,
        audioBitrateKbps: null,
        preset: 'Medium',
      })
    ).rejects.toThrow('CRF must be between 0 and 51.')
  })

  it('getCompressionJob fetches GET /api/compression-jobs/{id}', async () => {
    const readyJob: CompressionJobResponse = {
      ...sampleJob,
      status: 'Ready',
      progressPercent: 100,
      outputFileName: 'clip_compressed.mp4',
      outputDurationSeconds: 30,
      outputSizeBytes: 5_000_000,
    }
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(readyJob),
    })
    vi.stubGlobal('fetch', fetchMock)

    const result = await getCompressionJob(sampleJob.jobId)
    expect(result).toEqual(readyJob)
    expect(fetchMock.mock.calls[0][0]).toMatch(new RegExp(`/api/compression-jobs/${sampleJob.jobId}$`))
  })

  it('deleteCompressionJob sends DELETE /api/compression-jobs/{id}', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204 })
    vi.stubGlobal('fetch', fetchMock)

    await expect(deleteCompressionJob('job-42')).resolves.toBeUndefined()
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/compression-jobs\/job-42$/)
    expect(fetchMock.mock.calls[0][1].method).toBe('DELETE')
  })

  it('getCompressionDownloadUrl builds encoded download URL', () => {
    expect(getCompressionDownloadUrl('job-42')).toMatch(/\/api\/compression-jobs\/job-42\/download$/)
    expect(getCompressionDownloadUrl('a/b c')).toMatch(/\/api\/compression-jobs\/a%2Fb%20c\/download$/)
  })
})
