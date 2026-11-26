import { expect, test, type Page } from '@playwright/test';

async function mockWorkspace(page: Page, applications: () => Promise<{ status?: number; body: unknown }>, authenticated = true) {
  const unexpected: string[] = [];
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let json: unknown;
    if (path === '/api/auth/me') json = { authenticated, userId: 'mock-owner', email: 'mock@example.invalid', roles: authenticated ? ['Candidate'] : [] };
    else if (path === '/api/profile/me') json = { userId: 'mock-owner', rowVersion: 1, displayName: 'Ứng viên mock', details: { skills: [] }, sharedFields: [] };
    else if (path === '/api/auth/recovery-options') json = { localMailboxAvailable: false };
    else if (path === '/api/jobs' || path === '/api/saved-jobs') json = { items: [], page: 1, pageSize: 10, total: 0 };
    else if (path === '/api/applications/mine') {
      const result = await applications();
      await route.fulfill({ status: result.status ?? 200, json: result.body }); return;
    } else if (['/api/resumes', '/api/public/resumes'].includes(path)) json = { items: [], total: 0, page: 1, pageSize: 20 };
    else if (path === '/api/notifications') json = [];
    else if (path === '/api/resumes/public/me') json = null;
    else { unexpected.push(path); await route.fulfill({ status: 501, json: { title: 'unexpected_mock_request' } }); return; }
    await route.fulfill({ json });
  });
  return unexpected;
}

test('[mock] loading and failed application reads never masquerade as empty lists; retry recovers', async ({ page }) => {
  let unblock!: () => void;
  const gate = new Promise<void>(resolve => { unblock = resolve; });
  let fail = true;
  const unexpected = await mockWorkspace(page, async () => {
    await gate;
    return fail ? { status: 503, body: { title: 'synthetic_read_failure' } } : { body: { items: [], page: 1, pageSize: 50, total: 0 } };
  });
  await page.goto('/#applications');
  await expect(page.getByText('Đang tải đơn và thông báo…', { exact: true })).toBeVisible();
  await expect(page.locator('.application-empty, .notification-empty')).toHaveCount(0);
  unblock();
  await expect(page.getByRole('button', { name: 'Tải lại đơn và thông báo', exact: true })).toBeVisible();
  await expect(page.locator('.application-empty, .notification-empty')).toHaveCount(0);
  fail = false;
  await page.getByRole('button', { name: 'Tải lại đơn và thông báo', exact: true }).click();
  await expect(page.locator('.application-empty')).toBeVisible();
  await expect(page.locator('.notification-empty')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(unexpected).toEqual([]);
});

test('[mock] blocked storage keeps onboarding usable and keyboard skip preserves the current route', async ({ page }) => {
  await page.addInitScript(() => {
    for (const method of ['getItem', 'setItem', 'removeItem'] as const)
      Storage.prototype[method] = () => { throw new Error('Storage blocked for synthetic UI test'); };
  });
  const unexpected = await mockWorkspace(page, async () => ({ body: { items: [], page: 1, pageSize: 50, total: 0 } }), false);
  await page.goto('/#jobs');
  await page.getByRole('button', { name: 'Ẩn hướng dẫn', exact: true }).click();
  await page.getByRole('button', { name: 'Xem hướng dẫn', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Thu gọn', exact: true })).toHaveAttribute('aria-expanded', 'true');
  await page.getByRole('link', { name: 'Đến nội dung chính', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('main')).toBeFocused();
  await expect(page).toHaveURL(/#jobs$/);
  expect(unexpected).toEqual([]);
});
