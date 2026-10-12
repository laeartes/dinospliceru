import type { VideoResponse } from '../api/videoApi'
import type { SpliceMode } from '../api/spliceApi'
import { formatFileSize } from './formatFileSize'

export type LengthUnit = 's' | 'mm:ss'
export type SizeUnit = 'MB' | 'KB' | 'B'

export function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60)
  const remainingSecs = Math.floor(seconds % 60)
  return `${mins}:${remainingSecs.toString().padStart(2, '0')}`
}

export function parseTimeToSeconds(input: string, unit: LengthUnit): number | null {
  const trimmed = input.trim()
  if (!trimmed) return null

  if (unit === 's') {
    const num = Number(trimmed)
    return isNaN(num) ? null : num
  }

  // mm:ss or m:ss or hh:mm:ss format
  const parts = trimmed.split(':')
  if (parts.length === 2) {
    const mins = Number(parts[0])
    const secs = Number(parts[1])
    if (isNaN(mins) || isNaN(secs) || mins < 0 || secs < 0 || secs >= 60) return null
    return mins * 60 + secs
  }
  if (parts.length === 3) {
    const hours = Number(parts[0])
    const mins = Number(parts[1])
    const secs = Number(parts[2])
    if (
      isNaN(hours) ||
      isNaN(mins) ||
      isNaN(secs) ||
      hours < 0 ||
      mins < 0 ||
      mins >= 60 ||
      secs < 0 ||
      secs >= 60
    )
      return null
    return hours * 3600 + mins * 60 + secs
  }

  return null
}

export function parseSizeToBytes(input: string, unit: SizeUnit): number | null {
  const trimmed = input.trim()
  if (!trimmed) return null

  const num = Number(trimmed)
  if (isNaN(num)) return null

  switch (unit) {
    case 'MB':
      return Math.round(num * 1024 * 1024)
    case 'KB':
      return Math.round(num * 1024)
    case 'B':
      return Math.round(num)
  }
}

export function validateSpliceParameters(
  mode: SpliceMode,
  rawInput: string,
  lengthUnit: LengthUnit,
  sizeUnit: SizeUnit,
  video: VideoResponse
): { value: number; error: null } | { value: null; error: string } {
  if (!rawInput.trim()) {
    return { value: null, error: 'Please enter a splice segment value (；´Д｀)' }
  }

  if (mode === 'ByLength') {
    const seconds = parseTimeToSeconds(rawInput, lengthUnit)
    if (seconds === null || seconds <= 0) {
      return {
        value: null,
        error:
          lengthUnit === 'mm:ss'
            ? 'Invalid time format. Please use mm:ss (e.g. 01:30) (；´Д｀)'
            : 'Please enter a valid positive number of seconds (；´Д｀)',
      }
    }

    if (seconds < 1) {
      return { value: null, error: 'Segment length must be at least 1 second (；´Д｀)' }
    }

    if (video.durationSeconds !== null && seconds > video.durationSeconds) {
      return {
        value: null,
        error: `Segment length cannot exceed source video duration (${formatDuration(video.durationSeconds)}) (；´Д｀)`,
      }
    }

    return { value: seconds, error: null }
  }

  // BySize
  const bytes = parseSizeToBytes(rawInput, sizeUnit)
  if (bytes === null || bytes <= 0) {
    return { value: null, error: 'Please enter a valid positive file size (；´Д｀)' }
  }

  const minBytes = 1024 * 1024 // 1 MiB
  if (bytes < minBytes) {
    return {
      value: null,
      error: `Segment size must be at least 1 MiB (${formatFileSize(minBytes)}) (；´Д｀)`,
    }
  }

  if (bytes > video.sizeBytes) {
    return {
      value: null,
      error: `Segment size cannot exceed source video size (${formatFileSize(video.sizeBytes)}) (；´Д｀)`,
    }
  }

  return { value: bytes, error: null }
}
