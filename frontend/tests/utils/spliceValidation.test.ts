import { describe, it, expect } from 'vitest'
import {
  parseTimeToSeconds,
  parseSizeToBytes,
  formatDuration,
  validateSpliceParameters,
} from '../../src/utils/spliceValidation'
import type { VideoResponse } from '../../src/api/videoApi'

const mockVideo: VideoResponse = {
  id: 'vid-val-1',
  fileName: 'nature.mp4',
  contentType: 'video/mp4',
  sizeBytes: 10 * 1024 * 1024, // 10 MB
  durationSeconds: 120.0, // 2 minutes
  resolution: { width: 1920, height: 1080 },
  codec: 'h264',
  status: 'Ready',
  createdAt: '2026-10-12T10:00:00Z',
}

describe('spliceValidation', () => {
  describe('parseTimeToSeconds', () => {
    it('parses integer seconds correctly', () => {
      expect(parseTimeToSeconds('45', 's')).toBe(45)
      expect(parseTimeToSeconds('  60  ', 's')).toBe(60)
    })

    it('parses fractional seconds up to 2 decimal places', () => {
      expect(parseTimeToSeconds('10.5', 's')).toBe(10.5)
      expect(parseTimeToSeconds('10.25', 's')).toBe(10.25)
      expect(parseTimeToSeconds('1.05', 's')).toBe(1.05)
    })

    it('rejects seconds with more than 2 decimal places', () => {
      expect(parseTimeToSeconds('10.125', 's')).toBeNull()
      expect(parseTimeToSeconds('10.9999', 's')).toBeNull()
    })

    it('rejects invalid seconds strings', () => {
      expect(parseTimeToSeconds('', 's')).toBeNull()
      expect(parseTimeToSeconds('abc', 's')).toBeNull()
      expect(parseTimeToSeconds('-15', 's')).toBeNull()
      expect(parseTimeToSeconds('15.', 's')).toBeNull()
    })

    it('parses mm:ss with integer seconds', () => {
      expect(parseTimeToSeconds('01:30', 'mm:ss')).toBe(90)
      expect(parseTimeToSeconds('0:45', 'mm:ss')).toBe(45)
      expect(parseTimeToSeconds('2:00', 'mm:ss')).toBe(120)
    })

    it('parses mm:ss with fractional seconds up to 2 decimal places', () => {
      expect(parseTimeToSeconds('01:30.5', 'mm:ss')).toBe(90.5)
      expect(parseTimeToSeconds('01:30.25', 'mm:ss')).toBe(90.25)
      expect(parseTimeToSeconds('0:05.75', 'mm:ss')).toBe(5.75)
    })

    it('rejects mm:ss with more than 2 decimal places in seconds', () => {
      expect(parseTimeToSeconds('01:30.123', 'mm:ss')).toBeNull()
    })

    it('rejects invalid mm:ss formats or out of range seconds', () => {
      expect(parseTimeToSeconds('01:60', 'mm:ss')).toBeNull()
      expect(parseTimeToSeconds('01:65', 'mm:ss')).toBeNull()
      expect(parseTimeToSeconds('01', 'mm:ss')).toBeNull()
      expect(parseTimeToSeconds('aa:bb', 'mm:ss')).toBeNull()
      expect(parseTimeToSeconds('01:-10', 'mm:ss')).toBeNull()
    })

    it('parses hh:mm:ss format with optional fractions', () => {
      expect(parseTimeToSeconds('01:00:00', 'mm:ss')).toBe(3600)
      expect(parseTimeToSeconds('01:02:03.50', 'mm:ss')).toBe(3723.5)
      expect(parseTimeToSeconds('01:02:03.555', 'mm:ss')).toBeNull()
    })
  })

  describe('parseSizeToBytes', () => {
    it('parses MB, KB, and B correctly', () => {
      expect(parseSizeToBytes('5', 'MB')).toBe(5 * 1024 * 1024)
      expect(parseSizeToBytes('1.5', 'MB')).toBe(Math.round(1.5 * 1024 * 1024))
      expect(parseSizeToBytes('512', 'KB')).toBe(512 * 1024)
      expect(parseSizeToBytes('1024', 'B')).toBe(1024)
      expect(parseSizeToBytes('invalid', 'MB')).toBeNull()
      expect(parseSizeToBytes('', 'MB')).toBeNull()
    })
  })

  describe('formatDuration', () => {
    it('formats seconds into mm:ss', () => {
      expect(formatDuration(0)).toBe('0:00')
      expect(formatDuration(65)).toBe('1:05')
      expect(formatDuration(125.75)).toBe('2:05')
    })
  })

  describe('validateSpliceParameters', () => {
    it('accepts fractional ByLength seconds up to 2 decimal places', () => {
      const res = validateSpliceParameters('ByLength', '15.75', 's', 'MB', mockVideo)
      expect(res.value).toBe(15.75)
      expect(res.error).toBeNull()
    })

    it('accepts fractional ByLength mm:ss up to 2 decimal places', () => {
      const res = validateSpliceParameters('ByLength', '01:15.50', 'mm:ss', 'MB', mockVideo)
      expect(res.value).toBe(75.5)
      expect(res.error).toBeNull()
    })

    it('rejects ByLength inputs with more than 2 decimal places in seconds', () => {
      const res = validateSpliceParameters('ByLength', '15.123', 's', 'MB', mockVideo)
      expect(res.value).toBeNull()
      expect(res.error).toContain('Fractional seconds can have at most 2 decimal places')
    })

    it('rejects ByLength inputs with more than 2 decimal places in mm:ss', () => {
      const res = validateSpliceParameters('ByLength', '01:15.123', 'mm:ss', 'MB', mockVideo)
      expect(res.value).toBeNull()
      expect(res.error).toContain('Fractional seconds can have at most 2 decimal places')
    })

    it('enforces minimum length of 1 second', () => {
      const res = validateSpliceParameters('ByLength', '0.75', 's', 'MB', mockVideo)
      expect(res.value).toBeNull()
      expect(res.error).toContain('Segment length must be at least 1 second')
    })

    it('enforces video duration upper bound', () => {
      const res = validateSpliceParameters('ByLength', '125.5', 's', 'MB', mockVideo)
      expect(res.value).toBeNull()
      expect(res.error).toContain('cannot exceed source video duration')
    })

    it('enforces BySize bounds', () => {
      const underMin = validateSpliceParameters('BySize', '0.5', 's', 'MB', mockVideo)
      expect(underMin.value).toBeNull()
      expect(underMin.error).toContain('at least 1 MiB')

      const overMax = validateSpliceParameters('BySize', '15', 's', 'MB', mockVideo)
      expect(overMax.value).toBeNull()
      expect(overMax.error).toContain('cannot exceed source video size')

      const valid = validateSpliceParameters('BySize', '5', 's', 'MB', mockVideo)
      expect(valid.value).toBe(5 * 1024 * 1024)
      expect(valid.error).toBeNull()
    })
  })
})
