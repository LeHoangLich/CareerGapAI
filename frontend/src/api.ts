let csrf: string | undefined;
export async function refreshCsrf() {
  const response = await fetch('/api/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Không thể kết nối máy chủ.');
  csrf = (await response.json()).token;
}
export function errorMessage(code: string, status: number): string {
  if (status === 401) return 'Phiên đăng nhập đã hết hạn hoặc thông tin đăng nhập chưa đúng.';
  if (status === 403 || status === 404) return 'Bạn không có quyền truy cập tài nguyên này.';
  if (code === 'quota_exhausted') return 'Đã hết hạn mức phân tích. Bạn vẫn có thể mở CV riêng của mình.';
  if (code === 'csrf_invalid') return 'Phiên làm việc đã thay đổi. Vui lòng tải lại trang.';
  if (code === 'no_text_ocr_unsupported') return 'PDF chưa có văn bản đọc được. Hãy dùng PDF có lớp văn bản hoặc DOCX.';
  if (code === 'idempotency_conflict') return 'Yêu cầu đã được dùng cho một tài liệu khác.';
  if (['file_format', 'file_unreadable', 'no_text'].includes(code)) return 'Không đọc được tài liệu. Hãy kiểm tra file PDF/DOCX.';
  if (['file_size', 'text_limit', 'page_limit', 'docx_expansion_limit'].includes(code)) return 'Tài liệu vượt giới hạn: 4 MB, 20 trang PDF hoặc lượng văn bản cho phép.';
  if (status === 429) return 'Bạn thao tác quá nhanh hoặc đã hết hạn mức. Hãy thử lại sau.';
  return 'Chưa xử lý được yêu cầu. Hãy thử lại sau.';
}
export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.method && init.method !== 'GET') {
    if (!csrf) await refreshCsrf();
    headers.set('X-CSRF-TOKEN', csrf!);
  }
  const response = await fetch(path, { ...init, headers, credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(errorMessage(problem.title ?? '', response.status));
  }
  return response.status === 204 ? undefined as T : response.json();
}
