import { test, expect } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';

test('candidate registers, controls profile sharing, changes password, and recovers it', async ({ page }) => {
  const email = `candidate-${randomUUID()}@example.invalid`;
  const initialPassword = `${randomBytes(24).toString('base64')}Aa1!`;
  const changedPassword = `${randomBytes(24).toString('base64')}Bb2!`;
  const resetPassword = `${randomBytes(24).toString('base64')}Cc3!`;
  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(initialPassword);
  await page.getByLabel('Tên hiển thị (ứng viên)', { exact: true }).fill('Ứng viên E2E');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
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

  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(initialPassword);
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(changedPassword);
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã đổi mật khẩu.' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đăng nhập thử nghiệm' })).toBeVisible();

  await page.getByRole('button', { name: 'Quên mật khẩu?', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByRole('button', { name: 'Gửi hướng dẫn', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã nhận yêu cầu.' })).toBeVisible();
  await page.getByRole('button', { name: 'Mở hộp thư giả local', exact: true }).click();
  await page.getByRole('link', { name: 'Mở liên kết đặt lại mật khẩu giả', exact: true }).click();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(resetPassword);
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Mật khẩu đã được đặt lại.' })).toBeVisible();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(resetPassword);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();
});
