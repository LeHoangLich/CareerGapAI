import { expect, test } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import fs from 'node:fs';

test('candidate submits a selected resume version and recruiter reads it only through the owned application', async ({ browser, baseURL }) => {
  test.setTimeout(45000);
  const fixture = process.env.CAREERGAP_DOCX_FIXTURE;
  if (!fixture || !fs.existsSync(fixture)) throw new Error('Set CAREERGAP_DOCX_FIXTURE to a synthetic DOCX file.');
  const bytes = fs.readFileSync(fixture);
  const suffix = randomUUID();
  const title = `E2E application role ${suffix}`;
  const password = `${randomBytes(24).toString('base64')}Aa1!`;
  const recruiterContext = await browser.newContext({ baseURL });
  const candidateContext = await browser.newContext({ baseURL });
  const recruiterPage = await recruiterContext.newPage();
  const candidatePage = await candidateContext.newPage();

  try {
    await recruiterPage.goto('/');
    await recruiterPage.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
    await recruiterPage.getByLabel('Email', { exact: true }).fill(`recruiter-apply-${suffix}@example.invalid`);
    await recruiterPage.getByLabel('Mật khẩu', { exact: true }).fill(password);
    await recruiterPage.locator('select[name="role"]').selectOption('Recruiter');
    await recruiterPage.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
    await recruiterPage.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
    const manager = recruiterPage.locator('.recruiter-jobs');
    await expect(manager.getByRole('heading', { name: 'Công ty và tin tuyển dụng' })).toBeVisible();
    await manager.getByLabel('Tên công ty', { exact: true }).fill(`Apply Company ${suffix}`);
    await manager.getByRole('button', { name: 'Tạo công ty', exact: true }).click();
    await expect(manager.getByRole('status').filter({ hasText: 'Hồ sơ công ty đã được lưu.' })).toBeVisible();
    const editor = manager.locator('.editor-form').last();
    await editor.getByLabel('Chức danh', { exact: true }).fill(title);
    await editor.getByLabel('Mô tả công việc', { exact: true }).fill('Tin giả cho kiểm thử ứng tuyển.');
    await editor.getByLabel('Yêu cầu và kỹ năng', { exact: true }).fill('TypeScript, PostgreSQL');
    await editor.getByRole('button', { name: 'Tạo tin nháp', exact: true }).click();
    const managedJob = manager.locator('.job-card').filter({ hasText: title });
    await managedJob.getByRole('button', { name: 'Công khai', exact: true }).click();

    await candidatePage.goto('/');
    await candidatePage.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
    await candidatePage.getByLabel('Email', { exact: true }).fill(`candidate-apply-${suffix}@example.invalid`);
    await candidatePage.getByLabel('Mật khẩu', { exact: true }).fill(password);
    await candidatePage.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
    await candidatePage.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));
    await expect(candidatePage.getByRole('heading', { name: 'CV riêng của bạn' })).toBeVisible();
    const docxMime = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    await candidatePage.getByLabel('Thêm vào CV', { exact: true }).setInputFiles({ name: `apply-${suffix}.docx`, mimeType: docxMime, buffer: bytes });
    await candidatePage.getByRole('button', { name: 'Tạo CV riêng tư', exact: true }).click();

    const browserPanel = candidatePage.locator('.job-browser');
    const candidateMatching = browserPanel.locator('.matching-panel[aria-label="Gợi ý việc bằng từ khóa"]');
    await candidateMatching.getByRole('button', { name: 'Tìm việc theo CV', exact: true }).click();
    // The local app database intentionally keeps synthetic E2E jobs. Assert the bounded ranking UI
    // renders valid results without assuming this newly created job fits inside its top-20 window.
    const recommendedJobs = candidateMatching.locator('.matching-results li');
    await expect(recommendedJobs.first()).toContainText('retrieval_score');
    // Other test data can include a confirmed JD; this newly uploaded CV is still unconfirmed.
    await expect(recommendedJobs.first()).toContainText(/CV (và JD )?chưa có revision xác nhận/);

    await candidatePage.locator('.public-resume-controls').getByRole('button', { name: 'Công khai phiên bản 1', exact: true }).click();
    await expect(candidatePage.locator('.public-resume-controls')).toContainText('CV đã công khai');
    await browserPanel.getByLabel('Từ khóa', { exact: true }).fill(title);
    await browserPanel.getByRole('button', { name: 'Tìm việc', exact: true }).click();
    const publicJob = browserPanel.locator('.job-card').filter({ hasText: title });
    await expect(publicJob).toBeVisible();
    await expect(publicJob.getByRole('button', { name: 'Nộp hồ sơ', exact: true })).toBeEnabled();
    await publicJob.getByRole('button', { name: 'Nộp hồ sơ', exact: true }).click();
    await expect(candidatePage.getByText(`Đã nộp hồ sơ cho “${title}”.`, { exact: true })).toBeVisible();
    await expect(candidatePage.locator('.application-center').first()).toContainText(title);
    await expect(candidatePage.locator('.notification-center')).toContainText('Đã nộp hồ sơ');

    const recruiterMatching = managedJob.locator('.recruiter-matching');
    await recruiterMatching.getByRole('button', { name: 'Tìm ứng viên bằng từ khóa', exact: true }).click();
    const matchingCandidates = recruiterMatching.locator('.matching-results li');
    await expect(matchingCandidates.first()).toContainText('retrieval_score');
    await expect(recruiterMatching.locator('.matching-results')).not.toContainText(`candidate-apply-${suffix}@example.invalid`);

    await managedJob.getByRole('button', { name: /Hồ sơ ứng tuyển/ }).click();
    const applicant = managedJob.locator('.application-list li');
    await expect(applicant).toHaveCount(1);
    const applicationResumeUrl = await applicant.getByRole('link', { name: 'Mở CV đã nộp' }).getAttribute('href');
    expect(applicationResumeUrl).toMatch(/^\/api\/applications\/[0-9a-f-]+\/resume$/i);
    await applicant.getByRole('button', { name: 'Đang xem xét', exact: true }).click();
    await expect(applicant).toContainText('Đang xem xét');
    await applicant.getByRole('button', { name: 'Ghi chú nội bộ', exact: true }).click();
    const privateNote = `Chỉ recruiter thấy ${suffix}`;
    await applicant.getByLabel('Thêm ghi chú', { exact: true }).fill(privateNote);
    await applicant.getByRole('button', { name: 'Lưu ghi chú nội bộ', exact: true }).click();
    await expect(applicant.locator('.application-notes')).toContainText(privateNote);
    await applicant.getByRole('button', { name: 'Chặn ứng viên cho tin này', exact: true }).click();
    await expect(applicant.getByRole('button', { name: 'Bỏ chặn ứng viên', exact: true })).toBeVisible();

    await candidatePage.reload();
    const candidateApplications = candidatePage.locator('.application-center').first();
    await expect(candidateApplications).toContainText('Đang xem xét');
    await candidateApplications.getByRole('button', { name: 'Lịch sử trạng thái', exact: true }).click();
    await expect(candidateApplications.locator('.status-history')).toContainText('Đang xem xét');
    await candidateApplications.getByRole('button', { name: 'Rút hồ sơ', exact: true }).click();
    await expect(candidateApplications).toContainText('Đã rút');
    const applicationsResponse = await candidateContext.request.get('/api/applications/mine?page=1&pageSize=10');
    const applicationPage = await applicationsResponse.json() as { items: { id: string; jobId: string; jobTitle: string; status: string }[] };
    const application = applicationPage.items.find(item => item.id === applicationResumeUrl!.split('/')[3]);
    expect(application?.status).toBe('withdrawn');
    const resumesResponse = await candidateContext.request.get('/api/resumes?page=1');
    expect(resumesResponse.ok()).toBeTruthy();
    const resumeList = await resumesResponse.json() as { versions: { id: string; fileId: string }[] }[];
    const resumeVersionId = resumeList[0].versions[0].id;
    const fileId = resumeList[0].versions[0].fileId;
    const csrf = await candidateContext.request.get('/api/auth/csrf').then(response => response.json() as Promise<{ token: string }>);
    expect((await candidateContext.request.post(`/api/jobs/${application!.jobId}/applications`, { headers: { 'X-CSRF-TOKEN': csrf.token }, data: { resumeVersionId, submissionKey: randomUUID() } })).status()).toBe(409);
    expect((await recruiterContext.request.get(`/api/files/${fileId}`)).status()).toBe(404);
    const applicationFile = await recruiterContext.request.get(applicationResumeUrl!);
    expect(applicationFile.status()).toBe(200);
    expect(applicationFile.headers()['content-type']).toContain('application/vnd.openxmlformats-officedocument.wordprocessingml.document');
  } finally {
    await candidateContext.close();
    await recruiterContext.close();
  }
});
