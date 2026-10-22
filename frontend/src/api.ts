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
  if (code === 'redaction_not_ready') return 'Bản CV ẩn liên hệ chưa sẵn sàng; hệ thống không cung cấp file gốc cho candidate khác.';
  if (code === 'public_resume_conflict') return 'Lựa chọn CV công khai vừa thay đổi. Hãy tải lại trạng thái rồi thử lại.';
  if (code === 'resume_revision_conflict') return 'Dữ liệu CV vừa được cập nhật ở nơi khác. Hãy tải lại kết quả trích xuất trước khi xác nhận.';
  if (code === 'invalid_resume_builder_revision') return 'Hãy kiểm tra độ dài và các mục đã nhập trong CV rồi thử lại.';
  if (code === 'resume_builder_content_required') return 'Hãy nhập ít nhất một nội dung nghề nghiệp trước khi lưu CV.';
  if (code === 'resume_builder_content_too_large') return 'Nội dung CV vượt giới hạn lưu của bản thử nghiệm.';
  if (code === 'resume_revision_limit') return 'Phiên bản CV đã đạt giới hạn revision.';
  if (code === 'invalid_revision_confirmation') return 'Hãy kiểm tra các trường dữ liệu và thông tin bạn tự bổ sung trước khi xác nhận.';
  if (code === 'parse_timeout') return 'Tài liệu mất quá nhiều thời gian để đọc. Hãy dùng bản PDF/DOCX đơn giản hơn hoặc nhập thông tin thủ công.';
  if (code === 'parser_unavailable') return 'Bộ đọc tài liệu chưa sẵn sàng. Hãy thử lại sau.';
  if (code === 'full_cv_output_budget_required') return 'Phân tích CV đầy đủ chưa được cấu hình hạn mức đầu ra. Bạn vẫn có thể nhập CV thủ công.';
  if (code === 'confirmed_resume_required') return 'CV chưa có dữ liệu xác nhận phù hợp với thời điểm phân tích. Hãy rà soát CV trước.';
  if (code === 'confirmed_job_required') return 'JD chưa có yêu cầu được xác nhận phù hợp với thời điểm phân tích.';
  if (code === 'no_eligible_cv_facts') return 'CV công khai chưa có thông tin xác nhận từ tài liệu được phép đối chiếu.';
  if (code === 'no_assessable_requirements') return 'JD chưa có tiêu chí đủ đầu vào để chấm điểm.';
  if (code === 'matching_input_limit') return 'CV/JD vượt giới hạn phân tích thử nghiệm. Hãy dùng đầu vào ngắn hơn.';
  if (code === 'match_output_budget_required') return 'Chưa cấu hình hạn mức đầu ra cho phân tích cặp CV–JD.';
  if (code === 'user_quota_exhausted') return 'Đã hết hạn mức AI của tài khoản trong kỳ này.';
  if (code === 'user_concurrency_limit') return 'Đã đủ số phân tích đang chạy. Hãy đợi rồi thử lại.';
  if (code === 'ai_output_truncated') return 'Phản hồi phân tích chưa đầy đủ nên chưa được lưu. Hãy thử tài liệu ngắn hơn hoặc nhập thông tin thủ công.';
  if (code === 'ai_completion_invalid' || code === 'ai_output_empty') return 'Dịch vụ chưa trả được kết quả phân tích đầy đủ. Hãy thử lại sau hoặc nhập thông tin thủ công.';
  if (code === 'revision_data_unavailable') return 'Bản trích xuất này không còn đọc được. Hãy chạy lại trích xuất trên đúng phiên bản CV.';
  if (code === 'job_revision_conflict') return 'Phiên bản JD vừa được cập nhật. Hãy tải lại danh sách yêu cầu trước khi xác nhận.';
  if (code === 'invalid_job_revision_confirmation') return 'Hãy chọn yêu cầu và phân loại bắt buộc, tùy chọn hoặc chưa xác định hợp lệ.';
  if (code === 'job_revision_data_unavailable') return 'Bản trích xuất yêu cầu JD này không còn đọc được.';
  if (code === 'job_revision_limit') return 'JD đã đạt giới hạn số revision.';
  if (code === 'job_requirement_limit') return 'JD có quá nhiều dòng yêu cầu cho bản thử nghiệm này. Hãy gộp các dòng liên quan rồi thử lại.';
  if (code === 'redacted_file_unavailable') return 'Bản CV đã ẩn liên hệ chưa thể mở. File gốc vẫn được giữ riêng.';
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
