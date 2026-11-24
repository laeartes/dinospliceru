import type {
  CompressionRequest,
  EncodingPresetOption,
  OutputContainerFormat,
  VideoCodecOption,
} from '../api/compressionApi'
import type { Resolution } from '../api/videoApi'

export const MIN_CRF = 0
export const MAX_H264_CRF = 51
export const MAX_VP9_CRF = 63
export const MIN_AUDIO_BITRATE_KBPS = 32
export const MAX_AUDIO_BITRATE_KBPS = 512

export type ResolutionPreset = 'original' | '1080p' | '720p' | '480p' | 'custom'

export const RESOLUTION_PRESET_DIMENSIONS: Record<
  Exclude<ResolutionPreset, 'original' | 'custom'>,
  Resolution
> = {
  '1080p': { width: 1920, height: 1080 },
  '720p': { width: 1280, height: 720 },
  '480p': { width: 854, height: 480 },
}

export function getCompatibleCodec(format: OutputContainerFormat): VideoCodecOption {
  return format === 'Webm' ? 'Vp9' : 'H264'
}

export function isCompatibleFormatCodec(
  format: OutputContainerFormat,
  codec: VideoCodecOption
): boolean {
  return (format === 'Mp4' && codec === 'H264') || (format === 'Webm' && codec === 'Vp9')
}

export function getMaxCrf(codec: VideoCodecOption): number {
  return codec === 'H264' ? MAX_H264_CRF : MAX_VP9_CRF
}

export function getCrfQualityHint(crf: number, codec: VideoCodecOption): string {
  if (crf <= 0) return 'Lossless (huge file)'
  const highCutoff = codec === 'H264' ? 18 : 22
  const balancedCutoff = codec === 'H264' ? 28 : 35
  const smallCutoff = codec === 'H264' ? 38 : 48
  if (crf <= highCutoff) return 'High quality (larger file)'
  if (crf <= balancedCutoff) return 'Balanced quality & size (｡•̀ᴗ-)✧'
  if (crf <= smallCutoff) return 'Smaller file (visible compression)'
  return 'Lowest quality (smallest file)'
}

export interface CompressionFormInput {
  format: OutputContainerFormat
  videoCodec: VideoCodecOption
  crfInput: string
  resolutionPreset: ResolutionPreset
  customWidthInput: string
  customHeightInput: string
  preset: EncodingPresetOption
  audioBitrateKbps: number | null
}

export type CompressionValidationResult =
  | { request: CompressionRequest; error: null }
  | { request: null; error: string }

function parseDimension(raw: string): number | null {
  const trimmed = raw.trim()
  if (!/^\d+$/.test(trimmed)) return null
  return Number(trimmed)
}

function validateDimension(name: string, value: number, maxSource: number | undefined): string | null {
  if (value <= 0) return `${name} must be a positive integer (；´Д｀)`
  if (value % 2 !== 0) return `${name} must be an even number (divisible by 2) (；´Д｀)`
  if (maxSource !== undefined && value > maxSource) {
    return `${name} (${value}) cannot exceed source dimension (${maxSource}) (；´Д｀)`
  }
  return null
}

