import { useState, useEffect, type FormEvent } from 'react'
import type { VideoResponse } from '../api/videoApi'
import { type SpliceMode, type SpliceSegment } from '../api/spliceApi'
import { useSpliceJob } from '../hooks/useSpliceJob'
import { useToast } from '../hooks/useToast'
import { formatFileSize } from '../utils/formatFileSize'
import {
  type LengthUnit,
  type SizeUnit,
  formatDuration,
  validateSpliceParameters,
} from '../utils/spliceValidation'

interface SplicePanelProps {
  video: VideoResponse
  onBack?: () => void
  onSplicingChange?: (isSplicing: boolean) => void
}

export default function SplicePanel({ video, onBack, onSplicingChange }: SplicePanelProps) {
  const [mode, setMode] = useState<SpliceMode>('ByLength')
  const [lengthUnit, setLengthUnit] = useState<LengthUnit>('s')
  const [sizeUnit, setSizeUnit] = useState<SizeUnit>('MB')
  const [inputValue, setInputValue] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)

  const { spliceState, startSplice, reset, retry } = useSpliceJob(video.id)
  const { showError } = useToast()

  useEffect(() => {
    if (spliceState.status === 'error' || spliceState.status === 'failed') {
      showError({ error: spliceState.error, code: spliceState.code })
    }
  }, [spliceState, showError])

  const isSplicing = spliceState.status === 'triggering' || spliceState.status === 'polling'

  useEffect(() => {
    onSplicingChange?.(isSplicing)
  }, [isSplicing, onSplicingChange])

  useEffect(() => {
    return () => {
      onSplicingChange?.(false)
    }
  }, [onSplicingChange])

  function handleModeChange(newMode: SpliceMode) {
    setMode(newMode)
    setValidationError(null)
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const validation = validateSpliceParameters(mode, inputValue, lengthUnit, sizeUnit, video)
    if (validation.value === null) {
      setValidationError(validation.error)
      return
    }

    setValidationError(null)
    void startSplice(mode, validation.value)
  }

  function handleSpliceAgain() {
    reset()
    setValidationError(null)
  }

  return (
    <section
      role="region"
      aria-label="Splice video parameters and results"
      className="border-2 border-solid border-cyber-border bg-white p-8 text-center transition-colors"
      data-testid="splice-panel"
    >
      {/* Top Navigation & Context Bar */}
      {onBack && (
        <div className="mb-4 flex items-center justify-between border-b border-dashed border-cyber-border/80 pb-3">
          <button
            type="button"
            onClick={onBack}
            className="inline-flex items-center gap-1.5 border border-cyber-border bg-slate-50 px-3 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-dark hover:bg-slate-100 transition-colors cursor-pointer"
            data-testid="splice-back-btn"
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
          <span>[ video.splicer // splice ]</span>
        </span>
        <span className="text-cyber-pink font-semibold">
          {spliceState.status === 'idle' && '01 // PARAMETERS (｡•̀ᴗ-)✧'}
          {(spliceState.status === 'triggering' || spliceState.status === 'polling') &&
            '02 // PROCESSING ( •̀_•́ )'}
          {spliceState.status === 'ready' && '03 // READY (｡•̀ᴗ-)✧'}
          {spliceState.status === 'failed' && '04 // FAILED (；´Д｀)'}
          {spliceState.status === 'error' && 'ERROR (×_×)'}
        </span>
      </div>

      {/* IDLE: Splice Parameters Form */}
      {spliceState.status === 'idle' && (
        <form onSubmit={handleSubmit} className="flex flex-col items-center gap-5" data-testid="splice-form">
          <div className="w-full max-w-md text-left">
            <label className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              1. Choose Splice Mode:
            </label>
            <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Splice mode">
              <button
                type="button"
                role="radio"
                aria-checked={mode === 'ByLength'}
                onClick={() => handleModeChange('ByLength')}
                className={`border px-4 py-2 text-sm font-mono transition-colors cursor-pointer ${
                  mode === 'ByLength'
                    ? 'border-cyber-cyan bg-cyber-cyan/15 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="mode-by-length"
              >
                By Length
              </button>
              <button
                type="button"
                role="radio"
                aria-checked={mode === 'BySize'}
                onClick={() => handleModeChange('BySize')}
                className={`border px-4 py-2 text-sm font-mono transition-colors cursor-pointer ${
                  mode === 'BySize'
                    ? 'border-cyber-cyan bg-cyber-cyan/15 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="mode-by-size"
              >
                By Size
              </button>
            </div>
          </div>

          {/* Unit Selector Toggle */}
          <div className="w-full max-w-md text-left">
            <label className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              2. Unit:
            </label>
            {mode === 'ByLength' ? (
              <div className="flex gap-2" role="group" aria-label="Length units">
                <button
                  type="button"
                  onClick={() => setLengthUnit('s')}
                  className={`px-3 py-1 text-xs font-mono border transition-colors ${
                    lengthUnit === 's'
                      ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                      : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                  }`}
                  data-testid="unit-seconds"
                >
                  Seconds (s)
                </button>
                <button
                  type="button"
                  onClick={() => setLengthUnit('mm:ss')}
                  className={`px-3 py-1 text-xs font-mono border transition-colors ${
                    lengthUnit === 'mm:ss'
                      ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                      : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                  }`}
                  data-testid="unit-mmss"
                >
                  Minutes:Seconds (mm:ss)
                </button>
              </div>
            ) : (
              <div className="flex gap-2" role="group" aria-label="Size units">
                <button
                  type="button"
                  onClick={() => setSizeUnit('MB')}
                  className={`px-3 py-1 text-xs font-mono border transition-colors ${
                    sizeUnit === 'MB'
                      ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                      : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                  }`}
                  data-testid="unit-mb"
                >
                  MB (Megabytes)
                </button>
                <button
                  type="button"
                  onClick={() => setSizeUnit('KB')}
                  className={`px-3 py-1 text-xs font-mono border transition-colors ${
                    sizeUnit === 'KB'
                      ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                      : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                  }`}
                  data-testid="unit-kb"
                >
                  KB (Kilobytes)
                </button>
                <button
                  type="button"
                  onClick={() => setSizeUnit('B')}
                  className={`px-3 py-1 text-xs font-mono border transition-colors ${
                    sizeUnit === 'B'
                      ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                      : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                  }`}
                  data-testid="unit-b"
                >
                  Bytes (B)
                </button>
              </div>
            )}
          </div>

          {/* Value Input */}
          <div className="w-full max-w-md text-left">
            <label htmlFor="splice-value-input" className="block text-xs font-mono text-slate-600 mb-1 uppercase tracking-wide">
              3. Enter Segment Value:
            </label>
            <input
              id="splice-value-input"
              type="text"
              value={inputValue}
              onChange={(e) => {
                setInputValue(e.target.value)
                setValidationError(null)
              }}
              placeholder={
                mode === 'ByLength'
                  ? lengthUnit === 's'
                    ? 'e.g. 30'
                    : 'e.g. 01:30'
                  : sizeUnit === 'MB'
                  ? 'e.g. 5'
                  : sizeUnit === 'KB'
                  ? 'e.g. 5120'
                  : 'e.g. 5242880'
              }
              className="w-full border-2 border-cyber-border bg-slate-50 px-3 py-2 font-mono text-sm text-slate-800 placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors"
              data-testid="splice-value-input"
            />
            <p className="mt-1 text-xs font-mono text-slate-400" data-testid="splice-helper-bounds">
              {mode === 'ByLength' ? (
                <>Min: 1s &middot; Max: {video.durationSeconds !== null ? `${video.durationSeconds.toFixed(1)}s` : 'source length'}</>
              ) : (
                <>Min: 1MB &middot; Max: {formatFileSize(video.sizeBytes)}</>
              )}
            </p>
          </div>

          {/* Validation Error Message */}
          {validationError && (
            <p className="text-sm text-red-600 font-mono" role="alert" data-testid="splice-validation-error">
              <span className="mr-1" aria-hidden="true">(；´Д｀)</span>
              {validationError}
            </p>
          )}

          {/* Submit Trigger Button */}
          <button
            type="submit"
            className="mt-2 bg-cyber-cyan px-6 py-2.5 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
            data-testid="start-splice-btn"
          >
            Start Splicing ✦
          </button>
        </form>
      )}

      {/* TRIGGERING / POLLING: Progress View */}
      {(spliceState.status === 'triggering' || spliceState.status === 'polling') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="polite" data-testid="splice-progress">
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

          <p className="text-slate-800 font-medium">
            Splicing {video.fileName}...
          </p>

          <div className="inline-flex items-center gap-2 px-3 py-1 bg-amber-50 border border-amber-200 text-amber-800 text-xs font-mono">
            <span className="h-2 w-2 rounded-full bg-amber-500 animate-pulse" />
            <span data-testid="splice-job-status">
              {spliceState.status === 'triggering'
                ? 'Status: Initializing job ( ๑>ᴗ<๑ )'
                : spliceState.jobStatus === 'Pending'
                ? 'Status: Pending (queued in line...) ( •̀_•́ )'
                : 'Status: Processing (splicing frames...) ( •̀_•́ )'}
            </span>
          </div>

          <p className="text-xs font-mono text-slate-400">
            Please wait while the media worker generates segments (´｡• ᵕ •｡`)
          </p>
        </div>
      )}

      {/* READY: Resulting Segments Display */}
      {spliceState.status === 'ready' && (
        <div className="flex flex-col items-center gap-4 py-2" aria-live="polite" data-testid="splice-results">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
            </svg>
          </div>

          <p className="text-slate-800 font-semibold text-lg">
            Splicing Complete! (｡•̀ᴗ-)✧
          </p>

          <div className="inline-flex items-center gap-2 px-3 py-1 bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-mono">
            <span className="h-2 w-2 rounded-full bg-emerald-500" />
            <span data-testid="splice-ready-badge">
              Generated {spliceState.segments.length} segment{spliceState.segments.length === 1 ? '' : 's'}
            </span>
          </div>

          {/* Segments List / Table */}
          <div className="w-full max-w-lg overflow-hidden border border-cyber-border bg-slate-50 text-left font-mono text-xs">
            <div className="grid grid-cols-12 bg-slate-100 p-2 font-semibold text-slate-600 border-b border-cyber-border">
              <span className="col-span-1 text-center">#</span>
              <span className="col-span-6">Segment Name</span>
              <span className="col-span-3">Duration</span>
              <span className="col-span-2 text-right">Size</span>
            </div>
            <div className="divide-y divide-cyber-border" data-testid="segments-list">
              {spliceState.segments.length === 0 ? (
                <div className="p-4 text-center text-slate-400">No output segments returned</div>
              ) : (
                spliceState.segments.map((segment: SpliceSegment, idx: number) => (
                  <div
                    key={segment.name + idx}
                    className="grid grid-cols-12 items-center p-2.5 text-slate-700 hover:bg-slate-100/60 transition-colors"
                    data-testid="segment-item"
                  >
                    <span className="col-span-1 text-center text-slate-400 font-bold">{idx + 1}</span>
                    <span className="col-span-6 truncate font-medium text-slate-800" data-testid="segment-name">
                      {segment.name}
                    </span>
                    <span className="col-span-3 text-slate-600" data-testid="segment-duration">
                      {formatDuration(segment.duration)} ({segment.duration.toFixed(1)}s)
                    </span>
                    <span className="col-span-2 text-right font-medium text-slate-700" data-testid="segment-size">
                      {formatFileSize(segment.size)}
                    </span>
                  </div>
                ))
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={handleSpliceAgain}
            className="mt-2 bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
            data-testid="splice-again-btn"
          >
            Splice again (• ◡•) ✦
          </button>
        </div>
      )}

      {/* FAILED / ERROR: Error Display and Retry */}
      {(spliceState.status === 'failed' || spliceState.status === 'error') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="assertive" data-testid="splice-error">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-red-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>

          <p className="text-slate-800 font-medium">Splice operation failed (；´Д｀)</p>

          <div className="flex gap-3 mt-2">
            <button
              type="button"
              onClick={retry}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="splice-retry-btn"
            >
              Retry ( -_ -)
            </button>
            <button
              type="button"
              onClick={handleSpliceAgain}
              className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="splice-adjust-btn"
            >
              Adjust parameters (• ◡•)
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
