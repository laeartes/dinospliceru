import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import VideoUploader from '../../src/components/VideoUploader'

function createFile(name: string, type: string, size = 1024): File {
  return new File(['x'.repeat(size)], name, { type })
}

describe('VideoUploader', () => {
  it('renders the dropzone and browse button', () => {
    render(<VideoUploader />)

    expect(screen.getByText(/drag & drop your video here/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /browse files/i })).toBeInTheDocument()
    expect(screen.getByText(/\.mp4, .mov, .webm/i)).toBeInTheDocument()
    expect(screen.getByText(/500mb/i)).toBeInTheDocument()
  })

  it('applies drag-over styles on dragover', () => {
    render(<VideoUploader />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!

    fireEvent.dragEnter(dropzone)
    expect(dropzone.className).toContain('border-teal-500')
    expect(dropzone.className).toContain('bg-teal-50')

    fireEvent.dragLeave(dropzone)
    expect(dropzone.className).not.toContain('border-teal-500')
    expect(dropzone.className).not.toContain('bg-teal-50')
  })

  it('calls onFileSelect when a single file is dropped', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader onFileSelect={onFileSelect} />)

    const dropzone = screen.getByText(/drag & drop your video here/i).parentElement!.parentElement!
    const file = createFile('test.mp4', 'video/mp4')

    fireEvent.drop(dropzone, {
      dataTransfer: { files: [file] },
    })

    expect(onFileSelect).toHaveBeenCalledOnce()
    expect(onFileSelect).toHaveBeenCalledWith(file)
    expect(screen.queryByText(/only one file/i)).not.toBeInTheDocument()
  })

  it('shows an error when multiple files are dropped and does not call onFileSelect', () => {
    const onFileSelect = vi.fn()
    render(<VideoUploader onFileSelect={onFileSelect} />)

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
    render(<VideoUploader onFileSelect={onFileSelect} />)

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
    render(<VideoUploader onFileSelect={onFileSelect} />)

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
    render(<VideoUploader onFileSelect={onFileSelect} />)

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
    render(<VideoUploader onFileSelect={onFileSelect} />)

    const input = screen.getByTestId('file-input')
    const file = createFile('data.json', 'application/json')

    fireEvent.change(input, { target: { files: [file] } })

    expect(onFileSelect).not.toHaveBeenCalled()
    expect(screen.getByText(/invalid file type/i)).toBeInTheDocument()
  })
})