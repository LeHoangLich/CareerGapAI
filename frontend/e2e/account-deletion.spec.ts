import { test, expect } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';

test('candidate requests deletion, cancels, and can recover a pending session after login', async ({ page }) => {
  const email = `deletion-${randomUUID()}@example.invalid`;
  const password = `${randomBytes(24).toString('base64')}Aa1!`;
  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByLabel('Tên hiển thị (ứng viên)', { exact: true }).fill('Ứng viên retention E2E');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();

  await page.getByLabel('Nhập mật khẩu để xác nhận', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Yêu cầu xóa tài khoản', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Yêu cầu xóa đang chờ' })).toBeVisible();
  await expect(page.getByText(/Thời điểm dự kiến bắt đầu purge/)).toBeVisible();
  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Hủy yêu cầu xóa', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();

  await page.getByLabel('Nhập mật khẩu để xác nhận', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Yêu cầu xóa tài khoản', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Yêu cầu xóa đang chờ' })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Yêu cầu xóa đang chờ' })).toBeVisible();
  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Hủy yêu cầu xóa', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Hồ sơ và quyền chia sẻ' })).toBeVisible();
});
