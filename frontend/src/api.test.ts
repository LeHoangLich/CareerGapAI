import { describe, expect, it } from 'vitest';
import { errorMessage } from './api';
describe('private CV and quota messages', () => {
  it('keeps file access available when quota is exhausted', () => expect(errorMessage('quota_exhausted', 429)).toContain('vẫn có thể mở CV'));
  it('explains unsupported scan PDFs without fabricated content', () => expect(errorMessage('no_text_ocr_unsupported', 422)).toContain('chưa có văn bản'));
  it('does not expose driver or provider internals', () => expect(errorMessage('connection_password=secret', 500)).not.toContain('secret'));
});
