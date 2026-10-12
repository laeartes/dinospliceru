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

  it('renders Ready overview with Editor and Splicer buttons, and preserves Splicer inputs across navigation', async () => {
    class MockXHR {
      upload = { onprogress: null }
      status = 201
      responseText = JSON.stringify({
        id: 'app-vid-1',
        fileName: 'test.mkv',
        sizeBytes: 1024,
        status: 'Uploaded',
      })
      onload: (() => void) | null = null
      open() {}
      send() {
        setTimeout(() => this.onload?.(), 0)
      }
    }
    vi.stubGlobal('XMLHttpRequest', MockXHR)

    const fetchMock = vi.fn()
      .mockResolvedValueOnce(okResponse())
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () =>
          Promise.resolve({
            id: 'app-vid-1',
            fileName: 'test.mkv',
            contentType: 'video/x-matroska',
            sizeBytes: 1024,
            durationSeconds: 30.0,
            resolution: { width: 1280, height: 720 },
            codec: 'h264',
            status: 'Ready',
            createdAt: '2026-10-12T10:00:00Z',
          }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'test.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    // Overview shows Ready status with Editor and Splicer buttons
    const splicerBtn = await screen.findByTestId('open-splicer-btn')
    const editorBtn = screen.getByTestId('editor-btn')
    expect(editorBtn).toBeDisabled()
    expect(splicerBtn).toBeInTheDocument()

    // Click Splicer button to open Splicer screen
    fireEvent.click(splicerBtn)

    expect(screen.getByTestId('splice-panel')).toBeInTheDocument()
    expect(screen.getByTestId('mode-by-length')).toBeInTheDocument()

    // Enter a value in Splicer screen
    const valueInput = screen.getByTestId('splice-value-input') as HTMLInputElement
    fireEvent.change(valueInput, { target: { value: '25' } })
    expect(valueInput.value).toBe('25')

    // Click Back button to return to overview
    const backBtn = screen.getByTestId('splice-back-btn')
    fireEvent.click(backBtn)

    // Back on overview screen: Splicer button is visible again
    expect(screen.getByTestId('open-splicer-btn')).toBeInTheDocument()

    // Re-enter Splicer screen: previous input should be preserved!
    fireEvent.click(screen.getByTestId('open-splicer-btn'))
    const reopenedInput = screen.getByTestId('splice-value-input') as HTMLInputElement
    expect(reopenedInput.value).toBe('25')
  })

  it('displays processing indicator on Splicer button when splicing is running in background', async () => {
    class MockXHR {
      upload = { onprogress: null }
      status = 201
      responseText = JSON.stringify({
        id: 'app-bg-vid',
        fileName: 'test.mkv',
        sizeBytes: 1024,
        status: 'Uploaded',
      })
      onload: (() => void) | null = null
      open() {}
      send() {
        setTimeout(() => this.onload?.(), 0)
      }
    }
    vi.stubGlobal('XMLHttpRequest', MockXHR)

    const fetchMock = vi.fn()
      .mockResolvedValueOnce(okResponse())
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () =>
          Promise.resolve({
            id: 'app-bg-vid',
            fileName: 'test.mkv',
            contentType: 'video/x-matroska',
            sizeBytes: 1024,
            durationSeconds: 30.0,
            resolution: { width: 1280, height: 720 },
            codec: 'h264',
            status: 'Ready',
            createdAt: '2026-10-12T10:00:00Z',
          }),
      })
      // createSpliceJob POST
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ jobId: 'bg-job-1', status: 'Pending' }),
      })
      // getSpliceJob GET
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ id: 'bg-job-1', status: 'Processing', segments: [] }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'test.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    const splicerBtn = await screen.findByTestId('open-splicer-btn')
    fireEvent.click(splicerBtn)

    // In Splicer screen, enter value and trigger
    const valueInput = screen.getByTestId('splice-value-input')
    fireEvent.change(valueInput, { target: { value: '10' } })

    fireEvent.click(screen.getByTestId('start-splice-btn'))

    // Click back to overview
    const backBtn = await screen.findByTestId('splice-back-btn')
    fireEvent.click(backBtn)

    // Now on overview screen, the splicer button shows processing badge!
    const overviewSplicerBtn = screen.getByTestId('open-splicer-btn')
    expect(overviewSplicerBtn).toHaveTextContent('Splicer (processing...) ( •̀_•́ ) ✦')
  })
})
