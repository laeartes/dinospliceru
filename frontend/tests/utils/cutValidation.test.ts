import { describe, it, expect } from 'vitest'
import { formatTimestamp, parseTimestamp, validateCutRange } from '../../src/utils/cutValidation'

describe('cutValidation', () => {
  describe('formatTimestamp', () => {
    it('formats seconds as mm:ss.s', () => {
      expect(formatTimestamp(0)).toBe('00:00.0')
      expect(formatTimestamp(5.5)).toBe('00:05.5')
      expect(formatTimestamp(65.2)).toBe('01:05.2')
      expect(formatTimestamp(59.96)).toBe('01:00.0')
    })

    it('clamps negative or non-finite values to 00:00.0', () => {
      expect(formatTimestamp(-5)).toBe('00:00.0')
      expect(formatTimestamp(Number.NaN)).toBe('00:00.0')
      expect(formatTimestamp(Number.POSITIVE_INFINITY)).toBe('00:00.0')
    })
  })

  describe('parseTimestamp', () => {
    it('parses mm:ss.s, mm:ss, hh:mm:ss.s, and plain seconds', () => {
      expect(parseTimestamp('00:00.0')).toBe(0)
      expect(parseTimestamp('00:12.5')).toBe(12.5)
      expect(parseTimestamp('01:30')).toBe(90)
      expect(parseTimestamp('1:02:03.5')).toBe(3723.5)
      expect(parseTimestamp('15.25')).toBe(15.25)
    })

    it('returns null for empty, negative, or malformed inputs', () => {
      expect(parseTimestamp('')).toBeNull()
      expect(parseTimestamp('   ')).toBeNull()
      expect(parseTimestamp('-5')).toBeNull()
      expect(parseTimestamp('-00:05.0')).toBeNull()
      expect(parseTimestamp('00:60.0')).toBeNull()
      expect(parseTimestamp('01:60:00')).toBeNull()
      expect(parseTimestamp('abc')).toBeNull()
      expect(parseTimestamp('00:10.1234')).toBeNull()
    })
  })

  describe('validateCutRange', () => {
    it('accepts valid ranges within video duration', () => {
      expect(validateCutRange('00:02.0', '00:15.5', 60)).toEqual({
        startSeconds: 2,
        endSeconds: 15.5,
        error: null,
      })
    })

    it('rejects empty inputs', () => {
      expect(validateCutRange('', '00:10.0', 60).error).toMatch(/both start and end/i)
      expect(validateCutRange('00:00.0', '', 60).error).toMatch(/both start and end/i)
    })

    it('rejects negative timestamps', () => {
      expect(validateCutRange('-00:01.0', '00:10.0', 60).error).toMatch(/cannot be negative/i)
      expect(validateCutRange('00:00.0', '-5', 60).error).toMatch(/cannot be negative/i)
    })

    it('rejects malformed timestamps', () => {
      expect(validateCutRange('not-a-time', '00:10.0', 60).error).toMatch(/invalid time format/i)
    })

    it('rejects start >= end', () => {
      expect(validateCutRange('00:10.0', '00:10.0', 60).error).toMatch(/start time must be less than end time/i)
      expect(validateCutRange('00:20.0', '00:10.0', 60).error).toMatch(/start time must be less than end time/i)
    })

    it('rejects clips shorter than 0.1s', () => {
      expect(validateCutRange('00:05.0', '00:05.05', 60).error).toMatch(/at least 0\.1s/i)
    })

    it('rejects end exceeding video duration', () => {
      expect(validateCutRange('00:00.0', '01:05.0', 60).error).toMatch(/cannot exceed video duration/i)
    })

    it('allows end within floating-point tolerance of duration', () => {
      expect(validateCutRange('00:00.0', '01:00.0', 60).error).toBeNull()
    })
  })
})
