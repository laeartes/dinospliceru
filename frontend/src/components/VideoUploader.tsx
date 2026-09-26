import { useState, useRef, type DragEvent, type ChangeEvent } from 'react'
import { formatFileSize } from '../utils/formatFileSize'

const ALLOWED_EXTENSIONS = ['.mp4', '.mov', '.webm']
const ALLOWED_MIME_TYPES = ['video/mp4', 'video/quicktime', 'video/webm']
const MAX_FILE_SIZE_BYTES = 500 * 1024 * 1024

const ACCEPT_STRING = ALLOWED_EXTENSIONS.join(',') + ',' + ALLOWED_MIME_TYPES.join(',')

function getExtension(file: File): string {
  const dotIndex = file.name.lastIndexOf('.')
  if (dotIndex === -1) return ''
  return file.name.slice(dotIndex).toLowerCase()
}

function validateVideoFile(file: File): string | null {
  const ext = getExtension(file)
  if (ext && !ALLOWED_EXTENSIONS.includes(ext)) {
    return `Invalid file type. Accepted: ${ALLOWED_EXTENSIONS.join(', ')}`
  }

  if (file.type && !ALLOWED_MIME_TYPES.includes(file.type)) {
    return `Invalid file type. Accepted: ${ALLOWED_EXTENSIONS.join(', ')}`
  }

  if (file.size > MAX_FILE_SIZE_BYTES) {
    return `File too large. Maximum size is 500MB`
  }

  return null
}

interface VideoUploaderProps {
  onFileSelect?: (file: File) => void
}

function VideoUploader({ onFileSelect }: VideoUploaderProps) {
  const [isDragging, setIsDragging] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  function handleDragOver(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    e.stopPropagation()
  }

  function handleDragEnter(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(true)
  }

  function handleDragLeave(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
    setError(null)

    const files = e.dataTransfer.files
    if (files.length === 0) return

    if (files.length > 1) {
      setError('Only one file can be uploaded at a time')
      return
    }

    const validationError = validateVideoFile(files[0])
    if (validationError) {
      setError(validationError)
      return
    }

    setSelectedFile(files[0])
  }

  function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    setError(null)
    const file = e.target.files?.[0]
    if (!file) return

    const validationError = validateVideoFile(file)
    if (validationError) {
      setError(validationError)
      return
    }

    setSelectedFile(file)
  }

  function handleBrowseClick() {
    inputRef.current?.click()
  }

  function handleConfirm() {
    if (selectedFile) {
      onFileSelect?.(selectedFile)
    }
  }

  function handleChooseDifferent() {
    setSelectedFile(null)
    setError(null)
  }

  const borderClass = isDragging
    ? 'border-teal-500 bg-teal-50'
    : 'border-slate-300 bg-white/80 backdrop-blur-sm'

  return (
    <div
      className={`border-2 border-solid ${borderClass} relative p-8 text-center transition-colors`}
      onDragOver={handleDragOver}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <input
        ref={inputRef}
        type="file"
        accept={ACCEPT_STRING}
        className="hidden"
        data-testid="file-input"
        multiple={false}
        onChange={handleFileChange}
      />

      {selectedFile ? (
        <div className="flex flex-col items-center gap-3">
          <svg
            className="h-10 w-10 text-teal-500"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M15.75 10.5l4.72-4.72a.75.75 0 011.28.53v11.38a.75.75 0 01-1.28.53l-4.72-4.72M4.5 18.75h9a2.25 2.25 0 002.25-2.25v-9a2.25 2.25 0 00-2.25-2.25h-9A2.25 2.25 0 002.25 7.5v9a2.25 2.25 0 002.25 2.25z"
            />
          </svg>

          <p className="text-slate-800" data-testid="file-name">
            {selectedFile.name}
          </p>
          <p className="text-sm text-slate-500" data-testid="file-size">
            {formatFileSize(selectedFile.size)}
          </p>

          <div className="mt-2 flex gap-3">
            <button
              type="button"
              onClick={handleConfirm}
              className="bg-teal-500 px-4 py-2 text-white hover:bg-teal-600 transition-colors"
              data-testid="confirm-upload"
            >
              Upload
            </button>
            <button
              type="button"
              onClick={handleChooseDifferent}
              className="border border-slate-300 px-4 py-2 text-slate-700 hover:bg-slate-100 transition-colors"
              data-testid="choose-different"
            >
              Choose different
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="flex flex-col items-center gap-3">
            <svg
              className="h-10 w-10 text-teal-500"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={1.5}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
              />
            </svg>

            <p className="text-slate-800">
              Drag & drop your video here
            </p>
            <p className="text-sm text-slate-500">or</p>

            <button
              type="button"
              onClick={handleBrowseClick}
              className="bg-teal-500 px-4 py-2 text-white hover:bg-teal-600 transition-colors"
            >
              Browse files
            </button>

            <p className="text-xs text-slate-400">
              Accepted: .mp4, .mov, .webm &middot; Max size: 500MB
            </p>
          </div>

          {error && (
            <p className="mt-3 text-sm text-red-600">{error}</p>
          )}
        </>
      )}
    </div>
  )
}

export default VideoUploader