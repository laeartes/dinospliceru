import { describe, it, expect, vi, afterEach } from 'vitest'
import { uploadVideo, getVideoStatus, type VideoResponse } from '../../src/api/videoApi'

class MockXMLHttpRequest {
  static instances: MockXMLHttpRequest[] = []
  url = ''
  method = ''
  status = 200
  responseText = ''
  upload = {
    onprogress: null as ((event: ProgressEvent) => void) | null,
  }
  onload: (() => void) | null = null
  onerror: (() => void) | null = null
  ontimeout: (() => void) | null = null
  sentData: unknown = null
  aborted = false
  eventListeners: Record<string, ((event: Event) => void)[]> = {}

  constructor() {
    MockXMLHttpRequest.instances.push(this)
  }

  open(method: string, url: string) {
    this.method = method
    this.url = url
  }

  addEventListener(event: string, callback: (event: Event) => void) {
    this.eventListeners[event] = this.eventListeners[event] || []
    this.eventListeners[event].push(callback)
  }

  removeEventListener(event: string, callback: (event: Event) => void) {
    if (this.eventListeners[event]) {
      this.eventListeners[event] = this.eventListeners[event].filter((cb) => cb !== callback)
    }
  }

  dispatchEvent(event: Event) {
    const list = this.eventListeners[event.type] || []
    for (const cb of list) {
      cb(event)
    }
  }

  abort() {
    this.aborted = true
  }

  send(data: unknown) {
    this.sentData = data
  }

  simulateProgress(loaded: number, total: number) {
    if (this.upload.onprogress) {
      this.upload.onprogress({
        lengthComputable: true,
        loaded,
        total,
      } as unknown as ProgressEvent)
    }
  }

  simulateSuccess(status: number, body: unknown) {
    this.status = status
    this.responseText = JSON.stringify(body)
    this.onload?.()
    this.dispatchEvent(new Event('loadend'))
  }

  simulateError(status: number, body?: unknown) {
    this.status = status
    this.responseText = body ? JSON.stringify(body) : ''
    this.onload?.()
    this.dispatchEvent(new Event('loadend'))
  }

  simulateNetworkError() {
    this.onerror?.()
    this.dispatchEvent(new Event('loadend'))
  }
}

describe('videoApi', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    MockXMLHttpRequest.instances = []
  })

  describe('uploadVideo', () => {
    it('uploads a file and reports real-time upload progress', async () => {
      vi.stubGlobal('XMLHttpRequest', MockXMLHttpRequest)

      const file = new File(['dummy video data'], 'clip.mp4', { type: 'video/mp4' })
      const progressUpdates: number[] = []

      const uploadPromise = uploadVideo(file, {
        onProgress: (percent) => progressUpdates.push(percent),
      })

      const xhr = MockXMLHttpRequest.instances[0]
      expect(xhr).toBeDefined()
      expect(xhr.method).toBe('POST')
      expect(xhr.url).toMatch(/\/api\/videos$/)

      // Simulate progress events
      xhr.simulateProgress(25, 100)
      xhr.simulateProgress(50, 100)
      xhr.simulateProgress(100, 100)

      expect(progressUpdates).toEqual([25, 50, 100])

      // Complete upload
      xhr.simulateSuccess(201, {
        id: '11111111-2222-3333-4444-555555555555',
        fileName: 'clip.mp4',
        sizeBytes: 16,
        status: 'Uploaded',
      })

      const result = await uploadPromise
      expect(result).toEqual({
        id: '11111111-2222-3333-4444-555555555555',
        fileName: 'clip.mp4',
        sizeBytes: 16,
        status: 'Uploaded',
      })
    })

    it('rejects with server problem detail message on 413 Payload Too Large', async () => {
      vi.stubGlobal('XMLHttpRequest', MockXMLHttpRequest)

      const file = new File(['oversized video'], 'large.mp4', { type: 'video/mp4' })
      const uploadPromise = uploadVideo(file)

      const xhr = MockXMLHttpRequest.instances[0]
      xhr.simulateError(413, {
        title: 'Upload rejected',
        detail: 'The request is too large or malformed. Maximum file size is 1048576 bytes.',
      })

      await expect(uploadPromise).rejects.toThrow(
        'The request is too large or malformed. Maximum file size is 1048576 bytes.'
      )
    })

    it('rejects with network error message on network failure', async () => {
      vi.stubGlobal('XMLHttpRequest', MockXMLHttpRequest)

      const file = new File(['x'], 'test.mp4', { type: 'video/mp4' })
      const uploadPromise = uploadVideo(file)

      const xhr = MockXMLHttpRequest.instances[0]
      xhr.simulateNetworkError()

      await expect(uploadPromise).rejects.toThrow('Network error during upload')
    })

    it('aborts upload when AbortSignal triggers', async () => {
      vi.stubGlobal('XMLHttpRequest', MockXMLHttpRequest)

      const file = new File(['x'], 'test.mp4', { type: 'video/mp4' })
      const controller = new AbortController()

      const uploadPromise = uploadVideo(file, { signal: controller.signal })
      const xhr = MockXMLHttpRequest.instances[0]

      controller.abort()

      expect(xhr.aborted).toBe(true)
      await expect(uploadPromise).rejects.toThrow('Upload aborted')
    })
  })

  describe('getVideoStatus', () => {
    it('fetches status and metadata of a video by id', async () => {
      const mockResponse: VideoResponse = {
        id: '11111111-2222-3333-4444-555555555555',
        fileName: 'clip.mp4',
        contentType: 'video/mp4',
        sizeBytes: 1024,
        durationSeconds: 12.5,
        resolution: { width: 1920, height: 1080 },
        codec: 'h264',
        status: 'Ready',
        createdAt: '2026-10-11T12:00:00Z',
      }

      const fetchMock = vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockResponse),
      })
      vi.stubGlobal('fetch', fetchMock)

      const result = await getVideoStatus('11111111-2222-3333-4444-555555555555')
      expect(result).toEqual(mockResponse)
      expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/videos\/11111111-2222-3333-4444-555555555555$/)
    })

    it('throws error with problem detail when video is not found (404)', async () => {
      const fetchMock = vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ title: 'Video not found', detail: 'No video with id found' }),
      })
      vi.stubGlobal('fetch', fetchMock)

      await expect(getVideoStatus('unknown-id')).rejects.toThrow('No video with id found')
    })
  })
})
