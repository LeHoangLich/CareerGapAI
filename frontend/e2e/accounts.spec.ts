import { openView } from './helpers';
import { test, expect } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';

test('[mock] reset distinguishes policy and server/network failures without offering an invalid-link action', async ({ page }) => {
  let failure: number | 'network' = 400;
  await page.route('**/api/auth/password/reset', async route => {
    if (failure === 'network') await route.abort('failed');
    else await route.fulfill({ status: failure, json: { title: failure === 400 ? 'password_policy' : 'invalid_or_expired_reset' } });
  });
  await page.goto('/?resetEmail=synthetic%40example.invalid&resetToken=synthetic-invalid#access');
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('Synthetic!Password2026');
  for (const kind of [400, 500, 503, 'network'] as const) {
    failure = kind;
    await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
    await expect(page.getByRole('alert')).toHaveText(kind === 400
      ? 'Mật khẩu mới phải có độ dài từ 12 đến 256 ký tự.'
      : 'Máy chủ gặp sự cố hoặc kết nối bị gián đoạn. Vui lòng thử lại sau.');
    await expect(page.getByRole('button', { name: 'Yêu cầu gửi lại liên kết mới', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Quay lại đăng nhập', exact: true })).toBeVisible();
  }
});

test('candidate registers, controls profile sharing, changes password, and recovers it', async ({ page }) => {
  const email = `candidate-${randomUUID()}@example.invalid`;
  const initialPassword = `${randomBytes(24).toString('base64')}Aa1!`;
  const changedPassword = `${randomBytes(24).toString('base64')}Bb2!`;
  const resetPassword = `${randomBytes(24).toString('base64')}Cc3!`;
  await page.goto('/#access');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(initialPassword);
  await page.getByLabel('Tên hiển thị (ứng viên)', { exact: true }).fill('Ứng viên E2E');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await openView(page, 'profile');
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();
  const profilePanel = page.locator('section.card').filter({ has: page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' }) });

  await profilePanel.getByLabel('Tiêu đề nghề nghiệp', { exact: true }).fill('Backend Developer');
  await profilePanel.getByRole('textbox', { name: 'Email liên hệ', exact: true }).fill('candidate-contact@example.invalid');
  await page.getByLabel('Kỹ năng, ngăn cách bằng dấu phẩy', { exact: true }).fill('C#, PostgreSQL');
  await page.getByRole('checkbox', { name: 'Tiêu đề', exact: true }).check();
  await page.getByRole('checkbox', { name: 'Kỹ năng', exact: true }).check();
  await page.getByRole('button', { name: 'Lưu hồ sơ', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Hồ sơ đã được lưu.' })).toBeVisible();
  await expect(profilePanel.getByLabel('Tiêu đề nghề nghiệp', { exact: true })).toHaveValue('Backend Developer');
  await page.reload();
  await expect(profilePanel.getByLabel('Tiêu đề nghề nghiệp', { exact: true })).toHaveValue('Backend Developer');
  await expect(profilePanel.getByRole('textbox', { name: 'Email liên hệ', exact: true })).toHaveValue('candidate-contact@example.invalid');
  await expect(page.getByRole('checkbox', { name: 'Tiêu đề', exact: true })).toBeChecked();

  await openView(page, 'account');
  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(initialPassword);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(changedPassword);
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã đổi mật khẩu.' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Quên mật khẩu?', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByRole('button', { name: 'Gửi hướng dẫn', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã nhận yêu cầu.' })).toBeVisible();
  await page.getByRole('button', { name: 'Mở hộp thư giả local', exact: true }).click();
  await page.getByRole('link', { name: 'Mở liên kết đặt lại mật khẩu giả', exact: true }).click();
  const usedResetLink = page.url();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(resetPassword);
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Mật khẩu đã được đặt lại.' })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(resetPassword);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await openView(page, 'profile');
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await page.goto(usedResetLink);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(resetPassword);
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn');
  await page.getByRole('button', { name: 'Yêu cầu gửi lại liên kết mới', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Khôi phục mật khẩu' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(new URL(page.url()).search).toBe('');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByRole('button', { name: 'Gửi hướng dẫn', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã nhận yêu cầu.' })).toBeVisible();
});
