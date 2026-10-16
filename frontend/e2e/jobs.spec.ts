import { expect, test } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';

test('recruiter creates company and job versions; only published current jobs are public', async ({ page }) => {
  test.setTimeout(30000);
  const suffix = randomUUID();
  const email = `recruiter-${suffix}@example.invalid`;
  const password = `${randomBytes(24).toString('base64')}Aa1!`;
  const title = `E2E Backend Engineer ${suffix}`;

  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.locator('select[name="role"]').selectOption('Recruiter');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await expect(page.getByRole('heading', { name: 'Công ty và tin tuyển dụng' })).toBeVisible();

  const manager = page.locator('.recruiter-jobs');
  await manager.getByLabel('Tên công ty', { exact: true }).fill(`E2E Company ${suffix}`);
  await manager.getByRole('button', { name: 'Tạo công ty', exact: true }).click();
  await expect(manager.getByRole('status').filter({ hasText: 'Hồ sơ công ty đã được lưu.' })).toBeVisible();

  const browser = page.locator('.job-browser');
  await browser.getByLabel('Từ khóa', { exact: true }).fill(title);
  await browser.getByRole('button', { name: 'Tìm việc', exact: true }).click();
  await expect(browser.getByText('Chưa có tin công khai phù hợp.')).toBeVisible();

  const jobEditor = manager.locator('.editor-form').last();
  await jobEditor.getByLabel('Chức danh', { exact: true }).fill(title);
  await jobEditor.getByLabel('Mô tả công việc', { exact: true }).fill('Xây dựng API tuyển dụng bằng C# và PostgreSQL.');
  await jobEditor.getByLabel('Yêu cầu và kỹ năng', { exact: true }).fill('Bắt buộc: C#\nƯu tiên: PostgreSQL\nASP.NET Core');
  await jobEditor.getByRole('button', { name: 'Tạo tin nháp', exact: true }).click();
  const managedJob = manager.locator('.job-card').filter({ hasText: title });
  await expect(managedJob).toContainText('draft');
  await expect(browser.getByText('Chưa có tin công khai phù hợp.')).toBeVisible();

  const jdReview = managedJob.locator('.job-revision-review');
  await jdReview.getByRole('button', { name: 'Tách yêu cầu từ JD (local)' }).click();
  await expect(jdReview.getByRole('checkbox')).toHaveCount(3);
  await expect(jdReview).toContainText('Bắt buộc');
  await expect(jdReview).toContainText('Tùy chọn');
  await jdReview.getByLabel('Phân loại yêu cầu: ASP.NET Core').selectOption('required');
  await jdReview.getByLabel('Yêu cầu recruiter bổ sung').fill('Docker basics');
  await jdReview.getByLabel('Phân loại yêu cầu bổ sung').selectOption('optional');
  await jdReview.getByRole('button', { name: 'Thêm yêu cầu' }).click();
  await jdReview.getByRole('button', { name: 'Xác nhận yêu cầu JD' }).click();
  await expect(jdReview).toContainText('Đã xác nhận');
  await expect(jdReview).toContainText('Docker basics');
  await expect(jdReview).toContainText('không phải trích dẫn từ JD');

  await managedJob.getByRole('button', { name: 'Công khai', exact: true }).click();
  await browser.getByRole('button', { name: 'Tìm việc', exact: true }).click();
  await expect(browser.locator('.job-card').filter({ hasText: title })).toBeVisible();

  await managedJob.getByRole('button', { name: 'Sửa JD', exact: true }).click();
  await jobEditor.locator('textarea').nth(0).fill('Phiên bản 2: phát triển API và kiểm thử PostgreSQL.');
  await jobEditor.getByRole('button', { name: 'Lưu phiên bản mới', exact: true }).click();
  await expect(managedJob).toContainText('phiên bản 2');
  await expect(managedJob).toContainText('Phiên bản 2: phát triển API và kiểm thử PostgreSQL.');

  await managedJob.getByRole('button', { name: 'Đóng tin', exact: true }).click();
  await expect(managedJob).toContainText('closed');
  await browser.getByRole('button', { name: 'Tìm việc', exact: true }).click();
  await expect(browser.getByText('Chưa có tin công khai phù hợp.')).toBeVisible();
});
