import { expect, test, type Page } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

async function register(page: Page, role = 'Candidate') {
  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(`stage-b-${randomUUID()}@example.invalid`);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(`${randomBytes(24).toString('base64')}Aa1!`);
  await page.locator('select[name="role"]').selectOption(role);
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
}

test('full CV review survives reload, preserves source, and supplies builder draft', async ({ page }) => {
  test.setTimeout(90000);
  const fixture = process.env.CAREERGAP_FULL_CV_FIXTURE;
  if (!fixture) throw new Error('Set CAREERGAP_FULL_CV_FIXTURE to a generated synthetic full CV DOCX.');
  await register(page);
  await page.getByLabel('Thêm vào CV', { exact: true }).setInputFiles(fixture);
  await page.getByRole('button', { name: 'Tạo CV riêng tư', exact: true }).click();
  const resume = page.locator('.resumes > li').first();
  const admitted = page.waitForResponse(response => new URL(response.url()).pathname === '/api/ai/jobs' && response.request().method() === 'POST');
  await resume.getByRole('button', { name: 'Đọc đầy đủ v1', exact: true }).click();
  expect((await admitted).status()).toBe(202);
  await page.reload();
  const review = page.locator('.full-cv-review');
  await expect(review.getByRole('heading', { name: 'Rà soát toàn bộ dữ liệu CV' })).toBeVisible({ timeout: 30000 });
  await expect(review.getByLabel('Họ tên', { exact: true })).toHaveValue('Ứng Viên Tổng Hợp');
  await review.getByLabel('Giới thiệu', { exact: true }).fill('Nội dung do ứng viên tự sửa.');
  await review.getByLabel('Trường cần bổ sung').selectOption('skills[3]');
  await review.getByLabel('Nội dung tự bổ sung').fill('Go');
  await review.getByRole('button', { name: 'Thêm thông tin', exact: true }).click();
  await review.getByRole('button', { name: 'Xác nhận toàn bộ dữ liệu CV', exact: true }).click();
  await expect(review.getByRole('status')).toContainText('Đã lưu bản xác nhận mới');
  await expect(review.getByRole('group', { name: 'Giới thiệu', exact: true })).toContainText('không phải trích dẫn');
  const all = await page.request.get('/api/resumes').then(response => response.json());
  const versions = all[0].versions;
  const revisions = await page.request.get(`/api/resume-versions/${versions[0].id}/revisions`).then(response => response.json());
  expect(revisions.map((item: { origin: string }) => item.origin)).toEqual(['user_confirmed', 'ai_extracted']);
  expect(revisions[1].structuredData.fields.find((field: { path: string }) => field.path === 'summary').value).toBe('Xây dựng dữ liệu thử.');
  const builder = page.locator('.resume-builder');
  await builder.getByLabel('CV cần cập nhật').selectOption(all[0].id);
  await builder.getByRole('button', { name: 'Nạp revision mẫu' }).click();
  await expect(builder.getByLabel('Tên hiển thị *')).toHaveValue('Ứng Viên Tổng Hợp');
  await expect(builder.getByLabel('Giới thiệu')).toHaveValue('Nội dung do ứng viên tự sửa.');
  await expect(builder.getByLabel('Kỹ năng, mỗi dòng một mục')).toHaveValue('React\nTypeScript\nPostgreSQL\nGo');
});

