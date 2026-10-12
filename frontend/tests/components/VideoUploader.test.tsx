import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import VideoUploader from '../../src/components/VideoUploader'
import type { VideoUploadConfig } from '../../src/api/configApi'
import { formatFileSize } from '../../src/utils/formatFileSize'

const defaultConfig: VideoUploadConfig = {
  maxFileSizeBytes: 500 * 1024 * 1024,
  allowedExtensions: ['.mp4', '.mov', '.webm'],
  allowedContentTypes: ['video/mp4', 'video/quicktime', 'video/webm'],
}

const customConfig: VideoUploadConfig = {
  maxFileSizeBytes: 2 * 1024 * 1024,
  allowedExtensions: ['.mkv'],
  allowedContentTypes: ['video/x-matroska'],
}

function createFile(name: string, type: string, size = 1024): File {
  return new File(['x'.repeat(size)], name, { type })
}

describe('formatFileSize', () => {
  it('formats bytes as KB under 1MB', () => {
    expect(formatFileSize(500 * 1024)).toBe('500KB')
  })

  it('formats bytes as MB at or above 1MB', () => {
    expect(formatFileSize(1024 * 1024)).toBe('1MB')
    expect(formatFileSize(50 * 1024 * 1024)).toBe('50MB')
    expect(formatFileSize(1.5 * 1024 * 1024)).toBe('1.5MB')
  })
})

