export type StatusTone = 'neutral' | 'success' | 'warning' | 'danger' | 'ai';

export type StatusEntry = {
  label: string;
  tone: StatusTone;
};

const fallback = (value: string): StatusEntry => ({ label: value, tone: 'neutral' });

export const careerContextLabels: Record<string, string> = {
  career_start: 'Bắt đầu nghề',
  career_switch: 'Chuyển ngành',
  upskill: 'Nâng cao năng lực',
};

export const importanceLabels: Record<string, string> = {
  required: 'Bắt buộc',
  preferred: 'Ưu tiên',
  optional: 'Tùy chọn',
  unspecified: 'Chưa phân loại',
};

export const jobStatusMap: Record<string, StatusEntry> = {
  draft: { label: 'Bản nháp', tone: 'warning' },
  published: { label: 'Đang tuyển', tone: 'success' },
  closed: { label: 'Đã đóng', tone: 'danger' },
  archived: { label: 'Đã ẩn', tone: 'neutral' },
};

export const applicationStatusMap: Record<string, StatusEntry> = {
  submitted: { label: 'Đã nộp', tone: 'neutral' },
  reviewing: { label: 'Đang xem xét', tone: 'ai' },
  shortlisted: { label: 'Vào danh sách ngắn', tone: 'success' },
  interview: { label: 'Phỏng vấn', tone: 'success' },
  offered: { label: 'Đề nghị nhận việc', tone: 'success' },
  rejected: { label: 'Từ chối', tone: 'danger' },
  withdrawn: { label: 'Đã rút hồ sơ', tone: 'neutral' },
};

export const reportStatusMap: Record<string, StatusEntry> = {
  open: { label: 'Chờ xử lý', tone: 'warning' },
  resolved: { label: 'Đã xử lý', tone: 'success' },
  dismissed: { label: 'Đã bỏ qua', tone: 'neutral' },
};

export const businessStatusMap: Record<string, StatusEntry> = {
  pending: { label: 'Chờ xác minh', tone: 'warning' },
  verified: { label: 'Đã xác minh', tone: 'success' },
  rejected: { label: 'Từ chối xác minh', tone: 'danger' },
  unverified: { label: 'Chưa xác minh', tone: 'neutral' },
};

export const accountStatusMap: Record<string, StatusEntry> = {
  active: { label: 'Đang hoạt động', tone: 'success' },
  suspended: { label: 'Tạm khóa', tone: 'danger' },
  deletion_pending: { label: 'Đang chờ xóa', tone: 'warning' },
  deleted: { label: 'Đã xóa', tone: 'neutral' },
};

export const communityPostStatusMap: Record<string, StatusEntry> = {
  draft: { label: 'Bản nháp', tone: 'neutral' },
  pending: { label: 'Chờ duyệt', tone: 'warning' },
  published: { label: 'Đã đăng', tone: 'success' },
  hidden: { label: 'Đã ẩn', tone: 'neutral' },
  deleted: { label: 'Đã xóa', tone: 'danger' },
};

export const resumeStatusMap: Record<string, StatusEntry> = {
  draft: { label: 'Bản nháp', tone: 'neutral' },
  pending: { label: 'Đang xử lý', tone: 'warning' },
  ready: { label: 'Sẵn sàng', tone: 'success' },
  failed: { label: 'Lỗi xử lý', tone: 'danger' },
  delete_pending: { label: 'Đang chờ xóa', tone: 'warning' },
};

export function lookupStatus(map: Record<string, StatusEntry>, key: string | null | undefined): StatusEntry {
  if (!key) return fallback('Chưa xác định');
  return map[key] ?? fallback(key);
}

export function careerContextLabel(value: string | null | undefined): string {
  if (!value) return 'Chưa xác định';
  return careerContextLabels[value] ?? value;
}

export function importanceLabel(value: string | null | undefined): string {
  if (!value) return importanceLabels.unspecified;
  return importanceLabels[value] ?? value;
}
