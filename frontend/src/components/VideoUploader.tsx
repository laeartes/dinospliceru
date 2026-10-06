import { useState, useRef, useEffect, type DragEvent, type ChangeEvent, type KeyboardEvent } from 'react'
import type { VideoUploadConfig } from '../api/configApi'
import { formatFileSize } from '../utils/formatFileSize'

function getExtension(file: File): string {
  const dotIndex = file.name.lastIndexOf('.')
  if (dotIndex === -1) return ''
  return file.name.slice(dotIndex).toLowerCase()
}

function validateVideoFile(file: File, config: VideoUploadConfig): string | null {
  const acceptedList = config.allowedExtensions.join(', ')
  const ext = getExtension(file)
  if (!ext) {
    return `File has no extension. Accepted: ${acceptedList}`
  }

  // Case-insensitive, matching the backend's OrdinalIgnoreCase comparison
  const allowedExtensions = config.allowedExtensions.map((e) => e.toLowerCase())
  if (!allowedExtensions.includes(ext)) {
    return `Invalid file type. Accepted: ${acceptedList}`
  }

  const allowedContentTypes = config.allowedContentTypes.map((t) => t.toLowerCase())
  if (file.type && !allowedContentTypes.includes(file.type.toLowerCase())) {
    return `Invalid file type. Accepted: ${acceptedList}`
  }

  if (file.size > config.maxFileSizeBytes) {
    return `File too large. Maximum size is ${formatFileSize(config.maxFileSizeBytes)}`
  }

  return null
}

interface VideoUploaderProps {
  config: VideoUploadConfig
  onFileSelect?: (file: File) => void
}

function VideoUploader({ config, onFileSelect }: VideoUploaderProps) {
  const [isDragging, setIsDragging] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const confirmButtonRef = useRef<HTMLButtonElement>(null)
  const browseButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (selectedFile) {
      confirmButtonRef.current?.focus()
    }
  }, [selectedFile])

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

    const validationError = validateVideoFile(files[0], config)
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

    const validationError = validateVideoFile(file, config)
    if (validationError) {
      setError(validationError)
      if (inputRef.current) {
        inputRef.current.value = ''
      }
      return
    }

    setSelectedFile(file)
    if (inputRef.current) {
      inputRef.current.value = ''
    }
  }

  function handleBrowseClick() {
    if (inputRef.current) {
      inputRef.current.value = ''
    }
    inputRef.current?.click()
  }

  function handleConfirm() {
    if (selectedFile) {
      onFileSelect?.(selectedFile)
      if (inputRef.current) {
        inputRef.current.value = ''
      }
    }
  }

  function handleChooseDifferent() {
    setSelectedFile(null)
    setError(null)
    if (inputRef.current) {
      inputRef.current.value = ''
    }
    setTimeout(() => {
      browseButtonRef.current?.focus()
    }, 0)
  }

  function handleKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (!selectedFile) {
      if ((e.key === 'Enter' || e.key === ' ') && e.target === e.currentTarget) {
        e.preventDefault()
        handleBrowseClick()
      }
    } else {
      if (e.key === 'Escape') {
        e.preventDefault()
        handleChooseDifferent()
      }
    }
  }

  const acceptString = [...config.allowedExtensions, ...config.allowedContentTypes].join(',')

  const borderClass = isDragging
    ? 'border-cyber-cyan bg-cyan-50/50'
    : 'border-cyber-border bg-white'

  return (
    <div
      role="region"
      aria-label="Video uploader"
      tabIndex={selectedFile ? -1 : 0}
      className={`border-2 border-solid ${borderClass} relative p-8 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan`}
      onDragOver={handleDragOver}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      onKeyDown={handleKeyDown}
    >
      <input
        ref={inputRef}
        type="file"
        accept={acceptString}
        className="sr-only"
        tabIndex={-1}
        data-testid="file-input"
        multiple={false}
        onChange={handleFileChange}
      />

      <div className="mb-6 flex items-center justify-between border-b border-cyber-border pb-3 text-xs font-mono text-slate-500">
        <span className="flex items-center gap-1.5 text-cyber-dark">
          <span className="text-cyber-pink" aria-hidden="true">✦</span>
          <span>[ video.splicer // uploader ]</span>
        </span>
        <span className="text-cyber-pink font-semibold">01 // READY (｡•̀ᴗ-)✧</span>
      </div>

      {selectedFile ? (
        <div className="flex flex-col items-center gap-3" aria-live="polite">
          <svg
            className="h-10 w-10 text-cyber-cyan"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M15.75 10.5l4.72-4.72a.75.75 0 011.28.53v11.38a.75.75 0 01-1.28.53l-4.72-4.72M4.5 18.75h9a2.25 2.25 0 002.25-2.25v-9a2.25 2.25 0 00-2.25-2.25h-9A2.25 2.25 0 002.25 7.5v9a2.25 2.25 0 002.25 2.25z"
            />
          </svg>

          <p className="text-slate-800 font-medium" data-testid="file-name">
            {selectedFile.name}
          </p>
          <p className="text-sm text-slate-500 font-mono" data-testid="file-size">
            {formatFileSize(selectedFile.size)} ✦
          </p>

          <div className="mt-2 flex gap-3">
            <button
              ref={confirmButtonRef}
              type="button"
              onClick={handleConfirm}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
              data-testid="confirm-upload"
            >
              Upload ✦
            </button>
            <button
              type="button"
              onClick={handleChooseDifferent}
              className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors"
              data-testid="choose-different"
            >
              Choose different (• ◡•)
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="flex flex-col items-center gap-3">
            <svg
              className="h-10 w-10 text-cyber-cyan"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={1.5}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
              />
            </svg>

            <p className="text-slate-800 font-medium">
              Drag & drop your video here <span className="text-cyber-pink font-mono">(｡•̀ᴗ-)✧</span>
            </p>
            <p className="text-sm text-slate-500 font-mono">or</p>

            <button
              ref={browseButtonRef}
              type="button"
              onClick={handleBrowseClick}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
            >
              Browse files ✦
            </button>

            <p className="text-xs text-slate-400 font-mono">
              Accepted: {config.allowedExtensions.join(', ')} &middot; Max size: {formatFileSize(config.maxFileSizeBytes)} (´｡• ᵕ •｡`)
            </p>
          </div>

          {error && (
            <p className="mt-3 text-sm text-red-600 font-mono" role="alert" aria-live="assertive">
              <span className="mr-1" aria-hidden="true">(；´Д｀)</span>
              {error}
            </p>
          )}
        </>
      )}
    </div>
  )
}

export default VideoUploader
