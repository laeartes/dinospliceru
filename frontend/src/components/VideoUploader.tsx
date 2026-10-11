import { useState, useRef, useEffect, type DragEvent, type ChangeEvent, type KeyboardEvent } from 'react'
import type { VideoUploadConfig } from '../api/configApi'
import { formatFileSize } from '../utils/formatFileSize'
import { useVideoUpload } from '../hooks/useVideoUpload'

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

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60)
  const remainingSecs = Math.floor(seconds % 60)
  return `${mins}:${remainingSecs.toString().padStart(2, '0')}`
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

  const { uploadState, startUpload, reset, retry } = useVideoUpload()

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
      void startUpload(selectedFile)
      setSelectedFile(null)
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

  function handleUploadAnother() {
    reset()
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
    if (uploadState.status !== 'idle') return

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

  // If a video is active (uploading, polling, ready, failed, or error)
  if (uploadState.status !== 'idle') {
    return (
      <div
        role="region"
        aria-label="Video processing status"
        className="border-2 border-solid border-cyber-border bg-white p-8 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan"
      >
        <div className="mb-6 flex items-center justify-between border-b border-cyber-border pb-3 text-xs font-mono text-slate-500">
          <span className="flex items-center gap-1.5 text-cyber-dark">
            <span className="text-cyber-pink" aria-hidden="true">✦</span>
            <span>[ video.splicer // status ]</span>
          </span>
          <span className="text-cyber-pink font-semibold">
            {uploadState.status === 'uploading' && '01 // UPLOADING ( ๑>ᴗ<๑ )'}
            {uploadState.status === 'polling' && '02 // PROCESSING ( •̀_•́ )'}
            {uploadState.status === 'ready' && '03 // READY (｡•̀ᴗ-)✧'}
            {uploadState.status === 'failed' && '04 // FAILED (；´Д｀)'}
            {uploadState.status === 'error' && 'ERROR (×_×)'}
          </span>
        </div>

        {uploadState.status === 'uploading' && (
          <div className="flex flex-col items-center gap-4" aria-live="polite">
            <p className="text-slate-800 font-medium" data-testid="uploading-file-name">
              {uploadState.fileName}
            </p>
            <p className="text-xs font-mono text-slate-500">
              {formatFileSize(uploadState.sizeBytes)}
            </p>

            <div className="w-full max-w-md">
              <div className="flex justify-between text-xs font-mono mb-1 text-slate-600">
                <span>Uploading...</span>
                <span data-testid="upload-progress-percent">{uploadState.progress}%</span>
              </div>
              <div
                role="progressbar"
                aria-valuenow={uploadState.progress}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label="Upload progress"
                className="h-3 w-full overflow-hidden bg-slate-100 border border-cyber-border"
              >
                <div
                  className="h-full bg-cyber-cyan transition-all duration-200"
                  style={{ width: `${uploadState.progress}%` }}
                />
              </div>
            </div>
            <p className="text-xs font-mono text-slate-400">Please keep this window open (´｡• ᵕ •｡`)</p>
          </div>
        )}

        {uploadState.status === 'polling' && (
          <div className="flex flex-col items-center gap-4" aria-live="polite">
            <svg
              className="h-10 w-10 text-cyber-cyan animate-spin"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={1.5}
              aria-hidden="true"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>

            <p className="text-slate-800 font-medium" data-testid="processing-file-name">
              {uploadState.fileName}
            </p>
            <div className="inline-flex items-center gap-2 px-3 py-1 bg-amber-50 border border-amber-200 text-amber-800 text-xs font-mono">
              <span className="h-2 w-2 rounded-full bg-amber-500 animate-pulse" />
              <span data-testid="video-processing-status">
                Status: {uploadState.videoStatus} (extracting metadata...)
              </span>
            </div>
            <p className="text-xs font-mono text-slate-400">Analyzing frame & stream parameters...</p>
          </div>
        )}

        {uploadState.status === 'ready' && (
          <div className="flex flex-col items-center gap-4" aria-live="polite">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
              </svg>
            </div>

            <p className="text-slate-800 font-semibold text-lg" data-testid="ready-file-name">
              {uploadState.video.fileName}
            </p>

            <div className="inline-flex items-center gap-2 px-3 py-1 bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-mono">
              <span className="h-2 w-2 rounded-full bg-emerald-500" />
              <span data-testid="video-ready-status">Status: Ready (｡•̀ᴗ-)✧</span>
            </div>

            <div className="flex flex-wrap justify-center gap-4 border border-cyber-border bg-slate-50 p-4 text-xs font-mono text-slate-600 w-full max-w-md">
              <div>
                <span className="text-slate-400">Size: </span>
                <span className="font-semibold text-slate-700">{formatFileSize(uploadState.video.sizeBytes)}</span>
              </div>
              {uploadState.video.durationSeconds !== null && (
                <div>
                  <span className="text-slate-400">Duration: </span>
                  <span className="font-semibold text-slate-700" data-testid="ready-duration">
                    {formatDuration(uploadState.video.durationSeconds)} ({uploadState.video.durationSeconds.toFixed(1)}s)
                  </span>
                </div>
              )}
              {uploadState.video.resolution && (
                <div>
                  <span className="text-slate-400">Resolution: </span>
                  <span className="font-semibold text-slate-700" data-testid="ready-resolution">
                    {uploadState.video.resolution.width}x{uploadState.video.resolution.height}
                  </span>
                </div>
              )}
              {uploadState.video.codec && (
                <div>
                  <span className="text-slate-400">Codec: </span>
                  <span className="font-semibold text-slate-700" data-testid="ready-codec">
                    {uploadState.video.codec}
                  </span>
                </div>
              )}
            </div>

            <button
              type="button"
              onClick={handleUploadAnother}
              className="mt-2 bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
              data-testid="upload-another-btn"
            >
              Upload another video ✦
            </button>
          </div>
        )}

        {uploadState.status === 'failed' && (
          <div className="flex flex-col items-center gap-4" aria-live="assertive">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-red-600">
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>

            <p className="text-slate-800 font-medium" data-testid="failed-file-name">
              {uploadState.fileName}
            </p>

            <div className="inline-flex items-center gap-2 px-3 py-1 bg-red-50 border border-red-200 text-red-800 text-xs font-mono" role="alert">
              <span className="h-2 w-2 rounded-full bg-red-500" />
              <span data-testid="video-failed-status">Status: Failed (；´Д｀)</span>
            </div>

            <p className="text-sm text-red-600 font-mono">{uploadState.error}</p>

            <div className="flex gap-3 mt-2">
              <button
                type="button"
                onClick={retry}
                className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
                data-testid="retry-btn"
              >
                Retry ( -_ -)
              </button>
              <button
                type="button"
                onClick={handleUploadAnother}
                className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors"
                data-testid="upload-another-btn"
              >
                Choose another file
              </button>
            </div>
          </div>
        )}

        {uploadState.status === 'error' && (
          <div className="flex flex-col items-center gap-4" aria-live="assertive">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-red-600">
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
              </svg>
            </div>

            <p className="text-slate-800 font-medium">{uploadState.fileName}</p>

            <p className="text-sm text-red-600 font-mono" role="alert" data-testid="upload-error-message">
              <span className="mr-1" aria-hidden="true">(；´Д｀)</span>
              {uploadState.error}
            </p>

            <div className="flex gap-3 mt-2">
              <button
                type="button"
                onClick={retry}
                className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
                data-testid="retry-btn"
              >
                Retry ( -_ -)
              </button>
              <button
                type="button"
                onClick={handleUploadAnother}
                className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors"
                data-testid="upload-another-btn"
              >
                Choose another file
              </button>
            </div>
          </div>
        )}
      </div>
    )
  }

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
