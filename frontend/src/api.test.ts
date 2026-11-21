import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, errorMessage, refreshCsrf } from './api';
afterEach(() => vi.unstubAllGlobals());
describe('private CV and quota messages', () => {
  it('explains disabled AI while preserving manual CV access', () => {
    const message = errorMessage('ai_disabled', 503);
    expect(message).toContain('AI đang tắt');
    expect(message).toContain('Yêu cầu mới chưa được tạo');
    expect(message).toContain('tải CV và thao tác thủ công');
  });
  it('keeps file access available when quota is exhausted', () => expect(errorMessage('quota_exhausted', 429)).toContain('vẫn có thể mở CV'));
  it('explains unsupported scan PDFs without fabricated content', () => expect(errorMessage('no_text_ocr_unsupported', 422)).toContain('chưa có văn bản'));
  it('does not expose driver or provider internals', () => expect(errorMessage('connection_password=secret', 500)).not.toContain('secret'));
});
describe('password recovery errors', () => {
  it('distinguishes reset links, password policy and server failures', () => {
    expect(errorMessage('invalid_or_expired_reset', 400)).toBe('Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.');
    expect(errorMessage('password_policy', 400)).toBe('Mật khẩu mới phải có độ dài từ 12 đến 256 ký tự.');
    for (const status of [0, 500, 503]) expect(errorMessage('invalid_or_expired_reset', status)).toBe('Máy chủ gặp sự cố hoặc kết nối bị gián đoạn. Vui lòng thử lại sau.');
  });
  it('retains the problem code and status for the recovery action', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ title: 'invalid_or_expired_reset' }), { status: 400 })));
    await expect(api('/test')).rejects.toMatchObject({ name: 'ApiError', code: 'invalid_or_expired_reset', status: 400 });
  });
  it('handles non-JSON server errors and network failures including CSRF', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('server details', { status: 503 })));
    await expect(api('/test')).rejects.toThrow(errorMessage('', 503));
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    await expect(api('/test')).rejects.toThrow(ApiError);
    await expect(refreshCsrf()).rejects.toThrow(errorMessage('', 0));
  });
  it('preserves explicit request cancellation', async () => {
    const abort = new DOMException('Aborted', 'AbortError');
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abort));
    await expect(api('/test')).rejects.toBe(abort);
  });
});
