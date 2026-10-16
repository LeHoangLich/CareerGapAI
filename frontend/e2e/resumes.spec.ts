import { expect, test } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import fs from 'node:fs';

test('candidate names a private resume, appends a version, and selects the intended snapshot', async ({ page }) => {
  test.setTimeout(45000);
  const fixture = process.env.CAREERGAP_DOCX_FIXTURE;
  if (!fixture || !fs.existsSync(fixture)) throw new Error('Set CAREERGAP_DOCX_FIXTURE to a synthetic DOCX file.');
  const bytes = fs.readFileSync(fixture);
  const suffix = randomUUID();
  const email = `candidate-resume-${suffix}@example.invalid`;
  const password = `${randomBytes(24).toString('base64')}Aa1!`;
  const docxMime = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';

  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await expect(page.getByRole('heading', { name: 'CV riêng của bạn' })).toBeVisible();

  const upload = page.getByLabel('Thêm vào CV', { exact: true });
  await upload.setInputFiles({ name: `backend-${suffix}-v1.docx`, mimeType: docxMime, buffer: bytes });
  await page.getByRole('button', { name: 'Tạo CV riêng tư', exact: true }).click();
  const resume = page.locator('.resumes > li').first();
  await expect(resume).toContainText('1 phiên bản');
  await expect(resume.getByRole('radio')).toHaveCount(1);
  await expect(resume.getByRole('radio')).toBeChecked();

  await resume.getByRole('button', { name: 'Thử trích xuất v1', exact: true }).click();
  const revisionReview = page.locator('.revision-review');
  await expect(revisionReview).toContainText('Chờ bạn xác nhận', { timeout: 20000 });
  await expect(revisionReview.getByLabel('TypeScript', { exact: true })).toBeChecked();
  await revisionReview.getByLabel('Kỹ năng bạn tự xác nhận (mỗi dòng một kỹ năng)', { exact: true }).fill('Go');
  await revisionReview.getByRole('button', { name: 'Xác nhận dữ liệu revision 1', exact: true }).click();
  await expect(revisionReview).toContainText('Bạn đã xác nhận');
  await expect(revisionReview).toContainText('Bạn tự xác nhận; không phải trích dẫn từ CV.');

  await page.locator('select[name="targetResumeId"]').selectOption({ label: `backend-${suffix}-v1.docx` });
  await upload.setInputFiles({ name: `backend-${suffix}-v2.docx`, mimeType: docxMime, buffer: bytes });
  await page.getByRole('button', { name: 'Thêm phiên bản', exact: true }).click();
  await expect(resume).toContainText('2 phiên bản');
  await expect(resume.getByRole('radio')).toHaveCount(2);
  await expect(resume.getByRole('radio').nth(0)).toBeChecked();
  await resume.getByRole('radio').nth(1).check();
  await expect(resume.getByRole('radio').nth(1)).toBeChecked();
  await resume.getByRole('radio').nth(0).check();
  await expect(resume.getByRole('radio').nth(0)).toBeChecked();
  await expect(resume.getByRole('link', { name: 'Tải file', exact: true })).toHaveCount(2);

  await page.getByRole('button', { name: 'Công khai phiên bản 2', exact: true }).click();
  await expect(page.locator('.public-resume-controls')).toContainText('CV đã công khai');
  await expect(page.locator('.public-resume-controls')).toContainText('Đang công khai phiên bản 2');
  await page.getByRole('button', { name: 'Ẩn CV công khai', exact: true }).click();
  await expect(page.locator('.public-resume-controls')).toContainText('CV đã được ẩn');

  await resume.getByRole('button', { name: 'Đổi tên', exact: true }).click();
  await resume.getByLabel('Tên CV', { exact: true }).fill('CV Backend chính');
  await resume.getByRole('button', { name: 'Lưu tên', exact: true }).click();
  await expect(resume).toContainText('CV Backend chính');
  await expect(resume).toContainText('2 phiên bản');
});
