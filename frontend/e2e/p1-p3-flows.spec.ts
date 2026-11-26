import { expect, test, type Page } from '@playwright/test';

async function mockP1P3(page: Page) {
  const unexpected: string[] = [];
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let json: unknown;
    if (path === '/api/auth/me') json = { authenticated: true, userId: 'u-1', email: 'p1p3@example.invalid', roles: ['Candidate'] };
    else if (path === '/api/auth/csrf') json = { token: 'csrf' };
    else if (path === '/api/auth/recovery-options') json = { localMailboxAvailable: false };
    else if (path === '/api/profile/me') json = { userId: 'u-1', rowVersion: 1, displayName: 'Ứng viên P1P3', details: { skills: ['TypeScript'] }, sharedFields: [] };
    else if (path === '/api/jobs' || path === '/api/saved-jobs' || path === '/api/applications/mine') json = { items: [], page: 1, pageSize: 10, total: 0 };
    else if (path === '/api/resumes' || path === '/api/public/resumes') json = { items: [], page: 1, pageSize: 20, total: 0 };
    else if (path === '/api/resumes/public/me') json = null;
    else if (path === '/api/community/events') { await route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' }); return; }
    else if (path === '/api/notifications' || path === '/api/community/suggestions' || path === '/api/community/trending-searches') json = [];
    else if (path === '/api/locations') json = { items: [] };
    else if (path === '/api/community/me') json = { userId: 'u-1', handle: 'ung_vien_1', displayName: 'Ứng viên P1P3', bio: '', avatarId: null, allowMentions: true, rowVersion: 1, followers: 0, following: 0, posts: 1, isFollowing: false, showCareer: false, career: null };
    else if (path === '/api/community/people') json = [];
    else if (path === '/api/community/posts') json = {
      items: [{
        id: 'post-1', authorId: 'u-1', author: 'Ứng viên P1P3', handle: 'ung_vien_1', avatarId: null,
        title: 'Kinh nghiệm phỏng vấn Backend .NET', slug: 'kn-dotnet', summary: 'Chia sẻ thực tế.',
        body: '1. Chuẩn bị kiến thức\n- Hệ thống\n- Database', category: 'interview', tags: ['dotnet'],
        thumbnailId: null, status: 'draft', rowVersion: 1, createdAt: '2026-11-20T00:00:00Z', publishedAt: null,
        votes: 2, comments: 0, bookmarks: 1, views: 10, voted: false, bookmarked: false,
      }],
      page: 1, pageSize: 12, total: 1,
    };
    else if (path === '/api/matching/history') json = {
      items: [{
        id: '11111111-2222-3333-4444-555555555555', createdAt: '2026-11-20T00:00:00Z',
        status: 'succeeded', context: 'self', available: true, jobTitle: 'Backend Developer',
        resumeVersionNo: 1, score: 82.5,
      }],
      page: 1, pageSize: 20, total: 1,
    };
    else { unexpected.push(path); await route.fulfill({ status: 501, json: { title: 'unexpected' } }); return; }
    await route.fulfill({ json });
  });
  return unexpected;
}

test('[mock] P1-P3 flows: PublicHome grid, Community sub-tabs & ConfirmDialog, AnalysisHistory ConfirmDialog', async ({ page }) => {
  const unexpected = await mockP1P3(page);
  await page.setViewportSize({ width: 1280, height: 800 });

  // 1. PublicHome (#home)
  await page.goto('/#home');
  await expect(page.getByRole('heading', { name: 'Kết nối hồ sơ minh bạch, đối chiếu bằng chứng CV–JD.' })).toBeVisible();
  await expect(page.locator('.public-home .card-grid-3 > article')).toHaveCount(3);
  await page.getByRole('link', { name: 'Thử đối chiếu CV–JD', exact: true }).click();
  await expect(page).toHaveURL(/#jobs\?quickMatch=1$/);

  // 2. Community (#community) uses horizontal sub-tabs instead of dual sidebar and ConfirmDialog for delete
  await page.goto('/#community');
  await expect(page.getByRole('navigation', { name: 'Không gian cộng đồng' })).toBeVisible();
  await expect(page.locator('.social-sidebar')).toHaveCount(0);
  await page.getByRole('button', { name: 'Xóa bài', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Xóa bài viết' })).toBeVisible();
  await page.getByRole('button', { name: 'Hủy', exact: true }).click();

  // 3. AnalysisHistory (#analysis-history) uses ConfirmDialog for hiding history
  await page.goto('/#analysis-history');
  await expect(page.getByRole('heading', { name: 'Lịch sử phân tích', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Ẩn khỏi lịch sử', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Ẩn khỏi lịch sử phân tích' })).toBeVisible();
  await page.getByRole('button', { name: 'Hủy', exact: true }).click();

  expect(unexpected).toEqual([]);
});
