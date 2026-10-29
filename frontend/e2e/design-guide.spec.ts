import { expect, test, type Page } from '@playwright/test';
import { openView } from './helpers';
import path from 'node:path';
import fs from 'node:fs';

const jobId = '10000000-0000-0000-0000-000000000001';
const applicationId = '20000000-0000-0000-0000-000000000001';
const job = {
  id: jobId, status: 'published', acceptingApplications: true, rowVersion: 1,
  company: { id: 'company', name: 'Công ty kiểm thử UI', location: 'Hồ Chí Minh' },
  companyId: 'company', applicationDeadline: null,
  version: { id: 'version', versionNo: 1, title: 'Lập trình ứng dụng thử nghiệm', description: 'Dữ liệu tổng hợp cho kiểm tra giao diện.', requirementsText: 'React', employmentType: 'full_time', workMode: 'remote', industry: 'CNTT', language: 'vi' },
};

async function mock(page: Page, role: 'Guest' | 'Candidate' | 'Recruiter' | 'Admin' = 'Candidate') {
  const writes: string[] = [], unexpected: string[] = [];
  let withdrawn = false;
  await page.route('**/api/**', async route => {
    const request = route.request(), url = new URL(request.url()), endpoint = url.pathname;
    if (request.method() !== 'GET') writes.push(endpoint);
    let json: unknown;
    if (endpoint === '/api/auth/me') json = { authenticated: role !== 'Guest', userId: 'mock-owner', email: 'ui@example.invalid', roles: role === 'Guest' ? [] : [role] };
    else if (endpoint === '/api/auth/csrf') json = { token: 'synthetic-csrf' };
    else if (endpoint === '/api/auth/recovery-options') json = { localMailboxAvailable: false };
    else if (endpoint === '/api/profile/me') json = { displayName: 'Ứng viên thử nghiệm', details: { skills: [] }, sharedFields: [] };
    else if (endpoint === '/api/jobs' || endpoint === '/api/jobs/mine') json = { items: [job], page: 1, pageSize: 10, total: 1 };
    else if (endpoint === '/api/companies/mine') json = [{ id: 'company', name: 'Công ty kiểm thử UI', rowVersion: 1 }];
    else if (endpoint === '/api/saved-jobs') json = { items: [], page: 1, pageSize: 50, total: 0 };
    else if (endpoint === '/api/applications/mine') json = { items: [{ id: applicationId, jobId, jobTitle: job.version.title, companyName: job.company.name, status: withdrawn ? 'withdrawn' : 'submitted', resumeVersionNo: 1, rowVersion: 7, submittedAt: '2026-10-29T00:00:00Z' }], total: 1 };
    else if (endpoint.endsWith('/history')) json = [{ fromStatus: null, toStatus: 'submitted', changedAt: '2026-10-29T00:00:00Z' }];
    else if (endpoint.endsWith('/withdraw')) { expect(request.postDataJSON()).toEqual({ rowVersion: 7 }); withdrawn = true; await route.fulfill({ status: 204 }); return; }
    else if (endpoint === '/api/reports') { expect(request.postDataJSON()).toMatchObject({ targetType: 'job', targetId: jobId, reason: 'Nội dung kiểm thử tổng hợp' }); await route.fulfill({ status: 204 }); return; }
    else if (endpoint === '/api/account/deletion-request') json = { status: 'deletion_pending', purgeAfter: '2026-11-05T00:00:00Z' };
    else if (endpoint === '/api/admin/usage') json = { aiBudgets: [{ service: 'Mô phỏng', unit: 'test', limitAmount: 10, spentAmount: 1, reservedAmount: 0 }], search: [], email: [], aiErrors: [] };
    else if (endpoint === '/api/resumes/public/me') json = null;
    else if (['/api/resumes', '/api/notifications', '/api/public/resumes', '/api/admin/accounts'].includes(endpoint) || endpoint.endsWith('/revisions')) json = [];
    else { unexpected.push(endpoint); await route.fulfill({ status: 501, json: { title: 'unexpected_mock_request' } }); return; }
    await route.fulfill({ json });
  });
  return { writes, unexpected };
}

