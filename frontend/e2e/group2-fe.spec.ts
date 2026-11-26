import { expect, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { openView, register } from './helpers';

test('FE-06, FE-08, FE-09, FE-12: #home default, guide dialog, role gating, returnTo, auth tab reset', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('[data-workspace-view="home"]')).toBeVisible();

  // FE-06: Guide button opens dialog without changing route away from #home
  await page.getByRole('button', { name: 'Cách CareerGap hoạt động' }).click();
  const guideDialog = page.getByRole('dialog', { name: 'Cách CareerGap AI hoạt động' });
  await expect(guideDialog).toBeVisible();
  await expect(page.locator('[data-workspace-view="home"]')).toBeVisible();
  await guideDialog.getByRole('button', { name: 'Đóng hộp thoại' }).click();

  // FE-06: Logo navigates back to #home
  await page.goto('/#jobs');
  await expect(page.locator('[data-workspace-view="jobs"]')).toBeVisible();
  await page.getByRole('link', { name: /CareerGap AI/ }).click();
  await expect(page.locator('[data-workspace-view="home"]')).toBeVisible();

  // FE-08: Guest signing in from #community returns to #community
  await page.goto('/#community');
  await page.locator('.header-signin').click();
  await expect(page).toHaveURL(/#access\?returnTo=%23community/);

  // FE-09: Invalid login shows alert; switching auth tab clears error message
  await page.getByLabel('Email', { exact: true }).fill('nonexistent@example.invalid');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('WrongPassword123!');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('alert')).toBeVisible();

  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);

  // FE-12: Selecting Recruiter hides Candidate displayName input
  await page.locator('select[name="role"]').selectOption('Recruiter');
  await expect(page.getByLabel('Tên hiển thị (ứng viên)')).toHaveCount(0);
  await page.locator('select[name="role"]').selectOption('Candidate');
  await expect(page.getByLabel('Tên hiển thị (ứng viên)')).toBeVisible();

  // Complete Candidate registration from #access?returnTo=%23community -> lands back on #community
  const email = `fe08-${randomUUID()}@example.invalid`;
  const password = `${randomUUID()}Aa1!`;
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByLabel('Tên hiển thị (ứng viên)').fill('Candidate FE08');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
  await expect(page.locator('[data-workspace-view="community"]')).toBeVisible();

  // FE-06: Recruiter visiting #applications is redirected to #home (not blank page)
  await page.getByRole('button', { name: 'Đăng xuất', exact: true }).click();
  await register(page, 'Recruiter');
  await page.goto('/#applications');
  await expect(page.locator('[data-workspace-view="home"]')).toBeVisible();
});

test('FE-11, FE-12: Candidate saves preferredWorkMode, loads NotoSans-Bold.ttf PDF font, confirms dirty target switch', async ({ page }) => {
  test.setTimeout(60000);
  await register(page, 'Candidate');

  // FE-12: CandidateProfilePanel preferredWorkMode select + shared checkbox
  await openView(page, 'profile');
  const profileSection = page.locator('#profile');
  await profileSection.getByLabel('Hình thức làm việc mong muốn').selectOption('remote');
  await profileSection.getByRole('checkbox', { name: 'Hình thức làm việc', exact: true }).check();
  await profileSection.getByRole('button', { name: 'Lưu hồ sơ', exact: true }).click();
  await expect(profileSection.getByRole('status').filter({ hasText: 'Hồ sơ đã được lưu.' })).toBeVisible();
  await page.reload();
  await expect(page.locator('#profile').getByLabel('Hình thức làm việc mong muốn')).toHaveValue('remote');
  await expect(page.locator('#profile').getByRole('checkbox', { name: 'Hình thức làm việc', exact: true })).toBeChecked();

  // FE-11 & FE-12: Save one CV in Builder (verifying NotoSans-Bold.ttf 200), then edit draft & verify ConfirmDialog on target switch
  await openView(page, 'cv-builder');
  const builder = page.locator('#cv-builder');
  const fontRequestPromise = page.waitForResponse(
    res => res.url().includes('/fonts/NotoSans-Bold.ttf') && res.status() === 200,
    { timeout: 25000 },
  );
  await builder.getByLabel('Tên CV').fill('CV Bold Font Check');
  await builder.getByLabel('Tên hiển thị *').fill('Nguyễn Văn Bold');
  await builder.getByLabel('Giới thiệu').fill('Kỹ sư phần mềm kiểm thử font chữ đậm.');
  await builder.getByRole('button', { name: 'Tạo bản xem trước PDF' }).click();
  await fontRequestPromise;
  await expect(builder.getByTitle('Xem trước CV PDF')).toBeVisible({ timeout: 20000 });
  await builder.getByRole('button', { name: 'Lưu thành phiên bản CV' }).click();
  await expect(builder.getByRole('status').filter({ hasText: 'Đã lưu CV riêng tư' })).toBeVisible({ timeout: 20000 });

  // Switch back to blank target, make dirty edit, then switch target -> triggers confirmation dialog
  await builder.getByLabel('CV cần cập nhật').selectOption('');
  await builder.getByLabel('Tên hiển thị *').fill('Bản nháp chưa lưu');
  const savedOption = await builder.getByLabel('CV cần cập nhật').locator('option').nth(1).getAttribute('value');
  expect(savedOption).toBeTruthy();
  await builder.getByLabel('CV cần cập nhật').selectOption(savedOption!);

  const dirtyDialog = page.getByRole('dialog', { name: 'Bỏ bản nháp đang sửa?' });
  await expect(dirtyDialog).toBeVisible();
  await dirtyDialog.getByRole('button', { name: 'Giữ bản nháp' }).click();
  await expect(builder.getByLabel('Tên hiển thị *')).toHaveValue('Bản nháp chưa lưu');

  await builder.getByLabel('CV cần cập nhật').selectOption(savedOption!);
  await expect(dirtyDialog).toBeVisible();
  await dirtyDialog.getByRole('button', { name: 'Chuyển CV và bỏ nháp' }).click();
  await expect(builder.getByLabel('Tên hiển thị *')).toHaveValue('');
});

