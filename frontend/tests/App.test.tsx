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

  it('renders Ready overview with workspace tabs and tool buttons, and preserves Splicer inputs across navigation', async () => {
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
    expect(screen.queryByTestId('workspace-tablist')).not.toBeInTheDocument()

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'test.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    // Overview shows Ready status with workspace tabs and Cut/Compress/Splicer buttons
    const splicerBtn = await screen.findByTestId('open-splicer-btn')
    expect(await screen.findByTestId('workspace-tablist')).toBeInTheDocument()
    expect(screen.queryByTestId('editor-btn')).not.toBeInTheDocument()
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

  it('opens CutPanel from Ready overview with local video preview and returns cleanly via Back button', async () => {
    const createObjectUrlSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:local-preview')
    const revokeObjectUrlSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})

    class MockXHR {
      upload = { onprogress: null }
      status = 201
      responseText = JSON.stringify({
        id: 'app-cut-vid',
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
            id: 'app-cut-vid',
            fileName: 'test.mkv',
            contentType: 'video/x-matroska',
            sizeBytes: 1024,
            durationSeconds: 30.0,
            resolution: { width: 1280, height: 720 },
            codec: 'h264',
            status: 'Ready',
            createdAt: '2026-11-21T19:00:00Z',
          }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'test.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    const cutterBtn = await screen.findByTestId('open-cutter-btn')
    fireEvent.click(cutterBtn)

    expect(screen.getByTestId('cut-panel')).toBeInTheDocument()
    expect(screen.getByTestId('cut-video-preview')).toHaveAttribute('src', 'blob:local-preview')
    expect(screen.getByTestId('cut-start-input')).toHaveValue('00:00.0')
    expect(screen.getByTestId('cut-end-input')).toHaveValue('00:30.0')

    fireEvent.click(screen.getByTestId('cut-back-btn'))
    expect(screen.getByTestId('open-cutter-btn')).toBeInTheDocument()

    fireEvent.click(screen.getByTestId('open-compressor-btn'))
    expect(screen.getByTestId('compression-panel')).toBeInTheDocument()

    fireEvent.click(screen.getByTestId('compression-back-btn'))
    expect(screen.getByTestId('open-compressor-btn')).toBeInTheDocument()

    createObjectUrlSpy.mockRestore()
    revokeObjectUrlSpy.mockRestore()
  })

  it('switches cleanly between Overview, Cutter, Compressor, and Splicer via workspace tabs while preserving all panel states', async () => {
    class MockXHR {
      upload = { onprogress: null }
      status = 201
      responseText = JSON.stringify({
        id: 'app-nav-vid',
        fileName: 'workspace.mkv',
        sizeBytes: 2048,
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
            id: 'app-nav-vid',
            fileName: 'workspace.mkv',
            contentType: 'video/x-matroska',
            sizeBytes: 2048,
            durationSeconds: 45.0,
            resolution: { width: 1920, height: 1080 },
            codec: 'h264',
            status: 'Ready',
            createdAt: '2026-11-24T10:00:00Z',
          }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()
    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'workspace.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    const overviewTab = await screen.findByTestId('workspace-tab-overview')
    const cutterTab = screen.getByTestId('workspace-tab-cutter')
    const compressorTab = screen.getByTestId('workspace-tab-compressor')
    const splicerTab = screen.getByTestId('workspace-tab-splicer')

    expect(overviewTab).toHaveAttribute('aria-selected', 'true')

    // Keyboard arrow navigation from Overview -> Cut -> Compress -> Splice
    fireEvent.keyDown(overviewTab, { key: 'ArrowRight' })
    expect(cutterTab).toHaveAttribute('aria-selected', 'true')
    expect(document.activeElement).toBe(cutterTab)
    fireEvent.change(screen.getByTestId('cut-start-input'), { target: { value: '00:05.0' } })

    fireEvent.keyDown(cutterTab, { key: 'ArrowRight' })
    expect(compressorTab).toHaveAttribute('aria-selected', 'true')
    expect(document.activeElement).toBe(compressorTab)
    fireEvent.click(screen.getByTestId('toggle-advanced-btn'))
    fireEvent.change(screen.getByTestId('crf-input'), { target: { value: '28' } })

    fireEvent.keyDown(compressorTab, { key: 'ArrowRight' })
    expect(splicerTab).toHaveAttribute('aria-selected', 'true')
    expect(document.activeElement).toBe(splicerTab)
    fireEvent.change(screen.getByTestId('splice-value-input'), { target: { value: '15' } })

    // Home returns to Overview, End jumps to Splice
    fireEvent.keyDown(splicerTab, { key: 'Home' })
    expect(overviewTab).toHaveAttribute('aria-selected', 'true')
    expect(document.activeElement).toBe(overviewTab)

    // Switch back to each tool via clicks and verify preserved inputs
    fireEvent.click(cutterTab)
    expect(screen.getByTestId('cut-start-input')).toHaveValue('00:05.0')

    fireEvent.click(compressorTab)
    expect(screen.getByTestId('crf-input')).toHaveValue('28')

    fireEvent.click(splicerTab)
    expect(screen.getByTestId('splice-value-input')).toHaveValue('15')
  })

  it('disables Upload another video while any tool reports active processing and displays active tab indicator', async () => {
    class MockXHR {
      upload = { onprogress: null }
      status = 201
      responseText = JSON.stringify({
        id: 'app-guard-vid',
        fileName: 'guard.mkv',
        sizeBytes: 2048,
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
            id: 'app-guard-vid',
            fileName: 'guard.mkv',
            contentType: 'video/x-matroska',
            sizeBytes: 2048,
            durationSeconds: 30.0,
            resolution: { width: 1280, height: 720 },
            codec: 'h264',
            status: 'Ready',
            createdAt: '2026-11-24T10:00:00Z',
          }),
      })
      // POST /api/videos/{id}/cut
      .mockResolvedValueOnce({
        ok: true,
        status: 202,
        json: () =>
          Promise.resolve({
            jobId: 'cut-job-1',
            sourceVideoId: 'app-guard-vid',
            startSeconds: 0,
            endSeconds: 10,
            mode: 'FastCopy',
            status: 'Pending',
            progressPercent: 10,
            outputFileName: null,
            outputDurationSeconds: null,
            outputSizeBytes: null,
            errorMessage: null,
            createdAt: '2026-11-24T10:01:00Z',
          }),
      })
      // GET /api/cut-jobs/{jobId}
      .mockResolvedValue({
        ok: true,
        status: 200,
        json: () =>
          Promise.resolve({
            jobId: 'cut-job-1',
            sourceVideoId: 'app-guard-vid',
            startSeconds: 0,
            endSeconds: 10,
            mode: 'FastCopy',
            status: 'Processing',
            progressPercent: 40,
            outputFileName: null,
            outputDurationSeconds: null,
            outputSizeBytes: null,
            errorMessage: null,
            createdAt: '2026-11-24T10:01:00Z',
          }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    expect(await screen.findByRole('region', { name: /video uploader/i })).toBeInTheDocument()
    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [new File(['x'], 'guard.mkv', { type: 'video/x-matroska' })] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    const uploadAnotherBtn = await screen.findByTestId('upload-another-btn')
    expect(uploadAnotherBtn).toBeEnabled()

    // Start a Cut job
    fireEvent.click(screen.getByTestId('open-cutter-btn'))
    fireEvent.click(screen.getByTestId('start-cut-btn'))

    expect(await screen.findByTestId('cut-progress')).toBeInTheDocument()
    expect(screen.getByTestId('workspace-tab-cutter-active')).toBeInTheDocument()

    // Return to Overview: Upload another video button must be disabled with clear message
    fireEvent.click(screen.getByTestId('workspace-tab-overview'))
    expect(screen.getByTestId('upload-another-btn')).toBeDisabled()
    expect(screen.getByTestId('active-jobs-guard-msg')).toHaveTextContent(/Cut/)
  })
})
