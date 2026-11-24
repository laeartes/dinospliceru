import { describe, it, expect } from 'vitest'
import {
  calculateSizeSavings,
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
      targetSizeBytes: null,
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

  it('validates targetSizeMbInput and converts to targetSizeBytes', () => {
    const valid = validateCompressionParameters({ ...baseInput, targetSizeMbInput: '8' }, sourceRes, 41.3)
    expect(valid.error).toBeNull()
    expect(valid.request?.targetSizeBytes).toBe(8 * 1024 * 1024)

    expect(
      validateCompressionParameters({ ...baseInput, targetSizeMbInput: '0' }, sourceRes, 41.3).error
    ).toMatch(/positive number/i)

    expect(
      validateCompressionParameters({ ...baseInput, targetSizeMbInput: 'abc' }, sourceRes, 41.3).error
    ).toMatch(/positive number/i)

    // 0.1MB over 120s leaves less than 50kbps video bitrate after 128kbps audio
    expect(
      validateCompressionParameters({ ...baseInput, targetSizeMbInput: '0.1' }, sourceRes, 120).error
    ).toMatch(/too small/i)
  })
})
