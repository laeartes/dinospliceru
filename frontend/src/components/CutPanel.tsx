import { useState, useEffect, useMemo, useRef, type FormEvent } from 'react'
import type { VideoResponse } from '../api/videoApi'
import { type CutMode, getCutDownloadUrl } from '../api/cutApi'
import { useCutJob } from '../hooks/useCutJob'
import { useToast } from '../hooks/useToast'
import { formatFileSize } from '../utils/formatFileSize'
import { formatTimestamp, parseTimestamp, validateCutRange } from '../utils/cutValidation'

interface CutPanelProps {
  video: VideoResponse
  localFile?: File | null
  onBack?: () => void
  onCuttingChange?: (isCutting: boolean) => void
}

function floorToTenths(seconds: number): number {
  return Math.floor(seconds * 10) / 10
}

export default function CutPanel({ video, localFile = null, onBack, onCuttingChange }: CutPanelProps) {
  const maxDuration = video.durationSeconds
  const maxBoundSeconds = maxDuration !== null ? floorToTenths(maxDuration) : null
  const [startInput, setStartInput] = useState('00:00.0')
  const [endInput, setEndInput] = useState(() =>
    maxBoundSeconds !== null ? formatTimestamp(maxBoundSeconds) : ''
  )
  const [mode, setMode] = useState<CutMode>('FastCopy')
  const videoRef = useRef<HTMLVideoElement | null>(null)

  const { cutState, startCut, reset, retry } = useCutJob(video.id)
  const { showError } = useToast()

  const previewUrl = useMemo(() => {
    if (!localFile || typeof URL.createObjectURL !== 'function') {
      return null
    }
    try {
      return URL.createObjectURL(localFile)
    } catch {
      return null
    }
  }, [localFile])

  useEffect(() => {
    if (!previewUrl) return
    return () => {
      URL.revokeObjectURL(previewUrl)
    }
  }, [previewUrl])

  useEffect(() => {
    if (cutState.status === 'error' || cutState.status === 'failed') {
      showError({ error: cutState.error, code: cutState.code })
    }
  }, [cutState, showError])

  const isCutting = cutState.status === 'triggering' || cutState.status === 'polling'

  useEffect(() => {
    onCuttingChange?.(isCutting)
  }, [isCutting, onCuttingChange])

  useEffect(() => {
    return () => {
      onCuttingChange?.(false)
    }
  }, [onCuttingChange])

  const validation = validateCutRange(startInput, endInput, maxDuration)
  const parsedStart = parseTimestamp(startInput)
  const parsedEnd = parseTimestamp(endInput)

  const sliderStart =
    maxBoundSeconds !== null && parsedStart !== null
      ? Math.min(Math.max(parsedStart, 0), maxBoundSeconds)
      : 0
  const sliderEnd =
    maxBoundSeconds !== null && parsedEnd !== null
      ? Math.min(Math.max(parsedEnd, 0), maxBoundSeconds)
      : maxBoundSeconds ?? 0

  function clampToDuration(time: number): number {
    const safe = Math.max(0, Number.isFinite(time) ? time : 0)
    const clamped = maxDuration !== null ? Math.min(safe, maxDuration) : safe
    return floorToTenths(clamped)
  }

  function handleSetStartFromVideo() {
    if (!videoRef.current) return
    setStartInput(formatTimestamp(clampToDuration(videoRef.current.currentTime)))
  }

  function handleSetEndFromVideo() {
    if (!videoRef.current) return
    setEndInput(formatTimestamp(clampToDuration(videoRef.current.currentTime)))
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (validation.startSeconds === null || validation.endSeconds === null) {
      return
    }

    void startCut({
      startSeconds: validation.startSeconds,
      endSeconds: validation.endSeconds,
      mode,
    })
  }

  function handleCutAgain() {
    reset()
  }

  const progressPercent =
    cutState.status === 'polling' ? Math.min(100, Math.max(0, cutState.job.progressPercent ?? 0)) : 0

  return (
    <section
      role="region"
      aria-label="Cut video range and results"
      className="border-2 border-solid border-cyber-border bg-white p-8 text-center transition-colors"
      data-testid="cut-panel"
    >
      {/* Top Navigation & Context Bar */}
      {onBack && (
        <div className="mb-4 flex items-center justify-between border-b border-dashed border-cyber-border/80 pb-3">
          <button
            type="button"
            onClick={onBack}
            className="inline-flex items-center gap-1.5 border border-cyber-border bg-slate-50 px-3 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-dark hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer"
            data-testid="cut-back-btn"
          >
            ← Back (• ◡•)
          </button>
          <div className="flex items-center gap-2 font-mono text-xs text-slate-500 truncate max-w-xs sm:max-w-md">
            <span className="text-slate-400">Source:</span>
            <span className="font-semibold text-slate-700 truncate">{video.fileName}</span>
            <span className="text-slate-400">({formatFileSize(video.sizeBytes)})</span>
          </div>
        </div>
      )}

      {/* Panel Header */}
      <div className="mb-6 flex items-center justify-between border-b border-cyber-border pb-3 text-xs font-mono text-slate-500">
        <span className="flex items-center gap-1.5 text-cyber-dark">
          <span className="text-cyber-pink" aria-hidden="true">✦</span>
          <span>[ video.splicer // cut ]</span>
        </span>
        <span className="text-cyber-pink font-semibold">
          {cutState.status === 'idle' && '01 // TRIM RANGE (｡•̀ᴗ-)✧'}
          {(cutState.status === 'triggering' || cutState.status === 'polling') &&
            '02 // CUTTING ( •̀_•́ )'}
          {cutState.status === 'ready' && '03 // READY (｡•̀ᴗ-)✧'}
          {cutState.status === 'failed' && '04 // FAILED (；´Д｀)'}
          {cutState.status === 'error' && 'ERROR (×_×)'}
        </span>
      </div>

      {/* IDLE: Cut Range & Mode Form */}
      {cutState.status === 'idle' && (
        <form onSubmit={handleSubmit} className="flex flex-col items-center gap-5" data-testid="cut-form">
          {/* Optional Local Video Preview */}
          {previewUrl && (
            <div className="w-full max-w-md border border-cyber-border bg-slate-50 p-3">
              <video
                ref={videoRef}
                src={previewUrl}
                controls
                className="w-full max-h-56 bg-black"
                data-testid="cut-video-preview"
              />
              <div className="mt-2.5 flex flex-wrap justify-center gap-2">
                <button
                  type="button"
                  onClick={handleSetStartFromVideo}
                  className="border border-cyber-border bg-white px-3 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-cyan hover:bg-cyber-cyan/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer"
                  data-testid="set-start-from-video-btn"
                >
                  Set Start from current time ✦
                </button>
                <button
                  type="button"
                  onClick={handleSetEndFromVideo}
                  className="border border-cyber-border bg-white px-3 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-pink hover:bg-cyber-pink/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors cursor-pointer"
                  data-testid="set-end-from-video-btn"
                >
                  Set End from current time ✦
                </button>
              </div>
            </div>
          )}

          {/* Start / End Time Inputs */}
          <div className="w-full max-w-md text-left">
            <p className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              1. Select Start &amp; End Range (mm:ss.s):
            </p>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label htmlFor="cut-start-input" className="block text-xs font-mono text-slate-500 mb-1">
                  Start
                </label>
                <input
                  id="cut-start-input"
                  type="text"
                  value={startInput}
                  onChange={(e) => setStartInput(e.target.value)}
                  placeholder="00:00.0"
                  className="w-full border-2 border-cyber-border bg-slate-50 px-3 py-2 font-mono text-sm text-slate-800 placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors"
                  data-testid="cut-start-input"
                />
              </div>
              <div>
                <label htmlFor="cut-end-input" className="block text-xs font-mono text-slate-500 mb-1">
                  End
                </label>
                <input
                  id="cut-end-input"
                  type="text"
                  value={endInput}
                  onChange={(e) => setEndInput(e.target.value)}
                  placeholder={maxBoundSeconds !== null ? formatTimestamp(maxBoundSeconds) : '00:10.0'}
                  className="w-full border-2 border-cyber-border bg-slate-50 px-3 py-2 font-mono text-sm text-slate-800 placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors"
                  data-testid="cut-end-input"
                />
              </div>
            </div>

            {maxBoundSeconds !== null && maxBoundSeconds > 0 && (
              <div className="mt-3 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="w-10 text-[11px] font-mono text-slate-400">Start</span>
                  <input
                    type="range"
                    aria-label="Start time slider"
                    min={0}
                    max={maxBoundSeconds}
                    step={0.1}
                    value={sliderStart}
                    onChange={(e) => setStartInput(formatTimestamp(clampToDuration(Number(e.target.value))))}
                    className="w-full accent-cyber-cyan cursor-pointer"
                    data-testid="cut-start-range"
                  />
                </div>
                <div className="flex items-center gap-2">
                  <span className="w-10 text-[11px] font-mono text-slate-400">End</span>
                  <input
                    type="range"
                    aria-label="End time slider"
                    min={0}
                    max={maxBoundSeconds}
                    step={0.1}
                    value={sliderEnd}
                    onChange={(e) => setEndInput(formatTimestamp(clampToDuration(Number(e.target.value))))}
                    className="w-full accent-cyber-pink cursor-pointer"
                    data-testid="cut-end-range"
                  />
                </div>
              </div>
            )}

            <p className="mt-2 text-xs font-mono text-slate-400" data-testid="cut-helper-bounds">
              Range: 00:00.0 – {maxBoundSeconds !== null ? formatTimestamp(maxBoundSeconds) : 'source end'}
              {validation.startSeconds !== null && validation.endSeconds !== null && (
                <>
                  {' '}
                  &middot; Clip: {formatTimestamp(validation.endSeconds - validation.startSeconds)} (
                  {(validation.endSeconds - validation.startSeconds).toFixed(1)}s)
                </>
              )}
            </p>
          </div>

          {/* Cut Mode Toggle */}
          <div className="w-full max-w-md text-left">
            <label className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              2. Choose Cut Mode:
            </label>
            <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Cut mode">
              <button
                type="button"
                role="radio"
                aria-checked={mode === 'FastCopy'}
                onClick={() => setMode('FastCopy')}
                className={`border px-3 py-2 text-left font-mono focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer ${
                  mode === 'FastCopy'
                    ? 'border-cyber-cyan bg-cyber-cyan/15 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="cut-mode-fast"
              >
                <span className="block text-sm">Fast stream copy</span>
                <span className="block text-[11px] font-normal text-slate-500">
                  Instant, keyframe-aligned ✦
                </span>
              </button>
              <button
                type="button"
                role="radio"
                aria-checked={mode === 'FrameAccurate'}
                onClick={() => setMode('FrameAccurate')}
                className={`border px-3 py-2 text-left font-mono focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors cursor-pointer ${
                  mode === 'FrameAccurate'
                    ? 'border-cyber-pink bg-cyber-pink/10 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="cut-mode-accurate"
              >
                <span className="block text-sm">Frame-accurate re-encode</span>
                <span className="block text-[11px] font-normal text-slate-500">
                  Exact cut points ( •̀_•́ )
                </span>
              </button>
            </div>
          </div>

          {/* Validation Error */}
          {validation.error && (
            <p className="text-sm text-red-600 font-mono" role="alert" data-testid="cut-validation-error">
              {validation.error}
            </p>
          )}

          {/* Submit Button */}
          <button
            type="submit"
            disabled={validation.error !== null}
            className="mt-1 bg-cyber-cyan px-6 py-2.5 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white disabled:opacity-50 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
            data-testid="start-cut-btn"
          >
            Cut Video (｡•̀ᴗ-)✧ ✦
          </button>
        </form>
      )}

      {/* TRIGGERING / POLLING: Progress View */}
      {(cutState.status === 'triggering' || cutState.status === 'polling') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="polite" data-testid="cut-progress">
          <svg
            className="h-10 w-10 text-cyber-cyan animate-spin"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
            aria-hidden="true"
          >
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>

          <p className="text-slate-800 font-medium">Cutting {video.fileName}...</p>

          <div className="inline-flex items-center gap-2 px-3 py-1 bg-amber-50 border border-amber-200 text-amber-800 text-xs font-mono">
            <span className="h-2 w-2 rounded-full bg-amber-500 animate-pulse" />
            <span data-testid="cut-job-status">
              {cutState.status === 'triggering'
                ? 'Status: Initializing cut job ( ๑>ᴗ<๑ )'
                : cutState.job.status === 'Pending'
                ? 'Status: Pending (queued in line...) ( •̀_•́ )'
                : 'Status: Processing (trimming frames...) ( •̀_•́ )'}
            </span>
          </div>

          <div className="w-full max-w-md">
            <div className="flex justify-between text-xs font-mono mb-1 text-slate-600">
              <span>Progress</span>
              <span data-testid="cut-progress-percent">{progressPercent}%</span>
            </div>
            <div
              role="progressbar"
              aria-valuenow={progressPercent}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label="Cut job progress"
              className="h-3 w-full overflow-hidden bg-slate-100 border border-cyber-border"
              data-testid="cut-progress-bar"
            >
              <div
                className="h-full bg-cyber-cyan transition-all duration-200"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          </div>

          <p className="text-xs font-mono text-slate-400">
            Please wait while the media worker trims your clip (´｡• ᵕ •｡`)
          </p>
        </div>
      )}

      {/* READY: Resulting Clip Display & Download */}
      {cutState.status === 'ready' && (
        <div className="flex flex-col items-center gap-4 py-2" aria-live="polite" data-testid="cut-results">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
            </svg>
          </div>

          <p className="text-slate-800 font-semibold text-lg">Cut Complete! (｡•̀ᴗ-)✧</p>

          <div className="w-full max-w-md border border-cyber-border bg-slate-50 p-4 text-left font-mono text-xs space-y-2">
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">Filename:</span>
              <span className="font-semibold text-slate-800 truncate" data-testid="cut-output-filename">
                {cutState.job.outputFileName ?? 'trimmed-clip'}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">Trimmed Duration:</span>
              <span className="font-semibold text-slate-700" data-testid="cut-output-duration">
                {cutState.job.outputDurationSeconds !== null
                  ? `${formatTimestamp(cutState.job.outputDurationSeconds)} (${cutState.job.outputDurationSeconds.toFixed(1)}s)`
                  : `${formatTimestamp(cutState.job.endSeconds - cutState.job.startSeconds)}`}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">File Size:</span>
              <span className="font-semibold text-slate-700" data-testid="cut-output-size">
                {cutState.job.outputSizeBytes !== null ? formatFileSize(cutState.job.outputSizeBytes) : '—'}
              </span>
            </div>
          </div>

          <div className="mt-2 flex flex-wrap items-center justify-center gap-3">
            <a
              href={getCutDownloadUrl(cutState.job.jobId)}
              download={cutState.job.outputFileName ?? undefined}
              className="bg-cyber-cyan px-5 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
              data-testid="cut-download-link"
            >
              Download Clip ⤓ (｡•̀ᴗ-)✧
            </a>
            <button
              type="button"
              onClick={handleCutAgain}
              className="border border-cyber-border bg-white px-4 py-2 font-medium text-cyber-dark hover:border-cyber-pink hover:bg-cyber-pink/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="cut-again-btn"
            >
              Cut again (• ◡•) ✦
            </button>
          </div>
        </div>
      )}

      {/* FAILED / ERROR: Error Display & Retry */}
      {(cutState.status === 'failed' || cutState.status === 'error') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="assertive" data-testid="cut-error">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-red-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>

          <p className="text-slate-800 font-medium">Cut operation failed (；´Д｀)</p>

          <p className="text-sm text-red-600 font-mono" role="alert" data-testid="cut-error-message">
            <span className="mr-1" aria-hidden="true">(；´Д｀)</span>
            {cutState.error}
          </p>

          <div className="flex gap-3 mt-2">
            <button
              type="button"
              onClick={retry}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="cut-retry-btn"
            >
              Retry ( -_ -)
            </button>
            <button
              type="button"
              onClick={handleCutAgain}
              className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="cut-adjust-btn"
            >
              Adjust range (• ◡•)
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
