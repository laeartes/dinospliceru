import { useState, useEffect, type FormEvent } from 'react'
import type { VideoResponse } from '../api/videoApi'
import {
  getCompressionDownloadUrl,
  type EncodingPresetOption,
  type OutputContainerFormat,
  type VideoCodecOption,
} from '../api/compressionApi'
import { useCompressionJob } from '../hooks/useCompressionJob'
import { useToast } from '../hooks/useToast'
import { formatFileSize } from '../utils/formatFileSize'
import { formatTimestamp } from '../utils/cutValidation'
import {
  MIN_CRF,
  RESOLUTION_PRESET_DIMENSIONS,
  calculateSizeSavings,
  estimateCompressionForTargetSize,
  getCompatibleCodec,
  getCrfQualityHint,
  getMaxCrf,
  validateCompressionParameters,
  type ResolutionPreset,
} from '../utils/compressionValidation'

interface CompressionPanelProps {
  video: VideoResponse
  onBack?: () => void
  onCompressingChange?: (isCompressing: boolean) => void
}

const PRESET_OPTIONS: ReadonlyArray<{ value: EncodingPresetOption; hint: string }> = [
  { value: 'Ultrafast', hint: 'Fastest, largest file' },
  { value: 'Fast', hint: 'Quick encode' },
  { value: 'Medium', hint: 'Balanced (｡•̀ᴗ-)✧' },
  { value: 'Slow', hint: 'Slowest, smallest file' },
]
const AUDIO_BITRATE_OPTIONS: ReadonlyArray<{ label: string; value: number | null; hint: string }> = [
  { label: 'Original', value: null, hint: 'Keep source audio' },
  { label: '64 kbps', value: 64, hint: 'Voice / smallest' },
  { label: '96 kbps', value: 96, hint: 'Compact' },
  { label: '128 kbps', value: 128, hint: 'Standard (｡•̀ᴗ-)✧' },
  { label: '192 kbps', value: 192, hint: 'High quality' },
  { label: '256 kbps', value: 256, hint: 'Very high quality' },
  { label: '320 kbps', value: 320, hint: 'Near-transparent' },
]
const RESOLUTION_PRESETS: ReadonlyArray<{ id: ResolutionPreset; label: string; tip: string }> = [
  { id: 'original', label: 'Original', tip: 'Keep original resolution' },
  { id: '1080p', label: '1080p', tip: 'Downscale to 1920x1080' },
  { id: '720p', label: '720p', tip: 'Downscale to 1280x720' },
  { id: '480p', label: '480p', tip: 'Downscale to 854x480' },
  { id: 'custom', label: 'Custom', tip: 'Custom even WxH' },
]

function HoverTip({ text }: { text: string }) {
  return (
    <span
      role="tooltip"
      className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-1.5 -translate-x-1/2 whitespace-nowrap border border-cyber-cyan bg-cyber-dark px-2 py-1 font-mono text-[10px] font-normal normal-case tracking-normal text-white opacity-0 shadow-sm transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
    >
      {text}
    </span>
  )
}