test('[mock] FE-01..05, FE-07, FE-09, FE-10: Job detail match, CTA links, filter URL/focus sync, polling cap/pause & retry', async ({ page }) => {
  const jobId = '11111111-1111-1111-1111-111111111111';
  const jobVersionId = '22222222-2222-2222-2222-222222222222';
  const resumeVersionId = '33333333-3333-3333-3333-333333333333';
  const pairId = '44444444-4444-4444-4444-444444444444';

  let homeFail = true;
  let historyListCalls = 0;
  let pairPollCalls = 0;

  const jobDto = {
    id: jobId,
    status: 'published',
    companyId: '55555555-5555-5555-5555-555555555555',
    company: { id: '55555555-5555-5555-5555-555555555555', name: 'Test Corp', slug: 'test-corp', description: 'Tech', websiteUrl: null, location: 'Hà Nội', industry: 'IT', sizeLabel: '50', rowVersion: 1 },
    version: { id: jobVersionId, versionNo: 1, title: 'Senior Backend Engineer', description: 'Build APIs', requirementsText: 'C#, PostgreSQL', benefitsText: null, industry: 'IT', location: 'Hà Nội', employmentType: 'full_time', workMode: 'remote', experienceMin: 2, experienceMax: 5, salaryMin: 30, salaryMax: 45, salaryCurrency: 'VND', salaryPeriod: 'month', language: 'vi', contentHash: 'abc' },
    applicationDeadline: null,
    acceptingApplications: true,
    rowVersion: 1,
    publishedAt: '2026-11-20T00:00:00Z',
    closedAt: null,
    tags: ['C#'],
    viewCount: 5,
  };

  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path === '/api/auth/me') {
      await route.fulfill({ json: { authenticated: true, userId: 'cand-1', email: 'cand@example.invalid', roles: ['Candidate'] } });
      return;
    }
    if (path === '/api/auth/csrf') {
      await route.fulfill({ json: { token: 'mock-csrf-token' } });
      return;
    }
    if (path === '/api/auth/recovery-options') {
      await route.fulfill({ json: { localMailboxAvailable: false } });
      return;
    }
    if (path === '/api/resumes') {
      await route.fulfill({ json: { items: [{ id: 'res-1', title: 'Backend CV', rowVersion: 1, createdAt: '2026-11-20T00:00:00Z', updatedAt: '2026-11-20T00:00:00Z', versions: [{ id: resumeVersionId, fileId: 'file-1', versionNo: 1, documentHash: 'h1', createdAt: '2026-11-20T00:00:00Z' }] }], total: 1, page: 1, pageSize: 20 } });
      return;
    }
    if (path === '/api/applications/mine' || path === '/api/saved-jobs') {
      await route.fulfill({ json: { items: [], page: 1, pageSize: 50, total: 0 } });
      return;
    }
    if (path === '/api/jobs') {
      if (homeFail && url.searchParams.get('pageSize') === '6') {
        await route.fulfill({ status: 503, json: { title: 'temp_unavailable' } });
        return;
      }
      await route.fulfill({ json: { items: [jobDto], page: 1, pageSize: 10, total: 1 } });
      return;
    }
    if (path === `/api/jobs/${jobId}`) {
      await route.fulfill({ json: jobDto });
      return;
    }
    if (path === '/api/community/posts') {
      await route.fulfill({ json: { items: [], page: 1, pageSize: 12, total: 0 } });
      return;
    }
    if (path === '/api/matching/pairs' && route.request().method() === 'POST') {
      await route.fulfill({ status: 202, json: { id: pairId } });
      return;
    }
    if (path === `/api/matching/pairs/${pairId}`) {
      pairPollCalls += 1;
      await route.fulfill({
        json: {
          id: pairId,
          context: 'self',
          status: 'succeeded',
          errorCode: null,
          provider: 'Fake',
          score: 85,
          criteria: [{ requirementKey: 'req1', requirement: 'C#', importance: 'required', status: 'partial', evidence: [], explanation: 'Partial evidence', needsReview: false, requirementSourceType: 'document_span' }],
          location: 'Hà Nội',
          workMode: 'remote',
          salaryMin: 30,
          salaryMax: 45,
          salaryCurrency: 'VND',
          salaryPeriod: 'month',
          careerContext: null,
          criterionGroups: [],
        },
      });
      return;
    }
    if (path === `/api/learning/saved-roadmaps/${pairId}`) {
      await route.fulfill({ json: { saved: { plan: { pairId, weeks: 4, hoursPerWeek: 5, method: 'mock', schedule: [{ week: 1, hours: 5, focus: [{ requirementKey: 'req1', requirement: 'C#', importance: 'required', goal: 'Practice C#', practice: 'Build API', deliverable: 'Repo' }] }] }, completed: [], rowVersion: 1, updatedAt: '2026-11-20T00:00:00Z' } } });
      return;
    }
    if (path === '/api/matching/history') {
      historyListCalls += 1;
      await route.fulfill({ json: { items: [{ id: pairId, createdAt: '2026-11-20T00:00:00Z', status: 'succeeded', context: 'self', available: true, jobTitle: 'Senior Backend Engineer', resumeVersionNo: 1, score: 85 }], total: 1, page: 1, pageSize: 20 } });
      return;
    }
    if (path === '/api/public/resumes') {
      await route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 20 } });
      return;
    }
    if (path === '/api/resumes/public/me') {
      await route.fulfill({ json: null });
      return;
    }
    await route.fulfill({ status: 404, json: { title: 'not_found' } });
  });

  // FE-09: PublicHome shows error alert and retry button on 503, then recovers
  await page.goto('/#home');
  const homeAlert = page.locator('.public-home [role="alert"]');
  await expect(homeAlert).toBeVisible();
  homeFail = false;
  await homeAlert.getByRole('button', { name: 'Thử lại' }).click();
  await expect(page.locator('.public-home').getByRole('link', { name: 'Senior Backend Engineer' })).toBeVisible();

  // FE-07: #jobs writes filter state into URL hash and preserves it when returning from #job?id=...
  await openView(page, 'jobs');
  const browser = page.locator('.job-browser');
  await browser.getByLabel('Từ khóa', { exact: true }).fill('Backend');
  await browser.locator('.advanced-job-filters summary').click();
  await browser.getByRole('combobox', { name: 'Hình thức', exact: true }).selectOption('remote');
  await browser.getByRole('button', { name: 'Tìm việc', exact: true }).click();
  await expect(page).toHaveURL(/#jobs\?.*q=Backend.*workMode=remote/);

  // FE-01 & FE-02: Open #job?id=..., run PairAnalysis inline, open WeeklyRoadmap, verify CTA links
  await browser.getByRole('link', { name: 'Xem chi tiết việc làm' }).click();
  await expect(page.locator('[data-workspace-view="job"]')).toBeVisible();
  const matchSection = page.locator('.job-pair-match');
  await expect(matchSection.getByRole('heading', { name: 'Đối chiếu CV của bạn với tin tuyển dụng này' })).toBeVisible();
  await matchSection.getByRole('button', { name: 'Phân tích cặp CV–JD' }).click();
  await expect(matchSection.getByText('Điểm đối chiếu 85.00/100')).toBeVisible();
  await expect(matchSection.locator('.pair-next-steps a[href="#cv-builder"]').first()).toBeVisible();
  await matchSection.getByRole('button', { name: 'Xem lộ trình học theo tuần' }).click();
  await expect(matchSection.getByText('4 tuần · 5 giờ/tuần')).toBeVisible();

  // Browser Back from #job?id=... restores filters on #jobs
  await page.goBack();
  await expect(page.locator('[data-workspace-view="jobs"]')).toBeVisible();
  await expect(browser.getByLabel('Từ khóa', { exact: true })).toHaveValue('Backend');
  await expect(browser.getByRole('combobox', { name: 'Hình thức', exact: true })).toHaveValue('remote');

  // FE-07 & FE-10: AnalysisHistory retains focus on filter <select> and skips list fetch when id is in hash
  await page.locator('a[href="#analysis-history"]:visible').first().click();
  await expect(page.locator('[data-workspace-view="analysis-history"]')).toBeVisible();
  const statusSelect = page.locator('.analysis-history').getByLabel('Trạng thái phân tích');
  await statusSelect.focus();
  await statusSelect.selectOption('succeeded');
  await expect(statusSelect).toBeFocused();

  const callsBeforeDetail = historyListCalls;
  await page.goto(`/#analysis-history?id=${pairId}`);
  await expect(page.locator('.analysis-history').getByText('Điểm đối chiếu 85.00/100')).toBeVisible();
  expect(historyListCalls).toBe(callsBeforeDetail);
  expect(pairPollCalls).toBeGreaterThan(0);
});
