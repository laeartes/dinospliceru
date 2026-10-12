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
    if (!/^\d+(\.\d{1,2})?$/.test(trimmed)) {
      return null
    }
    const num = Number(trimmed)
    return isNaN(num) ? null : Math.round(num * 100) / 100
  }

  // mm:ss or m:ss or hh:mm:ss format
  const parts = trimmed.split(':')
  if (parts.length === 2) {
    const minsStr = parts[0]
    const secsStr = parts[1]
    if (!/^\d+$/.test(minsStr)) return null
    if (!/^\d+(\.\d{1,2})?$/.test(secsStr)) return null

    const mins = Number(minsStr)
    const secs = Number(secsStr)
    if (isNaN(mins) || isNaN(secs) || mins < 0 || secs < 0 || secs >= 60) return null
    return Math.round((mins * 60 + secs) * 100) / 100
  }

  if (parts.length === 3) {
    const hoursStr = parts[0]
    const minsStr = parts[1]
    const secsStr = parts[2]
    if (!/^\d+$/.test(hoursStr) || !/^\d+$/.test(minsStr)) return null
    if (!/^\d+(\.\d{1,2})?$/.test(secsStr)) return null

    const hours = Number(hoursStr)
    const mins = Number(minsStr)
    const secs = Number(secsStr)
    if (
      isNaN(hours) ||
      isNaN(mins) ||
      isNaN(secs) ||
      hours < 0 ||
      mins < 0 ||
      mins >= 60 ||
      secs < 0 ||
      secs >= 60
    ) {
      return null
    }
    return Math.round((hours * 3600 + mins * 60 + secs) * 100) / 100
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
  const trimmed = rawInput.trim()
  if (!trimmed) {
    return { value: null, error: 'Please enter a splice segment value (；´Д｀)' }
  }

  if (mode === 'ByLength') {
    // Check if input has more than 2 decimal places to give a helpful message
    if (lengthUnit === 's') {
      if (/^\d+\.\d{3,}$/.test(trimmed)) {
        return {
          value: null,
          error: 'Fractional seconds can have at most 2 decimal places (；´Д｀)',
        }
      }
    } else {
      const parts = trimmed.split(':')
      const secsPart = parts[parts.length - 1]
      if (parts.length >= 2 && /^\d+\.\d{3,}$/.test(secsPart)) {
        return {
          value: null,
          error: 'Fractional seconds can have at most 2 decimal places (；´Д｀)',
        }
      }
    }

    const seconds = parseTimeToSeconds(trimmed, lengthUnit)
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