/** Validates compression form inputs against backend codec, CRF, resolution, and audio rules. */
export function validateCompressionParameters(
  input: CompressionFormInput,
  sourceResolution: Resolution | null
): CompressionValidationResult {
  if (!isCompatibleFormatCodec(input.format, input.videoCodec)) {
    return {
      request: null,
      error: `Format ${input.format} is not compatible with codec ${input.videoCodec} (；´Д｀)`,
    }
  }

  const trimmedCrf = input.crfInput.trim()
  const maxCrf = getMaxCrf(input.videoCodec)
  if (!/^-?\d+$/.test(trimmedCrf)) {
    return { request: null, error: `CRF must be a whole number between ${MIN_CRF} and ${maxCrf} (；´Д｀)` }
  }

  const crf = Number(trimmedCrf)
  if (crf < MIN_CRF || crf > maxCrf) {
    return {
      request: null,
      error: `CRF must be between ${MIN_CRF} and ${maxCrf} for ${input.videoCodec} (；´Д｀)`,
    }
  }

  let targetWidth: number | null = null
  let targetHeight: number | null = null

  if (input.resolutionPreset === 'custom') {
    const w = parseDimension(input.customWidthInput)
    const h = parseDimension(input.customHeightInput)
    if (w === null || h === null) {
      return { request: null, error: 'Enter both custom width and height as positive even integers (；´Д｀)' }
    }
    const wErr = validateDimension('Width', w, sourceResolution?.width)
    if (wErr) return { request: null, error: wErr }
    const hErr = validateDimension('Height', h, sourceResolution?.height)
    if (hErr) return { request: null, error: hErr }
    targetWidth = w
    targetHeight = h
  } else if (input.resolutionPreset !== 'original') {
    const dims = RESOLUTION_PRESET_DIMENSIONS[input.resolutionPreset]
    const wErr = validateDimension('Width', dims.width, sourceResolution?.width)
    if (wErr) return { request: null, error: wErr }
    const hErr = validateDimension('Height', dims.height, sourceResolution?.height)
    if (hErr) return { request: null, error: hErr }
    targetWidth = dims.width
    targetHeight = dims.height
  }

  if (
    input.audioBitrateKbps !== null &&
    (input.audioBitrateKbps < MIN_AUDIO_BITRATE_KBPS || input.audioBitrateKbps > MAX_AUDIO_BITRATE_KBPS)
  ) {
    return {
      request: null,
      error: `Audio bitrate must be between ${MIN_AUDIO_BITRATE_KBPS} and ${MAX_AUDIO_BITRATE_KBPS} kbps (；´Д｀)`,
    }
  }

  return {
    request: {
      format: input.format,
      videoCodec: input.videoCodec,
      crf,
      targetWidth,
      targetHeight,
      audioBitrateKbps: input.audioBitrateKbps,
      preset: input.preset,
    },
    error: null,
  }
}

/** Computes bytes and percentage saved compared to the original file size. */
export function calculateSizeSavings(
  originalBytes: number,
  compressedBytes: number
): { savedBytes: number; percentSaved: number } {
  if (originalBytes <= 0) return { savedBytes: 0, percentSaved: 0 }
  const savedBytes = originalBytes - compressedBytes
  const percentSaved = Math.round((savedBytes / originalBytes) * 1000) / 10
  return { savedBytes, percentSaved }
}

export interface TargetSizeEstimate {
  crf: number
  resolutionPreset: Exclude<ResolutionPreset, 'custom'>
  audioBitrateKbps: number
  preset: EncodingPresetOption
}

/**
 * Estimates CRF, resolution downscale, and audio bitrate to roughly match a target size in MB.
 * Because CRF is content-dependent, the resulting file size is a best-effort approximation.
 */
export function estimateCompressionForTargetSize(
  targetSizeMb: number,
  sourceSizeBytes: number,
  sourceResolution: Resolution | null,
  codec: VideoCodecOption
): TargetSizeEstimate | null {
  if (!Number.isFinite(targetSizeMb) || targetSizeMb <= 0 || sourceSizeBytes <= 0) {
    return null
  }

  const targetBytes = targetSizeMb * 1024 * 1024
  const ratio = Math.min(1, targetBytes / sourceSizeBytes)

  // ~+6 CRF roughly halves bitrate for H.264 / VP9.
  const baseCrf = codec === 'H264' ? 23 : 28
  const maxEstimatedCrf = codec === 'H264' ? 46 : 58
  const crfDelta = ratio < 1 ? Math.round(-6 * Math.log2(ratio)) : 0
  const crf = Math.min(maxEstimatedCrf, Math.max(18, baseCrf + crfDelta))

  let resolutionPreset: Exclude<ResolutionPreset, 'custom'> = 'original'
  if (
    ratio < 0.15 &&
    sourceResolution &&
    (sourceResolution.width > 854 || sourceResolution.height > 480)
  ) {
    resolutionPreset = '480p'
  } else if (
    ratio < 0.35 &&
    sourceResolution &&
    (sourceResolution.width > 1280 || sourceResolution.height > 720)
  ) {
    resolutionPreset = '720p'
  }

  let audioBitrateKbps = 128
  if (ratio < 0.15) {
    audioBitrateKbps = 64
  } else if (ratio < 0.35) {
    audioBitrateKbps = 96
  }

  const preset: EncodingPresetOption = ratio < 0.35 ? 'Slow' : 'Medium'

  return { crf, resolutionPreset, audioBitrateKbps, preset }
}