describe('VideoUploader', () => {
  it('renders the dropzone and browse button', () => {
    render(<VideoUploader config={defaultConfig} />)

    expect(screen.getByText(/drag & drop your video here/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /browse files/i })).toBeInTheDocument()
    expect(screen.getByText(/\.mp4, .mov, .webm/i)).toBeInTheDocument()
    expect(screen.getByText(/500mb/i)).toBeInTheDocument()
  })

  it('applies drag-over styles on dragover', () => {
    render(<VideoUploader config={defaultConfig} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!

    fireEvent.dragEnter(dropzone)
    expect(dropzone.className).toContain('border-cyber-cyan')

    fireEvent.dragLeave(dropzone)
    expect(dropzone.className).not.toContain('border-cyber-cyan')
  })

  it('shows file info after dropping a valid single file and does not call onFileSelect', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByTestId('file-name')).toHaveTextContent('test.mp4')
    expect(screen.getByTestId('file-size')).toHaveTextContent('1KB')
  })

  it('calls onFileSelect when the confirm (Upload) button is clicked', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    fireEvent.click(screen.getByTestId('confirm-upload'))

    expect(onFileSelect).toHaveBeenCalledOnce()
    expect(onFileSelect).toHaveBeenCalledWith(file)
  })

  it('returns to dropzone after clicking choose different', () => {
    render(<VideoUploader config={defaultConfig} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(screen.getByTestId('file-name')).toBeInTheDocument()

    fireEvent.click(screen.getByTestId('choose-different'))

    expect(screen.queryByTestId('file-name')).not.toBeInTheDocument()
    expect(screen.getByText(/drag & drop your video here/i)).toBeInTheDocument()
  })

  it('shows an error when multiple files are dropped and does not call onFileSelect', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const files = [
      createFile('a.mp4', 'video/mp4'),
      createFile('b.mp4', 'video/mp4'),
    ]

    fireEvent.drop(dropzone, {
      dataTransfer: { files },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/only one file can be uploaded at a time/i)).toBeInTheDocument()
  })

  it('rejects a dropped file with invalid extension', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('readme.txt', 'text/plain')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/invalid file type/i)).toBeInTheDocument()
  })

  it('rejects a dropped file with invalid mime type', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('video.mp4', 'application/octet-stream')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/invalid file type/i)).toBeInTheDocument()
  })

  it('rejects a dropped file over the size limit', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const oversized = createFile('large.mp4', 'video/mp4', 500 * 1024 * 1024 + 1)

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [oversized] },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/file too large/i)).toBeInTheDocument()
  })

  it('rejects a file selected via file picker with invalid extension', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const input = screen.getByTestId('file-input')
    const file = createFile('data.json', 'application/json')

    fireEvent.change(input, { target: { files: [file] } })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/invalid file type/i)).toBeInTheDocument()
  })

  it('rejects a dropped file without any file extension', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('videofile', 'video/mp4')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/file has no extension/i)).toBeInTheDocument()
  })

  it('rejects a file selected via file picker without any file extension', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader config={defaultConfig} onFileSelect={onFileSelect} />)

    const input = screen.getByTestId('file-input')
    const file = createFile('videofile', 'video/mp4')

    fireEvent.change(input, { target: { files: [file] } })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/file has no extension/i)).toBeInTheDocument()
  })

  it('allows selecting the same file again after clicking choose different', () => {
    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId<HTMLInputElement>('file-input')
    const file = createFile('clip.mp4', 'video/mp4')

    fireEvent.change(input, { target: { files: [file] } })
    expect(screen.getByTestId('file-name')).toHaveTextContent('clip.mp4')

    fireEvent.click(screen.getByTestId('choose-different'))
    expect(screen.queryByTestId('file-name')).not.toBeInTheDocument()
    expect(input.value).toBe('')

    fireEvent.change(input, { target: { files: [file] } })
    expect(screen.getByTestId('file-name')).toHaveTextContent('clip.mp4')
  })

  it('triggers file picker click when Enter or Space is pressed on the dropzone', () => {
    render(<VideoUploader config={defaultConfig} />)

    const dropzone = screen.getByRole('region', { name: /video uploader/i })
    const input = screen.getByTestId('file-input')
    const clickSpy = vi.spyOn(input, 'click')

    fireEvent.keyDown(dropzone, { key: 'Enter' })
    expect(clickSpy).toHaveBeenCalledTimes(1)

    fireEvent.keyDown(dropzone, { key: ' ' })
    expect(clickSpy).toHaveBeenCalledTimes(2)
  })

  it('focuses the upload button when a file is selected', () => {
    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId('file-input')
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.change(input, { target: { files: [file] } })

    const uploadButton = screen.getByTestId('confirm-upload')
    expect(document.activeElement).toBe(uploadButton)
  })

  it('clears selection when Escape key is pressed', () => {
    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId('file-input')
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.change(input, { target: { files: [file] } })
    expect(screen.getByTestId('file-name')).toBeInTheDocument()

    const dropzone = screen.getByRole('region', { name: /video uploader/i })
    fireEvent.keyDown(dropzone, { key: 'Escape' })

    expect(screen.queryByTestId('file-name')).not.toBeInTheDocument()
    expect(screen.getByText(/drag & drop your video here/i)).toBeInTheDocument()
  })

  it('shows accepted formats and max size from the config prop', () => {
    render(<VideoUploader config={customConfig} />)

    expect(screen.getByText(/accepted: \.mkv · max size: 2mb/i)).toBeInTheDocument()
    expect(screen.queryByText(/\.mp4/i)).not.toBeInTheDocument()
    expect(screen.getByTestId('file-input')).toHaveAttribute('accept', '.mkv,video/x-matroska')
  })

  it('accepts a file that is only allowed by the config prop', () => {
    render(<VideoUploader config={customConfig} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('clip.mkv', 'video/x-matroska')] } })

    expect(screen.getByTestId('file-name')).toHaveTextContent('clip.mkv')
  })

  it('rejects a file whose format is not in the config prop', () => {
    render(<VideoUploader config={customConfig} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('clip.mp4', 'video/mp4')] } })

    expect(screen.queryByTestId('file-name')).not.toBeInTheDocument()
    expect(screen.getByText(/invalid file type\. accepted: \.mkv/i)).toBeInTheDocument()
  })

  it('rejects a file over the max size from the config prop', () => {
    render(<VideoUploader config={customConfig} />)

    const input = screen.getByTestId('file-input')
    const oversized = createFile('clip.mkv', 'video/x-matroska', 2 * 1024 * 1024 + 1)
    fireEvent.change(input, { target: { files: [oversized] } })

    expect(screen.queryByTestId('file-name')).not.toBeInTheDocument()
    expect(screen.getByText(/file too large\. maximum size is 2mb/i)).toBeInTheDocument()
  })

  it('compares configured extensions case-insensitively', () => {
    render(<VideoUploader config={{ ...customConfig, allowedExtensions: ['.MKV'] }} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('clip.mkv', 'video/x-matroska')] } })

    expect(screen.getByTestId('file-name')).toHaveTextContent('clip.mkv')
  })

  it('updates progress bar on simulated onUploadProgress events', async () => {
    interface ProgressXhrMock {
      upload: { onprogress: ((e: ProgressEvent) => void) | null }
      open: () => void
      send: () => void
      abort: () => void
      addEventListener: () => void
      removeEventListener: () => void
    }

    let capturedXhr: ProgressXhrMock | null = null

    function createProgressMock(): ProgressXhrMock {
      const mock: ProgressXhrMock = {
        upload: { onprogress: null },
        open: vi.fn(),
        send: vi.fn(),
        abort: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }
      capturedXhr = mock
      return mock
    }

    vi.stubGlobal('XMLHttpRequest', vi.fn(function () {
      return createProgressMock()
    }))

    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('test.mp4', 'video/mp4')] } })

    fireEvent.click(screen.getByTestId('confirm-upload'))

    expect(screen.getByRole('progressbar')).toBeInTheDocument()
    expect(screen.getByTestId('upload-progress-percent')).toHaveTextContent('0%')

    act(() => {
      capturedXhr?.upload.onprogress?.({
        lengthComputable: true,
        loaded: 65,
        total: 100,
      } as ProgressEvent)
    })

    expect(screen.getByTestId('upload-progress-percent')).toHaveTextContent('65%')
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '65')
  })

  it('shows uploaded/processing state and updates to Ready on polled status change without manual refresh', async () => {
    vi.useFakeTimers()

    interface PollingXhrMock {
      status: number
      responseText: string
      upload: { onprogress: null }
      onload: (() => void) | null
      open: () => void
      abort: () => void
      addEventListener: () => void
      removeEventListener: () => void
      send: () => void
    }

    let capturedXhr: PollingXhrMock | null = null

    function createPollingMock(): PollingXhrMock {
      const mock: PollingXhrMock = {
        status: 200,
        responseText: '',
        upload: { onprogress: null },
        onload: null,
        open: vi.fn(),
        abort: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        send: vi.fn(),
      }
      capturedXhr = mock
      return mock
    }

    vi.stubGlobal('XMLHttpRequest', vi.fn(function () {
      return createPollingMock()
    }))

    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({
          id: 'vid-123',
          fileName: 'movie.mp4',
          contentType: 'video/mp4',
          sizeBytes: 2048,
          durationSeconds: null,
          resolution: null,
          codec: null,
          status: 'Processing',
          createdAt: '2026-10-11T12:00:00Z',
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve({
          id: 'vid-123',
          fileName: 'movie.mp4',
          contentType: 'video/mp4',
          sizeBytes: 2048,
          durationSeconds: 120.4,
          resolution: { width: 1920, height: 1080 },
          codec: 'h264',
          status: 'Ready',
          createdAt: '2026-10-11T12:00:00Z',
        }),
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('movie.mp4', 'video/mp4', 2048)] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    // Trigger upload success
    await act(async () => {
      if (capturedXhr) {
        capturedXhr.status = 201
        capturedXhr.responseText = JSON.stringify({
          id: 'vid-123',
          fileName: 'movie.mp4',
          sizeBytes: 2048,
          status: 'Uploaded',
        })
        capturedXhr.onload?.()
      }
    })

    // First status poll returned Processing
    expect(screen.getByTestId('video-processing-status')).toHaveTextContent(/Processing/i)
    expect(fetchMock).toHaveBeenCalledTimes(1)

    // Advance 2 seconds for next poll iteration
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })

    // Updates to Ready automatically
    expect(screen.getByTestId('video-ready-status')).toHaveTextContent(/Ready/i)
    expect(screen.getByTestId('ready-duration')).toHaveTextContent('2:00 (120.4s)')
    expect(screen.getByTestId('ready-resolution')).toHaveTextContent('1920x1080')
    expect(screen.getByTestId('ready-codec')).toHaveTextContent('h264')

    // Polling stops once status is Ready
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(fetchMock).toHaveBeenCalledTimes(2)

    vi.useRealTimers()
  })

  it('handles failure path and shows error state without crashing', async () => {
    vi.useFakeTimers()

    interface FailXhrMock {
      status: number
      responseText: string
      upload: { onprogress: null }
      onload: (() => void) | null
      open: () => void
      abort: () => void
      addEventListener: () => void
      removeEventListener: () => void
      send: () => void
    }

    let capturedXhr: FailXhrMock | null = null

    function createFailMock(): FailXhrMock {
      const mock: FailXhrMock = {
        status: 200,
        responseText: '',
        upload: { onprogress: null },
        onload: null,
        open: vi.fn(),
        abort: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        send: vi.fn(),
      }
      capturedXhr = mock
      return mock
    }

    vi.stubGlobal('XMLHttpRequest', vi.fn(function () {
      return createFailMock()
    }))

    const fetchMock = vi.fn().mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({
        id: 'vid-fail',
        fileName: 'broken.mp4',
        contentType: 'video/mp4',
        sizeBytes: 1024,
        durationSeconds: null,
        resolution: null,
        codec: null,
        status: 'Failed',
        createdAt: '2026-10-11T12:00:00Z',
      }),
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<VideoUploader config={defaultConfig} />)

    const input = screen.getByTestId('file-input')
    fireEvent.change(input, { target: { files: [createFile('broken.mp4', 'video/mp4')] } })
    fireEvent.click(screen.getByTestId('confirm-upload'))

    await act(async () => {
      if (capturedXhr) {
        capturedXhr.status = 201
        capturedXhr.responseText = JSON.stringify({
          id: 'vid-fail',
          fileName: 'broken.mp4',
          sizeBytes: 1024,
          status: 'Uploaded',
        })
        capturedXhr.onload?.()
      }
    })

    expect(screen.getByTestId('video-failed-status')).toHaveTextContent(/Failed/i)
    expect(screen.getByText(/processing failed on the server/i)).toBeInTheDocument()

    // Polling stops once Failed
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)

    // Can click choose another file to return to dropzone
    fireEvent.click(screen.getByRole('button', { name: /choose another file/i }))
    expect(screen.getByText(/drag & drop your video here/i)).toBeInTheDocument()

    vi.useRealTimers()
  })
})

