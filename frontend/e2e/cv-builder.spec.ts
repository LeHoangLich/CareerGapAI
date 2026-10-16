import { expect, test } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';

test('candidate reviews, saves, reopens, and versions a Vietnamese template CV PDF', async ({ page }) => {
  test.setTimeout(60000);
  const suffix = randomUUID();
  const email = `candidate-builder-${suffix}@example.invalid`;
  const password = `${randomBytes(24).toString('base64')}Aa1!`;
  const title = `CV thử nghiệm ${suffix}`;
  const initialSummary = Array.from({ length: 75 }, () => 'Ứng viên tự nhập nội dung tiếng Việt.').join(' ');
  expect(initialSummary.length).toBeLessThanOrEqual(3000);

  await page.goto('/');
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
  await page.locator('input[name="password"]').evaluateAll(inputs => inputs.forEach(input => { (input as HTMLInputElement).value = ''; }));

  const builder = page.locator('.resume-builder');
  await expect(builder.getByRole('heading', { name: 'Tạo CV từ thông tin bạn cung cấp' })).toBeVisible();
  await builder.getByLabel('Tên CV').fill(title);
  await builder.getByLabel('Tên hiển thị *').fill('Nguyễn Ứng Viên');
  await builder.getByLabel('Tiêu đề nghề nghiệp').fill('Kỹ sư phần mềm');
  await builder.getByLabel('Email liên hệ').fill(email);
  await builder.getByLabel('Địa điểm').fill('Thành phố Hồ Chí Minh');
  await builder.getByLabel('Giới thiệu').fill(initialSummary);
  await builder.getByLabel('Kỹ năng, mỗi dòng một mục').fill('React\nTypeScript\nPostgreSQL');
  await builder.getByRole('button', { name: 'Thêm kinh nghiệm' }).click();
  const experience = builder.getByRole('group', { name: 'Kinh nghiệm 1' });
  await experience.getByLabel('Chức danh').fill('Lập trình viên');
  await experience.getByLabel('Đơn vị').fill('Công ty mẫu');
  await experience.getByLabel('Thời gian').fill('2024–2026');
  await experience.getByLabel('Mô tả', { exact: true }).fill('Phát triển ứng dụng thử nghiệm và kiểm thử dữ liệu.');
  await builder.getByRole('button', { name: 'Thêm dự án' }).click();
  const project = builder.getByRole('group', { name: 'Dự án 1' });
  await project.getByLabel('Tên dự án').fill('Ứng dụng tuyển dụng');
  await project.getByLabel('Bối cảnh/vai trò').fill('Dự án cá nhân');
  await project.getByLabel('Mô tả', { exact: true }).fill('Xây giao diện hỗ trợ quản lý hồ sơ ứng viên.');
  await builder.getByRole('button', { name: 'Thêm học vấn' }).click();
  const education = builder.getByRole('group', { name: 'Học vấn 1' });
  await education.getByLabel('Chương trình/ngành').fill('Công nghệ thông tin');
  await education.getByLabel('Trường/cơ sở đào tạo').fill('Trường đại học mẫu');
  await education.getByLabel('Thời gian').fill('2020–2024');
  await builder.getByLabel('Chứng chỉ, mỗi dòng một mục').fill('Chứng chỉ giả lập');

  await builder.getByRole('button', { name: 'Tạo bản xem trước PDF' }).click();
  await expect(builder.getByTitle('Xem trước CV PDF')).toBeVisible({ timeout: 20000 });
  await builder.getByRole('button', { name: 'Lưu thành phiên bản CV' }).click();
  await expect(builder.getByRole('status').filter({ hasText: 'Đã lưu CV riêng tư, phiên bản mới và revision 1.' })).toBeVisible({ timeout: 20000 });

  const resumes = await page.request.get('/api/resumes?page=1');
  expect(resumes.ok()).toBeTruthy();
  const listed = await resumes.json() as { id: string; title: string; versions: { id: string; fileId: string; documentHash: string; versionNo: number }[] }[];
  const savedResume = listed.find(item => item.versions.some(version => version.versionNo === 1 && item.title.endsWith('.pdf')));
  expect(savedResume).toBeTruthy();
  const savedVersion = savedResume!.versions[0];
  const file = await page.request.get(`/api/files/${savedVersion.fileId}`);
  expect(file.ok()).toBeTruthy();
  expect((await file.body()).subarray(0, 5).toString()).toBe('%PDF-');
  const revisionResponse = await page.request.get(`/api/resume-versions/${savedVersion.id}/revisions`);
  const revisions = await revisionResponse.json() as { origin: string; schemaVersion: number; structuredData: { displayName: string; summary: string; skills: string[] }; evidence: { field: string; sourceType: string }[] }[];
  expect(revisions[0].origin).toBe('user_entered');
  expect(revisions[0].schemaVersion).toBe(2);
  expect(revisions[0].structuredData.displayName).toBe('Nguyễn Ứng Viên');
  expect(revisions[0].structuredData.skills).toEqual(['React', 'TypeScript', 'PostgreSQL']);
  expect(revisions[0].evidence.every(item => item.sourceType === 'user_statement')).toBeTruthy();

  await page.reload();
  const reopenedBuilder = page.locator('.resume-builder');
  await expect(reopenedBuilder.getByRole('heading', { name: 'Tạo CV từ thông tin bạn cung cấp' })).toBeVisible();
  const resumeOption = reopenedBuilder.getByLabel('CV cần cập nhật');
  await resumeOption.selectOption(savedResume!.id);
  await reopenedBuilder.getByRole('button', { name: 'Nạp revision mẫu' }).click();
  await expect(reopenedBuilder.getByLabel('Tên hiển thị *')).toHaveValue('Nguyễn Ứng Viên');
  await expect(reopenedBuilder.getByLabel('Giới thiệu')).toHaveValue(initialSummary);

  await reopenedBuilder.getByLabel('Giới thiệu').fill('Đã chỉnh sửa nội dung; phiên bản trước vẫn được giữ lại nguyên vẹn.');
  await reopenedBuilder.getByRole('button', { name: 'Tạo bản xem trước PDF' }).click();
  await expect(reopenedBuilder.getByTitle('Xem trước CV PDF')).toBeVisible({ timeout: 20000 });
  await reopenedBuilder.getByRole('button', { name: 'Lưu thành phiên bản CV' }).click();
  await expect(reopenedBuilder.getByRole('status').filter({ hasText: 'Đã lưu CV riêng tư, phiên bản mới và revision 1.' })).toBeVisible({ timeout: 20000 });
  await expect(page.locator('.resumes > li').filter({ hasText: savedResume!.title })).toContainText('2 phiên bản');
});
