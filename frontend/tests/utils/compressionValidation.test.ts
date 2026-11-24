import { describe, it, expect } from 'vitest'
import {
  calculateSizeSavings,
  estimateCompressionForTargetSize,
  getCompatibleCodec,
  validateCompressionParameters,
  type CompressionFormInput,
} from '../../src/utils/compressionValidation'

const baseInput: CompressionFormInput = {
  format: 'Mp4',
  videoCodec: 'H264',
  crfInput: '23',
  resolutionPreset: 'original',
  customWidthInput: '1280',
  customHeightInput: '720',
  preset: 'Medium',
  audioBitrateKbps: 128,
}

const sourceRes = { width: 1920, height: 1080 }

describe('compressionValidation', () => {
  it('maps compatible codecs for MP4 and WebM', () => {
    expect(getCompatibleCodec('Mp4')).toBe('H264')
    expect(getCompatibleCodec('Webm')).toBe('Vp9')
  })

  it('accepts valid parameters', () => {
    const result = validateCompressionParameters(baseInput, sourceRes)
    expect(result.error).toBeNull()
    expect(result.request).toEqual({
      format: 'Mp4',
      videoCodec: 'H264',
      crf: 23,
      targetWidth: null,
      targetHeight: null,
      audioBitrateKbps: 128,
      preset: 'Medium',
    })
  })

  it('rejects incompatible format/codec pairings', () => {
    expect(
      validateCompressionParameters({ ...baseInput, format: 'Mp4', videoCodec: 'Vp9' }, sourceRes).error
    ).toMatch(/not compatible/i)
    expect(
      validateCompressionParameters({ ...baseInput, format: 'Webm', videoCodec: 'H264' }, sourceRes).error
    ).toMatch(/not compatible/i)
  })

  it('validates CRF bounds per codec (0-51 for H264, 0-63 for Vp9)', () => {
    expect(validateCompressionParameters({ ...baseInput, crfInput: '-1' }, sourceRes).error).toMatch(/between 0 and 51/i)
    expect(validateCompressionParameters({ ...baseInput, crfInput: '52' }, sourceRes).error).toMatch(/between 0 and 51/i)
    expect(
      validateCompressionParameters({ ...baseInput, format: 'Webm', videoCodec: 'Vp9', crfInput: '60' }, sourceRes).error
    ).toBeNull()
    expect(
      validateCompressionParameters({ ...baseInput, format: 'Webm', videoCodec: 'Vp9', crfInput: '64' }, sourceRes).error
    ).toMatch(/between 0 and 63/i)
  })

  it('rejects odd custom dimensions and dimensions exceeding source', () => {
    expect(
      validateCompressionParameters(
        { ...baseInput, resolutionPreset: 'custom', customWidthInput: '1281', customHeightInput: '720' },
        sourceRes
      ).error
    ).toMatch(/even number/i)

    expect(
      validateCompressionParameters(
        { ...baseInput, resolutionPreset: 'custom', customWidthInput: '1280', customHeightInput: '721' },
        sourceRes
      ).error
    ).toMatch(/even number/i)

    expect(
      validateCompressionParameters(
        { ...baseInput, resolutionPreset: 'custom', customWidthInput: '2560', customHeightInput: '1080' },
        sourceRes
      ).error
    ).toMatch(/cannot exceed source dimension/i)
  })

  it('calculates size savings percentage accurately', () => {
    expect(calculateSizeSavings(10_000_000, 4_000_000)).toEqual({
      savedBytes: 6_000_000,
      percentSaved: 60,
    })
    expect(calculateSizeSavings(0, 100)).toEqual({ savedBytes: 0, percentSaved: 0 })
  })

  it('estimates compression parameters from a target MB size', () => {
    const twentyMb = 20 * 1024 * 1024

    expect(estimateCompressionForTargetSize(0, twentyMb, sourceRes, 'H264')).toBeNull()
    expect(estimateCompressionForTargetSize(-5, twentyMb, sourceRes, 'H264')).toBeNull()

    // 50% reduction keeps original resolution and raises CRF by ~6
    expect(estimateCompressionForTargetSize(10, twentyMb, sourceRes, 'H264')).toEqual({
      crf: 29,
      resolutionPreset: 'original',
      audioBitrateKbps: 128,
      preset: 'Medium',
    })

    // Heavy reduction (25%) suggests 720p, 96kbps audio, Slow preset
    expect(estimateCompressionForTargetSize(5, twentyMb, sourceRes, 'H264')).toEqual({
      crf: 35,
      resolutionPreset: '720p',
      audioBitrateKbps: 96,
      preset: 'Slow',
    })

    // Very heavy reduction (10%) suggests 480p and 64kbps audio
    expect(estimateCompressionForTargetSize(2, twentyMb, sourceRes, 'H264')).toEqual({
      crf: 43,
      resolutionPreset: '480p',
      audioBitrateKbps: 64,
      preset: 'Slow',
    })
  })
})