test('[mock] guide action, one current menu, field feedback and mobile modal navigation', async ({ page }, info) => {
  const { writes, unexpected } = await mock(page, 'Guest');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/#jobs');
  await expect(page.locator('.workspace-nav a[aria-current]:visible')).toHaveCount(1);
  await page.getByRole('button', { name: 'Cách CareerGap hoạt động', exact: true }).click();
  await expect(page).toHaveURL(/#jobs$/);
  await expect(page.getByRole('button', { name: 'Thu gọn', exact: true })).toHaveAttribute('aria-expanded', 'true');
  await page.getByRole('link', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.locator('main h1')).toHaveCount(1);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  const email = page.getByLabel('Email', { exact: true });
  await expect(email).toHaveAttribute('aria-invalid', 'true');
  const description = await email.getAttribute('aria-describedby');
  await expect(page.locator(`[id="${description}"]`)).toContainText('Hãy điền');
  await email.fill('synthetic@example.invalid');
  await expect(email).not.toHaveAttribute('aria-invalid', 'true');
  await expect(email).not.toHaveAttribute('aria-describedby', /field-error/);
  await page.setViewportSize({ width: 390, height: 844 });
  const before = await page.locator('main').boundingBox();
  const trigger = page.getByRole('button', { name: 'Mở điều hướng', exact: true });
  await trigger.click();
  const drawer = page.getByRole('dialog', { name: 'Điều hướng', exact: true });
  await expect(drawer).toBeVisible();
  expect((await page.locator('main').boundingBox())?.y).toBe(before?.y);
  for (let i = 0; i < 9; i++) {
    await page.keyboard.press('Tab');
    expect(await drawer.evaluate(el => el.contains(document.activeElement))).toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(drawer).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await trigger.click();
  await page.mouse.click(380, 200);
  await expect(drawer).not.toBeVisible();
  await expect(trigger).toBeFocused();
  const evidence = process.env.CAREERGAP_E2E_EVIDENCE_DIRECTORY ?? info.outputPath('evidence');
  fs.mkdirSync(evidence, { recursive: true });
  await trigger.click();
  await page.screenshot({ path: path.join(evidence, 'design-guide-mobile-drawer.png') });
  expect(writes).toEqual([]); expect(unexpected).toEqual([]);
});

test('[mock] withdrawal and reporting only mutate after dialog confirmation', async ({ page }, info) => {
  const { writes, unexpected } = await mock(page);
  await page.goto('/#applications');
  const history = page.getByRole('button', { name: 'Lịch sử trạng thái', exact: true });
  await expect(history).toHaveAttribute('aria-expanded', 'false');
  await history.click();
  await expect(page.getByRole('button', { name: 'Ẩn lịch sử', exact: true })).toHaveAttribute('aria-expanded', 'true');
  await page.getByRole('button', { name: 'Rút hồ sơ', exact: true }).click();
  await page.getByRole('button', { name: 'Giữ hồ sơ', exact: true }).click();
  expect(writes).toEqual([]);
  await page.getByRole('button', { name: 'Rút hồ sơ', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận rút hồ sơ', exact: true }).click();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.locator('#applications')).toContainText('Đã rút');
  expect(writes).toEqual([`/api/applications/${applicationId}/withdraw`]);
  await openView(page, 'jobs');
  const report = page.getByRole('button', { name: 'Báo cáo nội dung', exact: true });
  await report.click();
  await page.keyboard.press('Escape');
  await expect(report).toBeFocused();
  expect(writes).toHaveLength(1);
  await report.click();
  const dialog = page.getByRole('dialog', { name: 'Báo cáo nội dung', exact: true });
  await dialog.getByLabel('Lý do báo cáo', { exact: true }).fill('Nội dung kiểm thử tổng hợp');
  await dialog.getByRole('button', { name: 'Gửi báo cáo', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(writes).toEqual([`/api/applications/${applicationId}/withdraw`, '/api/reports']);
  await report.click();
  const evidence = process.env.CAREERGAP_E2E_EVIDENCE_DIRECTORY ?? info.outputPath('evidence');
  fs.mkdirSync(evidence, { recursive: true });
  await page.screenshot({ path: path.join(evidence, 'design-guide-report-dialog.png') });
  expect(unexpected).toEqual([]);
});

test('[mock] account deletion dialog cancels safely and validates the password field', async ({ page }) => {
  const { writes, unexpected } = await mock(page);
  await page.goto('/#account');
  await page.getByRole('button', { name: 'Yêu cầu xóa tài khoản', exact: true }).click();
  await page.getByRole('button', { name: 'Giữ tài khoản', exact: true }).click();
  expect(writes).toEqual([]);
  await page.getByRole('button', { name: 'Yêu cầu xóa tài khoản', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Xác nhận yêu cầu xóa tài khoản', exact: true });
  await dialog.getByRole('button', { name: 'Xác nhận yêu cầu xóa', exact: true }).click();
  const password = dialog.getByLabel('Nhập mật khẩu để xác nhận', { exact: true });
  await expect(password).toHaveAttribute('aria-invalid', 'true');
  expect(writes).toEqual([]);
  await password.fill('Synthetic-validation-only');
  await dialog.getByRole('button', { name: 'Xác nhận yêu cầu xóa', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Yêu cầu xóa đang chờ', exact: true })).toBeVisible();
  expect(writes).toEqual(['/api/account/deletion-request']);
  expect(unexpected).toEqual([]);
});

test('[mock] recruiter subviews retain editor state and support keyboard, reload and small screens', async ({ page }, info) => {
  const { writes, unexpected } = await mock(page, 'Recruiter');
  await page.goto('/#recruiter-company');
  await page.getByLabel('Tên công ty', { exact: true }).fill('Nội dung công ty chưa lưu');
  await openView(page, 'recruiter-jobs');
  await expect(page.getByLabel('Tên công ty', { exact: true })).not.toBeVisible();
  await page.getByLabel('Chức danh', { exact: true }).fill('JD chưa lưu');
  await openView(page, 'recruiter-applicants');
  await expect(page.getByLabel('Chức danh', { exact: true })).not.toBeVisible();
  await expect(page.getByRole('button', { name: /Hồ sơ ứng tuyển/ })).toBeVisible();
  await openView(page, 'recruiter-company');
  await expect(page.getByLabel('Tên công ty', { exact: true })).toHaveValue('Nội dung công ty chưa lưu');
  await openView(page, 'recruiter-jobs');
  await expect(page.getByLabel('Chức danh', { exact: true })).toHaveValue('JD chưa lưu');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(page.locator('.recruiter-center-tabs a').first()).toHaveCSS('transition-duration', '0s');
  await page.reload();
  await expect(page.locator('[data-workspace-view]')).toHaveAttribute('data-workspace-view', 'recruiter-jobs');
  for (const width of [1440, 1024, 768, 390, 320]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(page.locator('main h1')).toHaveCount(1);
  }
  await page.setViewportSize({ width: 1366, height: 900 });
  const evidence = process.env.CAREERGAP_E2E_EVIDENCE_DIRECTORY ?? info.outputPath('evidence');
  fs.mkdirSync(evidence, { recursive: true });
  await page.screenshot({ path: path.join(evidence, 'design-guide-recruiter-tabs.png'), fullPage: true });
  expect(writes).toEqual([]); expect(unexpected).toEqual([]);
});

test('[mock] PDF language invalidates previews, exports both languages, and respects reduced motion', async ({ page }, info) => {
  page.on('console', message => { if (message.type() === 'error') console.error(message.text()); });
  const { writes, unexpected } = await mock(page);
  await page.goto('/#cv-builder');
  await page.getByLabel('Tên hiển thị *', { exact: true }).fill('Ứng viên kiểm tra ngôn ngữ');
  await page.getByLabel('Giới thiệu', { exact: true }).fill('Nội dung nguồn không thay đổi khi đổi ngôn ngữ tiêu đề.');
  await page.getByLabel('Kỹ năng, mỗi dòng một mục', { exact: true }).fill('React');
  await page.getByRole('button', { name: 'Thêm kinh nghiệm', exact: true }).click();
  const experience = page.getByRole('group', { name: 'Kinh nghiệm 1', exact: true });
  await experience.getByLabel('Chức danh', { exact: true }).fill('Developer');
  await experience.getByLabel('Đơn vị', { exact: true }).fill('Công ty kiểm thử');
  await experience.getByLabel('Mô tả', { exact: true }).fill('Built React tools.');
  await page.getByRole('button', { name: 'Thêm dự án', exact: true }).click();
  const project = page.getByRole('group', { name: 'Dự án 1', exact: true });
  await project.getByLabel('Tên dự án', { exact: true }).fill('Ứng dụng thử nghiệm');
  await project.getByLabel('Mô tả', { exact: true }).fill('Verified source data.');
  await page.getByRole('button', { name: 'Thêm học vấn', exact: true }).click();
  const education = page.getByRole('group', { name: 'Học vấn 1', exact: true });
  await education.getByLabel('Chương trình/ngành', { exact: true }).fill('CNTT');
  await education.getByLabel('Trường/cơ sở đào tạo', { exact: true }).fill('Trường kiểm thử');
  await page.getByLabel('Chứng chỉ, mỗi dòng một mục', { exact: true }).fill('Chứng chỉ tổng hợp');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(page.locator('.resume-center-tabs a').first()).toHaveCSS('transition-duration', '0s');
  const evidence = process.env.CAREERGAP_E2E_EVIDENCE_DIRECTORY ?? info.outputPath('evidence');
  fs.mkdirSync(evidence, { recursive: true });
  for (const [index, language] of ['vi', 'en', 'vi'].entries()) {
    await page.getByLabel('Ngôn ngữ tiêu đề PDF', { exact: true }).selectOption(language);
    await expect(page.getByRole('button', { name: 'Lưu thành phiên bản CV', exact: true })).toBeDisabled();
    await expect(page.getByTitle('Xem trước CV PDF')).toHaveCount(0);
    await page.getByRole('button', { name: 'Tạo bản xem trước PDF', exact: true }).click();
    await expect(page.getByTitle('Xem trước CV PDF')).toBeVisible({ timeout: 20000 });
    const downloading = page.waitForEvent('download');
    await page.getByRole('link', { name: 'Tải PDF bản xem trước', exact: true }).click();
    await (await downloading).saveAs(path.join(evidence, `design-guide-${index}-${language}.pdf`));
    await expect(page.getByLabel('Giới thiệu', { exact: true })).toHaveValue('Nội dung nguồn không thay đổi khi đổi ngôn ngữ tiêu đề.');
  }
  expect(writes).toEqual([]); expect(unexpected).toEqual([]);
});

test('[mock] computed token contrast, meaningful text sizes and mobile admin table', async ({ page }) => {
  const { writes, unexpected } = await mock(page, 'Admin');
  await page.goto('/#administration');
  await page.getByRole('button', { name: 'Hạn mức và lỗi', exact: true }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('.admin-panel td').first()).toHaveCSS('font-size', '14px');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const colors = await page.evaluate(() => {
    const style = getComputedStyle(document.documentElement);
    return Object.fromEntries(['text-tertiary', 'text-caption', 'text-placeholder', 'text-secondary', 'surface-secondary', 'status-draft-text', 'status-draft-bg', 'status-closed-text', 'status-closed-bg', 'ai-text', 'ai-bg', 'badge-bg', 'badge-text', 'bg'].map(key => [key, style.getPropertyValue(`--color-${key}`).trim()]));
  });
  const luminance = (hex: string) => {
    const channels = hex.replace('#', '').match(/../g)!.map(x => parseInt(x, 16) / 255).map(x => x <= .04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4);
    return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
  };
  for (const [text, background] of [['text-tertiary', 'bg'], ['text-caption', '#ffffff'], ['text-placeholder', '#ffffff'], ['ai-text', 'ai-bg'], ['badge-text', 'badge-bg'], ['text-secondary', 'surface-secondary'], ['status-draft-text', 'status-draft-bg'], ['status-closed-text', 'status-closed-bg']]) {
    const a = luminance(colors[text]), b = luminance(colors[background] ?? background);
    expect((Math.max(a,b) + .05) / (Math.min(a,b) + .05)).toBeGreaterThanOrEqual(4.5);
  }
  expect(writes).toEqual([]); expect(unexpected).toEqual([]);
});

test('[mock] sticky elevation appears only while pinned and clears on a short viewport', async ({ page }) => {
  const { writes, unexpected } = await mock(page);
  await page.route('**/api/jobs?**', route => route.fulfill({ json: {
    items: Array.from({ length: 8 }, (_, index) => ({ ...job, id: `job-${index}`, version: { ...job.version, title: `Cơ hội thử nghiệm ${index + 1}` } })),
    page: 1, pageSize: 10, total: 8,
  } }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/#jobs');
  const controls = page.locator('.job-discovery-controls');
  await expect(controls).toHaveAttribute('data-sticky-fits', 'true');
  await expect(controls.locator(':scope > .matching-panel')).toHaveCSS('box-shadow', 'none');
  await page.mouse.wheel(0, 700);
  await expect(controls).toHaveAttribute('data-sticky-active', 'true');
  await expect(controls.locator(':scope > .matching-panel')).not.toHaveCSS('box-shadow', 'none');
  await page.setViewportSize({ width: 1440, height: 300 });
  await expect(controls).toHaveAttribute('data-sticky-fits', 'false');
  await expect(controls).toHaveCSS('position', 'static');
  await expect(controls.locator(':scope > .matching-panel')).toHaveCSS('box-shadow', 'none');
  expect(writes).toEqual([]); expect(unexpected).toEqual([]);
});
