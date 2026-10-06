import { describe, it, expect, vi, afterEach } from 'vitest'
import { fetchConfig } from '../../src/api/configApi'

function mockFetchResponse(body: unknown, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const validConfig = {
  videoUpload: {
    maxFileSizeBytes: 2048,
    allowedExtensions: ['.mkv'],
    allowedContentTypes: ['video/x-matroska'],
  },
}

describe('fetchConfig', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests /api/config and returns the parsed config', async () => {
    const fetchMock = mockFetchResponse(validConfig)

    const config = await fetchConfig()

    expect(fetchMock).toHaveBeenCalledOnce()
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/config$/)
    expect(config).toEqual(validConfig)
  })

  it('throws when the server responds with an error status', async () => {
    mockFetchResponse({}, 500)

    await expect(fetchConfig()).rejects.toThrow(/500/)
  })

  it('throws when the response does not have the expected shape', async () => {
    mockFetchResponse({ maxFileSizeBytes: 2048 })

    await expect(fetchConfig()).rejects.toThrow(/unexpected response shape/)
  })

  it('throws when the request itself fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(fetchConfig()).rejects.toThrow('Failed to fetch')
  })
})