test('builder resumes a failed revision save after reload and recruiter downloads exact submitted PDF snapshot', async ({ browser, baseURL }) => {
  test.setTimeout(120000);
  const candidate = await browser.newContext({ baseURL });
  const recruiter = await browser.newContext({ baseURL });
  const page = await candidate.newPage(); const recruiterPage = await recruiter.newPage();
  page.on('response', async response => {
    if (new URL(response.url()).pathname.endsWith('/revisions/manual') && !response.ok()) {
      const problem = await response.json().catch(() => ({}));
      console.log('Builder revision failure status/code:', response.status(), problem.title);
    }
  });
  try {
    await register(recruiterPage, 'Recruiter');
    const manager = recruiterPage.locator('.recruiter-jobs');
    const title = `Builder snapshot ${randomUUID()}`;
    await manager.getByLabel('Tên công ty', { exact: true }).fill(`Synthetic ${randomUUID()}`);
    await manager.getByRole('button', { name: 'Tạo công ty', exact: true }).click();
    await expect(manager.getByRole('status')).toContainText('Hồ sơ công ty đã được lưu.');
    const editor = manager.locator('.editor-form').last();
    await editor.getByLabel('Chức danh', { exact: true }).fill(title);
    await editor.getByLabel('Mô tả công việc', { exact: true }).fill('Synthetic PDF snapshot verification.');
    await editor.getByLabel('Yêu cầu và kỹ năng', { exact: true }).fill('React');
    await editor.getByRole('button', { name: 'Tạo tin nháp' }).click();
    const managed = manager.locator('.job-card').filter({ hasText: title });
    await managed.getByRole('button', { name: 'Công khai', exact: true }).click();
    await register(page);
    const builder = page.locator('.resume-builder');
    await builder.getByLabel('Tên hiển thị *').fill('Ứng Viên PDF Tổng Hợp');
    await builder.getByLabel('Giới thiệu').fill(Array.from({ length: 75 }, () => 'Thông tin tổng hợp để kiểm tra tiếng Việt.').join(' ').slice(0, 2900));
    await builder.getByLabel('Kỹ năng, mỗi dòng một mục').fill('React\nTypeScript');
    for (let i = 1; i <= 3; i++) {
      await builder.getByRole('button', { name: 'Thêm kinh nghiệm', exact: true }).click();
      const experience = builder.getByRole('group', { name: `Kinh nghiệm ${i}`, exact: true });
      await experience.getByLabel('Chức danh', { exact: true }).fill(`Kỹ sư tổng hợp ${i}`);
      await experience.getByLabel('Đơn vị', { exact: true }).fill('Công ty thử nghiệm');
      await experience.getByLabel('Mô tả', { exact: true }).fill(Array.from({ length: 30 }, () => 'Thực hiện công việc tổng hợp để kiểm tra phân trang.').join(' ').slice(0, 1100));
    }
    await builder.getByRole('button', { name: 'Tạo bản xem trước PDF', exact: true }).click();
    await expect(builder.getByTitle('Xem trước CV PDF')).toBeVisible({ timeout: 20000 });
    let fail = true;
    await page.route('**/revisions/manual', route => {
      if (fail) return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'synthetic_save_failure' }) });
      return route.continue();
    });
    await builder.getByRole('button', { name: 'Lưu thành phiên bản CV', exact: true }).click();
    await expect(builder.getByRole('alert')).toBeVisible();
    const initial = await page.request.get('/api/resumes').then(response => response.json());
    expect(initial).toHaveLength(1); expect(initial[0].versions).toHaveLength(1);
    fail = false; await page.reload();
    await builder.getByRole('button', { name: 'Hoàn tất bản lưu dang dở' }).click();
    await expect(builder.getByRole('status').filter({ hasText: 'Đã hoàn tất' })).toBeVisible();
    const recovered = await page.request.get('/api/resumes').then(response => response.json());
    expect(recovered[0].versions).toHaveLength(1);
    expect(await page.evaluate(() => Object.keys(sessionStorage).filter(key => key.startsWith('careergap:builder-recovery:')))).toHaveLength(0);
    const version = recovered[0].versions[0];
    const original = await page.request.get(`/api/files/${version.fileId}`);
    const originalBytes = await original.body();
    const evidence = path.resolve(process.env.CAREERGAP_E2E_EVIDENCE_DIRECTORY ?? '../.local/stage-b-20261016/evidence'); fs.mkdirSync(evidence, { recursive: true });
    fs.writeFileSync(path.join(evidence, 'builder-long.pdf'), originalBytes);
    const jobs = page.locator('.job-browser');
    await jobs.getByLabel('Từ khóa', { exact: true }).fill(title);
    await jobs.getByRole('button', { name: 'Tìm việc', exact: true }).click();
    const job = jobs.locator('.job-card').filter({ hasText: title });
    await job.getByRole('button', { name: 'Nộp hồ sơ', exact: true }).click();
    await expect(page.getByText(`Đã nộp hồ sơ cho “${title}”.`, { exact: true })).toBeVisible();
    await builder.getByLabel('Giới thiệu').fill('Phiên bản ngắn mới. Snapshot đơn vẫn giữ bản PDF dài.');
    await builder.getByRole('button', { name: 'Tạo bản xem trước PDF', exact: true }).click();
    await expect(builder.getByTitle('Xem trước CV PDF')).toBeVisible();
    await builder.getByRole('button', { name: 'Lưu thành phiên bản CV', exact: true }).click();
    await expect(builder.getByRole('status').filter({ hasText: 'Đã lưu CV riêng tư' })).toBeVisible();
    const changed = await page.request.get('/api/resumes').then(response => response.json());
    expect(changed[0].versions).toHaveLength(2);
    const short = await page.request.get(`/api/files/${changed[0].versions[0].fileId}`);
    fs.writeFileSync(path.join(evidence, 'builder-short.pdf'), await short.body());
    await managed.getByRole('button', { name: /Hồ sơ ứng tuyển/ }).click();
    const link = await managed.getByRole('link', { name: 'Mở CV đã nộp', exact: true }).getAttribute('href');
    const snapshot = await recruiter.request.get(link!);
    expect(snapshot.status()).toBe(200); expect(await snapshot.body()).toEqual(originalBytes);
    expect((await recruiter.request.get(`/api/files/${version.fileId}`)).status()).toBe(404);
  } finally { await candidate.close(); await recruiter.close(); }
});
