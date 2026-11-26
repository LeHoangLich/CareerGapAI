import { describe, expect, it } from 'vitest';
import { coverLetterLimit, normalizeCoverLetter } from './coverLetter';

describe('cover letter submission', () => {
  it('normalizes line endings and outer whitespace without changing text or inner spacing', () => {
    expect(normalizeCoverLetter('  Kính gửi anh/chị,\r\nTôi biết React.\rTrân trọng.  ')).toBe('Kính gửi anh/chị,\nTôi biết React.\nTrân trọng.');
    expect(normalizeCoverLetter('<script>alert(1)</script>  React')).toBe('<script>alert(1)</script>  React');
  });
  it('keeps empty submissions compatible with historical applications', () => {
    for (const value of ['', '   ', '\r\n\t']) expect(normalizeCoverLetter(value)).toBeNull();
  });
  it('accepts the limit and rejects oversized or NUL-containing input before submission', () => {
    expect(normalizeCoverLetter('a'.repeat(coverLetterLimit))).toHaveLength(coverLetterLimit);
    expect(() => normalizeCoverLetter('a'.repeat(coverLetterLimit + 1))).toThrow('5.000');
    expect(() => normalizeCoverLetter('invalid\0text')).toThrow('không hợp lệ');
  });
});
