import { render, screen, fireEvent } from '@testing-library/react'
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
})
