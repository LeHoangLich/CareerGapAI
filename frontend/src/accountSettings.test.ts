import { describe, expect, it } from 'vitest';
import { accountInitials, quotaSeverity, validateNewPasswordMatch } from './accountSettings';

describe('accountSettings helpers', () => {
  it('computes initials from display name or email', () => {
    expect(accountInitials('Hoàng Lịch', 'hlich2080@gmail.com')).toBe('HL');
    expect(accountInitials('An', 'hlich2080@gmail.com')).toBe('AN');
    expect(accountInitials('', 'hlich2080@gmail.com')).toBe('HL');
    expect(accountInitials(null, null)).toBe('CG');
  });

  it('classifies quota severity thresholds correctly', () => {
    expect(quotaSeverity(0)).toBe('normal');
    expect(quotaSeverity(80)).toBe('normal');
    expect(quotaSeverity(80.1)).toBe('warning');
    expect(quotaSeverity(94.9)).toBe('warning');
    expect(quotaSeverity(95)).toBe('danger');
    expect(quotaSeverity(100)).toBe('danger');
    expect(quotaSeverity(40, true)).toBe('danger');
  });

  it('validates new password confirmation match', () => {
    expect(validateNewPasswordMatch('StrongPass123!', 'StrongPass123!')).toBeNull();
    expect(validateNewPasswordMatch('StrongPass123!', 'StrongPass123?')).toBe('Mật khẩu xác nhận không khớp với mật khẩu mới.');
  });
});