export default function CompressionPanel({
  video,
  onBack,
  onCompressingChange,
}: CompressionPanelProps) {
  const [format, setFormat] = useState<OutputContainerFormat>('Mp4')
  const [videoCodec, setVideoCodec] = useState<VideoCodecOption>('H264')
  const [crfInput, setCrfInput] = useState('23')
  const [resolutionPreset, setResolutionPreset] = useState<ResolutionPreset>('original')
  const [customWidthInput, setCustomWidthInput] = useState(() =>
    video.resolution ? String(video.resolution.width) : ''
  )
  const [customHeightInput, setCustomHeightInput] = useState(() =>
    video.resolution ? String(video.resolution.height) : ''
  )
  const [preset, setPreset] = useState<EncodingPresetOption>('Medium')
  const [audioBitrateKbps, setAudioBitrateKbps] = useState<number | null>(128)
  const [targetSizeMbInput, setTargetSizeMbInput] = useState('')
  const [showAdvanced, setShowAdvanced] = useState(false)

  const { compressionState, startCompression, reset, retry } = useCompressionJob(video.id)
  const { showError } = useToast()

  useEffect(() => {
    if (compressionState.status === 'error' || compressionState.status === 'failed') {
      showError({ error: compressionState.error, code: compressionState.code })
    }
  }, [compressionState, showError])

  const isCompressing =
    compressionState.status === 'triggering' || compressionState.status === 'polling'

  useEffect(() => {
    onCompressingChange?.(isCompressing)
  }, [isCompressing, onCompressingChange])

  useEffect(() => () => onCompressingChange?.(false), [onCompressingChange])

  const maxCrf = getMaxCrf(videoCodec)
  const parsedCrf = /^\d+$/.test(crfInput.trim()) ? Number(crfInput.trim()) : 23
  const sliderCrf = Math.min(Math.max(parsedCrf, MIN_CRF), maxCrf)

  const validation = validateCompressionParameters(
    {
      format,
      videoCodec,
      crfInput,
      resolutionPreset,
      customWidthInput,
      customHeightInput,
      preset,
      audioBitrateKbps,
    },
    video.resolution
  )

  const sourceSizeMb = video.sizeBytes / (1024 * 1024)
  const halfSizeMb = Math.max(0.5, Math.round(sourceSizeMb * 5) / 10)
  const parsedTargetMb =
    /^\d+(\.\d+)?$/.test(targetSizeMbInput.trim()) ? Number(targetSizeMbInput.trim()) : null
  const targetSizeInvalid =
    targetSizeMbInput.trim() !== '' && (parsedTargetMb === null || parsedTargetMb <= 0)

  function applyTargetSize(rawValue: string, codecOverride?: VideoCodecOption) {
    setTargetSizeMbInput(rawValue)
    const trimmed = rawValue.trim()
    if (!/^\d+(\.\d+)?$/.test(trimmed)) return
    const targetMb = Number(trimmed)
    const estimate = estimateCompressionForTargetSize(
      targetMb,
      video.sizeBytes,
      video.resolution,
      codecOverride ?? videoCodec
    )
    if (!estimate) return
    setCrfInput(String(estimate.crf))
    setResolutionPreset(estimate.resolutionPreset)
    setAudioBitrateKbps(estimate.audioBitrateKbps)
    setPreset(estimate.preset)
  }

  function handleFormatChange(nextFormat: OutputContainerFormat) {
    const nextCodec = getCompatibleCodec(nextFormat)
    const nextMaxCrf = getMaxCrf(nextCodec)
    setFormat(nextFormat)
    setVideoCodec(nextCodec)
    if (parsedTargetMb !== null && parsedTargetMb > 0) {
      applyTargetSize(targetSizeMbInput, nextCodec)
      return
    }
    if (/^\d+$/.test(crfInput.trim()) && Number(crfInput.trim()) > nextMaxCrf) {
      setCrfInput(String(nextMaxCrf))
    }
  }

  function isPresetLargerThanSource(id: ResolutionPreset): boolean {
    if (id === 'original' || id === 'custom' || !video.resolution) return false
    const dims = RESOLUTION_PRESET_DIMENSIONS[id]
    return dims.width > video.resolution.width || dims.height > video.resolution.height
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!validation.request) return
    void startCompression(validation.request)
  }

  const progressPercent =
    compressionState.status === 'polling'
      ? Math.min(100, Math.max(0, compressionState.job.progressPercent ?? 0))
      : 0

  const savings =
    compressionState.status === 'ready' && compressionState.job.outputSizeBytes !== null
      ? calculateSizeSavings(video.sizeBytes, compressionState.job.outputSizeBytes)
      : null

  return (
    <section
      role="region"
      aria-label="Compress video parameters and results"
      className="border-2 border-solid border-cyber-border bg-white p-8 text-center transition-colors"
      data-testid="compression-panel"
    >
      {onBack && (
        <div className="mb-4 flex items-center justify-between border-b border-dashed border-cyber-border/80 pb-3">
          <button
            type="button"
            onClick={onBack}
            className="inline-flex items-center gap-1.5 border border-cyber-border bg-slate-50 px-3 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-dark hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer"
            data-testid="compression-back-btn"
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

      <div className="mb-6 flex items-center justify-between border-b border-cyber-border pb-3 text-xs font-mono text-slate-500">
        <span className="flex items-center gap-1.5 text-cyber-dark">
          <span className="text-cyber-pink" aria-hidden="true">✦</span>
          <span>[ video.splicer // compress ]</span>
        </span>
        <span className="text-cyber-pink font-semibold">
          {compressionState.status === 'idle' && '01 // SETTINGS (｡•̀ᴗ-)✧'}
          {(compressionState.status === 'triggering' || compressionState.status === 'polling') &&
            '02 // ENCODING ( •̀_•́ )'}
          {compressionState.status === 'ready' && '03 // READY (｡•̀ᴗ-)✧'}
          {compressionState.status === 'failed' && '04 // FAILED (；´Д｀)'}
          {compressionState.status === 'error' && 'ERROR (×_×)'}
        </span>
      </div>

      {compressionState.status === 'idle' && (
        <form
          onSubmit={handleSubmit}
          className="flex flex-col items-center gap-5"
          data-testid="compression-form"
        >
          {/* Quick Target Size Estimator (est. MB) */}
          <div className="w-full max-w-md border-2 border-cyber-border bg-slate-50/80 p-4 text-left">
            <div className="mb-2 flex items-center justify-between gap-2">
              <label
                htmlFor="compress-target-size-input"
                className="text-xs font-mono font-semibold text-cyber-dark uppercase tracking-wide"
              >
                <span className="text-cyber-pink mr-1" aria-hidden="true">♡</span>
                Target Size (approx. MB)
              </label>
              <span className="font-mono text-[11px] text-slate-400">
                Source: {formatFileSize(video.sizeBytes)}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <input
                id="compress-target-size-input"
                type="text"
                inputMode="decimal"
                value={targetSizeMbInput}
                onChange={(e) => applyTargetSize(e.target.value)}
                placeholder={`e.g. ${halfSizeMb}`}
                className="w-full border-2 border-cyber-border bg-white px-3 py-2 font-mono text-sm text-slate-800 placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors"
                data-testid="target-size-input"
              />
              <span className="border-2 border-cyber-border bg-white px-3 py-2 font-mono text-xs font-semibold text-slate-500">
                MB
              </span>
            </div>

            <div className="mt-2.5 flex flex-wrap gap-2" role="group" aria-label="Quick target size presets">
              <button
                type="button"
                disabled={sourceSizeMb <= 8}
                onClick={() => applyTargetSize('8')}
                className="border border-cyber-border bg-white px-2.5 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-pink hover:bg-cyber-pink/10 hover:-translate-y-0.5 disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:translate-y-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-all cursor-pointer"
                data-testid="target-chip-8mb"
              >
                ♡ 8MB (Discord)
              </button>
              <button
                type="button"
                disabled={sourceSizeMb <= 25}
                onClick={() => applyTargetSize('25')}
                className="border border-cyber-border bg-white px-2.5 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-teal hover:bg-cyber-teal/10 hover:-translate-y-0.5 disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:translate-y-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-teal transition-all cursor-pointer"
                data-testid="target-chip-25mb"
              >
                ✧ 25MB
              </button>
              <button
                type="button"
                onClick={() => applyTargetSize(String(halfSizeMb))}
                className="border border-cyber-border bg-white px-2.5 py-1 font-mono text-xs text-cyber-dark hover:border-cyber-cyan hover:bg-cyber-cyan/15 hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-all cursor-pointer"
                data-testid="target-chip-50pct"
              >
                ★ 50% ({halfSizeMb}MB)
              </button>
            </div>

            {targetSizeInvalid && (
              <p className="mt-2 font-mono text-xs text-red-600" role="alert" data-testid="target-size-error">
                Enter a positive MB number (；´Д｀)
              </p>
            )}

            <div
              className="mt-3 border border-dashed border-cyber-border bg-white px-3 py-2 font-mono text-[11px] text-slate-600"
              data-testid="compression-active-summary"
            >
              <span className="text-cyber-pink font-semibold mr-1" aria-hidden="true">✦</span>
              <span>
                {parsedTargetMb !== null && parsedTargetMb > 0 ? 'Auto-picked: ' : 'Active: '}
                {format.toUpperCase()} &middot; CRF {crfInput.trim() || '—'} &middot;{' '}
                {resolutionPreset === 'original' ? 'Original' : resolutionPreset} &middot;{' '}
                {audioBitrateKbps === null ? 'Original audio' : `${audioBitrateKbps} kbps`} &middot; {preset}{' '}
                <span className="text-cyber-pink">(｡•̀ᴗ-)✧</span>
              </span>
            </div>
            <p className="mt-1.5 font-mono text-[10px] text-slate-400">
              Best-effort CRF estimate — actual size varies with motion &amp; detail (´｡• ᵕ •｡`)
            </p>
          </div>

          {/* Collapsible Advanced Settings Toggle */}
          <div className="w-full max-w-md text-left">
            <button
              type="button"
              aria-expanded={showAdvanced}
              aria-controls="compression-advanced-settings"
              onClick={() => setShowAdvanced((prev) => !prev)}
              className="flex w-full items-center justify-between border border-dashed border-cyber-border bg-white px-3 py-2 font-mono text-xs text-cyber-dark hover:border-cyber-cyan hover:bg-cyber-cyan/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer"
              data-testid="toggle-advanced-btn"
            >
              <span className="flex items-center gap-1.5">
                <span className="text-cyber-pink" aria-hidden="true">✧</span>
                <span>Advanced settings (codec, resolution, CRF, speed, audio)</span>
              </span>
              <span className="text-slate-500" aria-hidden="true">
                {showAdvanced ? '▴' : '▾'}
              </span>
            </button>
          </div>

          {showAdvanced && (
            <div
              id="compression-advanced-settings"
              className="flex w-full max-w-md flex-col items-center gap-5 border border-cyber-border bg-slate-50/40 p-4"
              data-testid="compression-advanced-settings"
            >
          {/* Container Format & Codec */}
          <div className="w-full max-w-md text-left">
            <p className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              1. Container &amp; Codec:
            </p>
            <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Container format">
              <button
                type="button"
                role="radio"
                aria-checked={format === 'Mp4'}
                onClick={() => handleFormatChange('Mp4')}
                className={`border px-3 py-2 text-left font-mono focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors cursor-pointer ${
                  format === 'Mp4'
                    ? 'border-cyber-cyan bg-cyber-cyan/15 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="format-mp4"
              >
                <span className="block text-sm">MP4 / H.264</span>
                <span className="block text-[11px] font-normal text-slate-500">
                  Universal compatibility ✦
                </span>
              </button>
              <button
                type="button"
                role="radio"
                aria-checked={format === 'Webm'}
                onClick={() => handleFormatChange('Webm')}
                className={`border px-3 py-2 text-left font-mono focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors cursor-pointer ${
                  format === 'Webm'
                    ? 'border-cyber-pink bg-cyber-pink/10 text-cyber-dark font-bold'
                    : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                }`}
                data-testid="format-webm"
              >
                <span className="block text-sm">WebM / VP9</span>
                <span className="block text-[11px] font-normal text-slate-500">
                  Web-optimized compression ✦
                </span>
              </button>
            </div>
            <p className="mt-1 text-[11px] font-mono text-slate-400" data-testid="selected-codec-info">
              Active codec: {videoCodec} (CRF {MIN_CRF}–{maxCrf})
            </p>
          </div>

          {/* Resolution Preset */}
          <div className="w-full max-w-md text-left">
            <p className="block text-xs font-mono text-slate-600 mb-2 uppercase tracking-wide">
              2. Resolution:
            </p>
            <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Resolution preset">
              {RESOLUTION_PRESETS.map((option) => {
                const disabled = isPresetLargerThanSource(option.id)
                return (
                  <button
                    key={option.id}
                    type="button"
                    role="radio"
                    aria-checked={resolutionPreset === option.id}
                    disabled={disabled}
                    onClick={() => setResolutionPreset(option.id)}
                    className={`group relative px-3 py-1 text-xs font-mono border focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors ${
                      resolutionPreset === option.id
                        ? 'border-cyber-cyan bg-cyber-cyan text-cyber-dark font-semibold'
                        : 'border-cyber-border bg-white text-slate-600 hover:border-slate-400'
                    } disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer`}
                    data-testid={`resolution-${option.id}`}
                  >
                    {option.label}
                    <HoverTip text={option.tip} />
                  </button>
                )
              })}
            </div>

            {resolutionPreset === 'custom' && (
              <div className="mt-3 grid grid-cols-2 gap-3">
                <div>
                  <label htmlFor="compress-width-input" className="block text-xs font-mono text-slate-500 mb-1">
                    Width (even px)
                  </label>
                  <input
                    id="compress-width-input"
                    type="text"
                    inputMode="numeric"
                    value={customWidthInput}
                    onChange={(e) => setCustomWidthInput(e.target.value)}
                    placeholder="1280"
                    className="w-full border-2 border-cyber-border bg-slate-50 px-3 py-1.5 font-mono text-sm text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan"
                    data-testid="custom-width-input"
                  />
                </div>
                <div>
                  <label htmlFor="compress-height-input" className="block text-xs font-mono text-slate-500 mb-1">
                    Height (even px)
                  </label>
                  <input
                    id="compress-height-input"
                    type="text"
                    inputMode="numeric"
                    value={customHeightInput}
                    onChange={(e) => setCustomHeightInput(e.target.value)}
                    placeholder="720"
                    className="w-full border-2 border-cyber-border bg-slate-50 px-3 py-1.5 font-mono text-sm text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink"
                    data-testid="custom-height-input"
                  />
                </div>
              </div>
            )}
          </div>

          {/* CRF Quality */}
          <div className="w-full max-w-md text-left">
            <div className="flex items-center justify-between mb-1">
              <label
                htmlFor="compress-crf-input"
                className="group relative inline-block text-xs font-mono text-slate-600 uppercase tracking-wide cursor-help"
              >
                3. Quality (CRF {MIN_CRF}–{maxCrf}):
                <HoverTip text="Lower = sharper/larger, higher = smaller" />
              </label>
              <input
                id="compress-crf-input"
                type="text"
                inputMode="numeric"
                value={crfInput}
                onChange={(e) => setCrfInput(e.target.value)}
                className="w-16 border border-cyber-border bg-slate-50 px-2 py-0.5 text-right font-mono text-xs text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan"
                data-testid="crf-input"
              />
            </div>
            <input
              type="range"
              aria-label="CRF quality slider"
              min={MIN_CRF}
              max={maxCrf}
              step={1}
              value={sliderCrf}
              onChange={(e) => setCrfInput(e.target.value)}
              className="w-full accent-cyber-cyan cursor-pointer"
              data-testid="crf-slider"
            />
            <p className="mt-1 text-xs font-mono text-slate-400" data-testid="crf-hint">
              {getCrfQualityHint(sliderCrf, videoCodec)}
            </p>
          </div>

          {/* Preset & Audio Bitrate */}
          <div className="w-full max-w-md grid grid-cols-2 gap-3 text-left">
            <div>
              <label
                htmlFor="compress-preset-select"
                className="group relative inline-block text-xs font-mono text-slate-600 mb-1 uppercase tracking-wide cursor-help"
              >
                4. Speed Preset:
                <HoverTip text="Slower = smaller file, longer encode" />
              </label>
              <select
                id="compress-preset-select"
                value={preset}
                onChange={(e) => setPreset(e.target.value as EncodingPresetOption)}
                className="w-full border-2 border-cyber-border bg-slate-50 px-2.5 py-2 font-mono text-xs text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan"
                data-testid="preset-select"
              >
                {PRESET_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.value}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-[11px] font-mono text-slate-400" data-testid="preset-hint">
                {PRESET_OPTIONS.find((o) => o.value === preset)?.hint}
              </p>
            </div>
            <div>
              <label
                htmlFor="compress-audio-select"
                className="group relative inline-block text-xs font-mono text-slate-600 mb-1 uppercase tracking-wide cursor-help"
              >
                5. Audio Bitrate:
                <HoverTip text="Higher = clearer audio, larger file" />
              </label>
              <select
                id="compress-audio-select"
                value={audioBitrateKbps === null ? 'original' : String(audioBitrateKbps)}
                onChange={(e) =>
                  setAudioBitrateKbps(e.target.value === 'original' ? null : Number(e.target.value))
                }
                className="w-full border-2 border-cyber-border bg-slate-50 px-2.5 py-2 font-mono text-xs text-slate-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink"
                data-testid="audio-bitrate-select"
              >
                {AUDIO_BITRATE_OPTIONS.map((option) => (
                  <option
                    key={option.label}
                    value={option.value === null ? 'original' : String(option.value)}
                  >
                    {option.label}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-[11px] font-mono text-slate-400" data-testid="audio-bitrate-hint">
                {AUDIO_BITRATE_OPTIONS.find((o) => o.value === audioBitrateKbps)?.hint}
              </p>
            </div>
          </div>
            </div>
          )}

          {validation.error && (
            <p className="text-sm text-red-600 font-mono" role="alert" data-testid="compression-validation-error">
              {validation.error}
            </p>
          )}

          <button
            type="submit"
            disabled={validation.error !== null || targetSizeInvalid}
            className="mt-1 bg-cyber-cyan px-6 py-2.5 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white hover:-translate-y-0.5 hover:shadow-[3px_3px_0px_#373b3e] disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:translate-y-0 disabled:hover:shadow-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-all cursor-pointer"
            data-testid="start-compression-btn"
          >
            Compress Video (｡•̀ᴗ-)✧ ✦
          </button>
        </form>
      )}

      {(compressionState.status === 'triggering' || compressionState.status === 'polling') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="polite" data-testid="compression-progress">
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

          <p className="text-slate-800 font-medium">Compressing {video.fileName}...</p>

          <div className="inline-flex items-center gap-2 px-3 py-1 bg-amber-50 border border-amber-200 text-amber-800 text-xs font-mono">
            <span className="h-2 w-2 rounded-full bg-amber-500 animate-pulse" />
            <span data-testid="compression-job-status">
              {compressionState.status === 'triggering'
                ? 'Status: Initializing compression job ( ๑>ᴗ<๑ )'
                : compressionState.job.status === 'Pending'
                ? 'Status: Pending (queued in line...) ( •̀_•́ )'
                : 'Status: Processing (encoding video...) ( •̀_•́ )'}
            </span>
          </div>

          <div className="w-full max-w-md">
            <div className="flex justify-between text-xs font-mono mb-1 text-slate-600">
              <span>Encoding Progress</span>
              <span data-testid="compression-progress-percent">{progressPercent}%</span>
            </div>
            <div
              role="progressbar"
              aria-valuenow={progressPercent}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label="Compression job progress"
              className="h-3 w-full overflow-hidden bg-slate-100 border border-cyber-border"
              data-testid="compression-progress-bar"
            >
              <div
                className="h-full bg-cyber-cyan transition-all duration-200"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          </div>

          <p className="text-xs font-mono text-slate-400">
            Please wait while the media worker encodes your video (´｡• ᵕ •｡`)
          </p>
        </div>
      )}

      {compressionState.status === 'ready' && (
        <div className="flex flex-col items-center gap-4 py-2" aria-live="polite" data-testid="compression-results">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
            </svg>
          </div>

          <p className="text-slate-800 font-semibold text-lg">Compression Complete! (｡•̀ᴗ-)✧</p>

          <div className="w-full max-w-md border border-cyber-border bg-slate-50 p-4 text-left font-mono text-xs space-y-2">
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">Filename:</span>
              <span className="font-semibold text-slate-800 truncate" data-testid="compression-output-filename">
                {compressionState.job.outputFileName ?? 'compressed-video'}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">Duration:</span>
              <span className="font-semibold text-slate-700" data-testid="compression-output-duration">
                {compressionState.job.outputDurationSeconds !== null
                  ? `${formatTimestamp(compressionState.job.outputDurationSeconds)} (${compressionState.job.outputDurationSeconds.toFixed(1)}s)`
                  : '—'}
              </span>
            </div>
            <div className="flex justify-between gap-2">
              <span className="text-slate-400">Size Comparison:</span>
              <span className="font-semibold text-slate-700" data-testid="compression-output-size">
                {compressionState.job.outputSizeBytes !== null
                  ? `${formatFileSize(compressionState.job.outputSizeBytes)} vs ${formatFileSize(video.sizeBytes)}`
                  : '—'}
              </span>
            </div>
            {savings !== null && (
              <div className="flex justify-between gap-2">
                <span className="text-slate-400">Space Saved:</span>
                <span
                  className={`font-semibold ${savings.percentSaved >= 0 ? 'text-emerald-700' : 'text-amber-700'}`}
                  data-testid="compression-size-savings"
                >
                  {savings.percentSaved >= 0
                    ? `${savings.percentSaved}% saved (${formatFileSize(savings.savedBytes)}) (｡•̀ᴗ-)✧`
                    : `${Math.abs(savings.percentSaved)}% larger`}
                </span>
              </div>
            )}
          </div>

          <div className="mt-2 flex flex-wrap items-center justify-center gap-3">
            <a
              href={getCompressionDownloadUrl(compressionState.job.jobId)}
              download={compressionState.job.outputFileName ?? undefined}
              className="bg-cyber-cyan px-5 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
              data-testid="compression-download-link"
            >
              Download Compressed ⤓ (｡•̀ᴗ-)✧
            </a>
            <button
              type="button"
              onClick={reset}
              className="border border-cyber-border bg-white px-4 py-2 font-medium text-cyber-dark hover:border-cyber-pink hover:bg-cyber-pink/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="compress-again-btn"
            >
              Compress again (• ◡•) ✦
            </button>
          </div>
        </div>
      )}

      {(compressionState.status === 'failed' || compressionState.status === 'error') && (
        <div className="flex flex-col items-center gap-4 py-4" aria-live="assertive" data-testid="compression-error">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-red-100 text-red-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>

          <p className="text-slate-800 font-medium">Compression failed (；´Д｀)</p>

          <p className="text-sm text-red-600 font-mono" role="alert" data-testid="compression-error-message">
            <span className="mr-1" aria-hidden="true">(；´Д｀)</span>
            {compressionState.error}
          </p>

          <div className="flex gap-3 mt-2">
            <button
              type="button"
              onClick={retry}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="compression-retry-btn"
            >
              Retry ( -_ -)
            </button>
            <button
              type="button"
              onClick={reset}
              className="border border-cyber-border px-4 py-2 text-cyber-dark hover:border-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2 transition-colors cursor-pointer"
              data-testid="compression-adjust-btn"
            >
              Adjust settings (• ◡•)
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
