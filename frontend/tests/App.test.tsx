import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, afterEach } from 'vitest'
import App from '../src/App'

const videoUpload = {
  maxFileSizeBytes: 2 * 1024 * 1024,
  allowedExtensions: ['.mkv'],
  allowedContentTypes: ['video/x-matroska'],
}

function okResponse() {
  return { ok: true, status: 200, json: () => Promise.resolve({ videoUpload }) }
}

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders header, video uploader dropzone, and footer', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(okResponse()))

    render(<App />)

    expect(screen.getByText('dinospliceru')).toBeInTheDocument()
    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()
    expect(screen.getByText('Tiesiog UAB')).toBeInTheDocument()
  })

  it('shows a loading message until the config has loaded', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(okResponse()))

    render(<App />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading! ( ๑>ᴗ<๑ )')
    expect(screen.queryByRole('region', { name: /video uploader/i })).not.toBeInTheDocument()
    await screen.findByRole('region', { name: /video uploader/i })
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('reads upload limits from /api/config and applies them to the uploader', async () => {
    const fetchMock = vi.fn().mockResolvedValue(okResponse())
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByText(/accepted: \.mkv · max size: 2mb/i)).toBeInTheDocument()
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/config$/)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'clip.mp4', { type: 'video/mp4' })] } })

    expect(screen.getByText(/invalid file type\. accepted: \.mkv/i)).toBeInTheDocument()
  })

  it('shows an error with a retry button when the config fails to load, and recovers on retry', async () => {
    const fetchMock = vi.fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce(okResponse())
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn't load upload settings/i)
    expect(screen.queryByRole('region', { name: /video uploader/i })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Retry ( -_ -)' }))

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})
