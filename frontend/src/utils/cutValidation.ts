export const MIN_CLIP_SECONDS = 0.1
export const TOLERANCE_SECONDS = 0.001

/** Formats non-negative seconds as mm:ss.s (e.g. 72.5 -> "01:12.5"). */
export function formatTimestamp(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) {
    return '00:00.0'
  }

  // Round to tenths first so values like 59.96 roll over cleanly to 01:00.0
  const roundedTenths = Math.round(seconds * 10)
  const totalSeconds = Math.floor(roundedTenths / 10)
  const tenths = roundedTenths % 10
  const mins = Math.floor(totalSeconds / 60)
  const secs = totalSeconds % 60

  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}.${tenths}`
}

/**
 * Parses a timestamp string (`mm:ss.s`, `mm:ss`, `hh:mm:ss.s`, or plain seconds) into non-negative seconds.
 * Returns null if the input is empty, negative, or malformed.
 */
export function parseTimestamp(input: string): number | null {
  const trimmed = input.trim()
  if (!trimmed || trimmed.startsWith('-')) {
    return null
  }

  const parts = trimmed.split(':')

  if (parts.length === 1) {
    if (!/^\d+(\.\d{1,3})?$/.test(trimmed)) return null
    const secs = Number(trimmed)
    return Number.isFinite(secs) ? Math.round(secs * 1000) / 1000 : null
  }

  if (parts.length === 2) {
    const [minsStr, secsStr] = parts
    if (!/^\d+$/.test(minsStr) || !/^\d+(\.\d{1,3})?$/.test(secsStr)) return null
    const mins = Number(minsStr)
    const secs = Number(secsStr)
    if (!Number.isFinite(mins) || !Number.isFinite(secs) || secs >= 60) return null
    return Math.round((mins * 60 + secs) * 1000) / 1000
  }

  if (parts.length === 3) {
    const [hoursStr, minsStr, secsStr] = parts
    if (!/^\d+$/.test(hoursStr) || !/^\d+$/.test(minsStr) || !/^\d+(\.\d{1,3})?$/.test(secsStr)) {
      return null
    }
    const hours = Number(hoursStr)
    const mins = Number(minsStr)
    const secs = Number(secsStr)
    if (!Number.isFinite(hours) || !Number.isFinite(mins) || !Number.isFinite(secs) || mins >= 60 || secs >= 60) {
      return null
    }
    return Math.round((hours * 3600 + mins * 60 + secs) * 1000) / 1000
  }

  return null
}

export type CutRangeValidationResult =
  | { startSeconds: number; endSeconds: number; error: null }
  | { startSeconds: null; endSeconds: null; error: string }

/** Validates start and end timestamp strings against [0, durationSeconds] and minimum clip length. */
export function validateCutRange(
  startInput: string,
  endInput: string,
  durationSeconds: number | null
): CutRangeValidationResult {
  const trimmedStart = startInput.trim()
  const trimmedEnd = endInput.trim()

  if (!trimmedStart || !trimmedEnd) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: 'Please enter both start and end times (；´Д｀)',
    }
  }

  if (trimmedStart.startsWith('-') || trimmedEnd.startsWith('-')) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: 'Start and end times cannot be negative (；´Д｀)',
    }
  }

  const startSeconds = parseTimestamp(trimmedStart)
  const endSeconds = parseTimestamp(trimmedEnd)

  if (startSeconds === null || endSeconds === null) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: 'Invalid time format. Use mm:ss.s (e.g. 00:12.5) (；´Д｀)',
    }
  }

  if (startSeconds >= endSeconds) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: 'Start time must be less than end time (；´Д｀)',
    }
  }

  if (endSeconds - startSeconds < MIN_CLIP_SECONDS - TOLERANCE_SECONDS) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: `Clip must be at least ${MIN_CLIP_SECONDS}s long (；´Д｀)`,
    }
  }

  if (durationSeconds !== null && endSeconds > durationSeconds + TOLERANCE_SECONDS) {
    return {
      startSeconds: null,
      endSeconds: null,
      error: `End time cannot exceed video duration (${formatTimestamp(durationSeconds)}) (；´Д｀)`,
    }
  }

  return { startSeconds, endSeconds, error: null }
}
