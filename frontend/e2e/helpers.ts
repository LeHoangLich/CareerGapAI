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

export async function verifyRecruiterBusiness(
  recruiterPage: Page,
  recruiterEmail: string,
  baseURL?: string,
) {
  const adminEmail = process.env.DEMO_ADMIN_EMAIL;
  const adminPassword = process.env.DEMO_ADMIN_PASSWORD;
  if (!adminEmail || !adminPassword) {
    throw new Error('DEMO_ADMIN_EMAIL and DEMO_ADMIN_PASSWORD must be set for recruiter business verification.');
  }

  const getCsrf = async (req: Page['request']) => {
    const res = await req.get('/api/auth/csrf');
    expect(res.ok()).toBeTruthy();
    const body = (await res.json()) as { token?: string };
    return body.token ?? '';
  };

  const csrfToken = await getCsrf(recruiterPage.request);
  const companiesRes = await recruiterPage.request.get('/api/companies/mine');
  expect(companiesRes.ok()).toBeTruthy();
  const companies = (await companiesRes.json()) as Array<{ id: string }>;
  const companyId = companies[0]?.id;
  expect(companyId).toBeTruthy();

  const primaryRes = await recruiterPage.request.post(`/api/recruiter/business/primary/${companyId}`, {
    headers: { 'X-CSRF-TOKEN': csrfToken },
  });
  expect(primaryRes.ok()).toBeTruthy();

  const emailReqRes = await recruiterPage.request.post('/api/recruiter/business/email/request', {
    headers: { 'X-CSRF-TOKEN': csrfToken },
  });
  expect(emailReqRes.ok()).toBeTruthy();

  const mailboxRes = await recruiterPage.request.get('/api/dev/mailbox');
  expect(mailboxRes.ok()).toBeTruthy();
  const mailbox = (await mailboxRes.json()) as Array<{
    to?: string;
    recipient?: string;
    link?: string;
    body?: string;
    text?: string;
    html?: string;
  }>;
  const mailItem = [...mailbox].reverse().find((item) => {
    const recipient = item.to ?? item.recipient ?? '';
    const content = `${item.link ?? ''} ${item.body ?? ''} ${item.text ?? ''} ${item.html ?? ''}`;
    return recipient.toLowerCase() === recruiterEmail.toLowerCase() && content.includes('businessEmailToken=');
  });
  expect(mailItem).toBeTruthy();
  const content = `${mailItem?.link ?? ''} ${mailItem?.body ?? ''} ${mailItem?.text ?? ''} ${mailItem?.html ?? ''}`;
  const tokenMatch = content.match(/businessEmailToken=([^&#\s]+)/);
  expect(tokenMatch).toBeTruthy();
  const emailToken = decodeURIComponent(tokenMatch![1]);

  const confirmRes = await recruiterPage.request.post('/api/recruiter/business/email/confirm', {
    headers: { 'X-CSRF-TOKEN': csrfToken },
    data: { token: emailToken },
  });
  expect(confirmRes.ok()).toBeTruthy();

  const bizGetRes = await recruiterPage.request.get('/api/recruiter/business');
  expect(bizGetRes.ok()).toBeTruthy();
  const bizData = (await bizGetRes.json()) as { identity?: { rowVersion?: number } };
  expect(bizData.identity).toBeTruthy();

  const taxCode = String(Math.floor(1000000000 + Math.random() * 8999999999));
  const bizPutRes = await recruiterPage.request.put('/api/recruiter/business', {
    headers: { 'X-CSRF-TOKEN': csrfToken },
    data: {
      legalName: 'Cong ty E2E',
      taxCode,
      rowVersion: bizData.identity!.rowVersion,
    },
  });
  expect(bizPutRes.ok()).toBeTruthy();

  const browser = recruiterPage.context().browser();
  if (!browser) throw new Error('Browser instance unavailable');
  const adminContext = await browser.newContext({ baseURL });
  try {
    const adminCsrf = await getCsrf(adminContext.request);
    const loginRes = await adminContext.request.post('/api/auth/login', {
      headers: { 'X-CSRF-TOKEN': adminCsrf },
      data: { email: adminEmail, password: adminPassword },
    });
    expect(loginRes.ok()).toBeTruthy();
    const postLoginCsrf = await getCsrf(adminContext.request);

    const listRes = await adminContext.request.get('/api/admin/businesses?page=1');
    expect(listRes.ok()).toBeTruthy();
    const listBody = (await listRes.json()) as {
      items?: Array<{ userId: string; email?: string; rowVersion?: number }>;
    };
    const target = listBody.items?.find(
      (item) => (item.email ?? '').toLowerCase() === recruiterEmail.toLowerCase(),
    );
    expect(target).toBeTruthy();

    const verifyRes = await adminContext.request.post(`/api/admin/businesses/${target!.userId}`, {
      headers: { 'X-CSRF-TOKEN': postLoginCsrf },
      data: {
        action: 'verify',
        reason: 'E2E verified',
        rowVersion: target!.rowVersion,
      },
    });
    expect(verifyRes.ok()).toBeTruthy();
  } finally {
    await adminContext.close();
  }

  await recruiterPage.reload();
}
