import { expect, type Page } from '@playwright/test';

export type View = 'jobs' | 'private-resumes' | 'cv-builder' | 'profile' | 'applications' | 'public-resumes' | 'account' | 'recruiter-company' | 'recruiter-applicants' | 'recruiter-jobs' | 'shared-profile' | 'administration';

export async function openView(page: Page, view: View) {
  if (['recruiter-company', 'recruiter-applicants'].includes(view) && !await page.locator(`a[href="#${view}"]:visible`).count())
    await openView(page, 'recruiter-jobs');
  if (['cv-builder', 'profile'].includes(view) && !await page.locator(`a[href="#${view}"]:visible`).count())
    await openView(page, 'private-resumes');
  if (!await page.locator(`a[href="#${view}"]:visible`).count()) {
    const menu = page.getByRole('button', { name: 'Mở điều hướng', exact: true });
    if (await menu.isVisible()) await menu.click();
  }
  await page.locator(`a[href="#${view}"]:visible`).first().click();
  await expect(page.locator('[data-workspace-view]')).toHaveCount(1);
  await expect(page.locator('[data-workspace-view]')).toHaveAttribute('data-workspace-view', view);
}

export async function register(page: Page, role: 'Candidate' | 'Recruiter' = 'Candidate') {
  const email = `ui-${crypto.randomUUID()}@example.invalid`;
  const password = `${crypto.randomUUID()}Aa1!`;
  await page.goto('/#access');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.locator('select[name="role"]').selectOption(role);
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất', exact: true })).toBeVisible();
  return { email, password };
}
