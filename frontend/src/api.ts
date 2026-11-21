let csrf: string | undefined;
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;
  constructor(code: string, status: number) {
    super(errorMessage(code, status));
    this.name = 'ApiError'; this.code = code; this.status = status;
  }
}
async function fetchResponse(path: string, init: RequestInit): Promise<Response> {
  try { return await fetch(path, init); }
  catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error;
    throw new ApiError('', 0);
  }
}
export async function refreshCsrf() {
  const response = await fetchResponse('/api/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new ApiError('', response.status);
  csrf = (await response.json()).token;
}
export function errorMessage(code: string, status: number): string {
  if ((status >= 500 && code !== 'ai_disabled') || status === 0)
    return 'Máy chủ gặp sự cố hoặc kết nối bị gián đoạn. Vui lòng thử lại sau.';
  const messages: Record<string,string> = {
    invalid_cover_letter:'Thư ứng tuyển tối đa 5.000 ký tự và không chứa ký tự không hợp lệ.',
    invalid_community_report_query: 'Bộ lọc báo cáo cộng đồng không hợp lệ.',
  community_content_unavailable: 'Nội dung không còn khả dụng để gỡ. Hãy tải lại chi tiết trước khi xử lý.',
  community_conflict:'Nội dung đã thay đổi ở nơi khác. Hãy tải lại trước khi sửa hoặc kiểm duyệt.',
    community_not_found:'Nội dung không còn công khai hoặc bạn không có quyền xem.',
    community_profile_required:'Hãy tạo hồ sơ cộng đồng để tương tác.',
    community_handle_taken:'Handle này đã được sử dụng. Hãy chọn tên khác.',
    community_handle_immutable:'Handle không đổi sau khi đã tạo hồ sơ.',
    invalid_community_profile:'Kiểm tra tên, handle và độ dài giới thiệu.',
    invalid_community_post:'Bài viết cần tiêu đề 5–200 ký tự, nội dung 20–30.000 ký tự và tối đa 8 tag.',
    invalid_community_tags:'Mỗi tag tối đa 40 ký tự, chỉ dùng chữ, số và ký hiệu công nghệ thông dụng.',
    community_image_invalid:'Dùng ảnh PNG/JPEG hợp lệ, tối đa 2 MB và 8 triệu điểm ảnh.',
    community_image_in_use:'Ảnh đang được dùng. Hãy bỏ ảnh khỏi hồ sơ và bài viết trước khi xóa.',
    community_image_limit:'Bạn đã có 30 ảnh. Hãy xóa ảnh chưa dùng trước khi tải thêm.',
    community_image_not_owned:'Chỉ được chọn ảnh do chính bạn tải lên.',
    community_report_exists:'Bạn đã có báo cáo đang được xử lý cho nội dung này.',
    community_reason_required:'Hãy ghi lý do kiểm duyệt từ 3 ký tự.',
    community_invalid_transition:'Trạng thái bài đã thay đổi. Hãy tải lại danh sách.',
    community_mention_limit:'Mỗi nội dung chỉ được nhắc tối đa 10 người.',
    community_reply_invalid:'Chỉ trả lời bình luận gốc còn hiển thị trong cùng bài.',
    community_self_follow:'Bạn không thể theo dõi chính mình.',
    invalid_community_comment:'Bình luận cần từ 1 đến 4.000 ký tự.',
    invalid_community_report:'Lý do báo cáo cần từ 3 đến 1.000 ký tự.',

    invalid_or_expired_reset:'Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.',
    password_policy:'Mật khẩu mới phải có độ dài từ 12 đến 256 ký tự.',
    ai_disabled:'AI đang tắt trong môi trường này. Yêu cầu mới chưa được tạo; bạn vẫn có thể xem, tải CV và thao tác thủ công.',
    writing_output_budget_required:'Chưa cấu hình hạn mức đầu ra cho đề xuất viết CV.', writing_output_invalid:'Đề xuất chưa đạt kiểm tra dữ kiện nguồn; CV giữ nguyên.',
    writing_input_limit:'Dữ kiện CV vượt giới hạn soạn thử nghiệm.', writing_facts_required:'CV chưa có đủ dữ kiện nghề nghiệp đã xác nhận trong giới hạn soạn thử nghiệm.', invalid_writing_language:'Chọn tiếng Việt hoặc tiếng Anh cho đề xuất.',
    writing_not_ready:'Đề xuất chưa xử lý xong.', invalid_writing_decision:'Hãy kiểm tra nội dung và độ dài trước khi duyệt.', writing_already_decided:'Đề xuất này đã được duyệt hoặc bỏ trước đó.',
    search_not_configured:'Search thật chưa được cấu hình và xác nhận hạn mức. Kết quả đối chiếu vẫn xem được.', search_quota_exhausted:'Đã hết hạn mức search; kết quả đối chiếu vẫn xem được.',
    search_provider_quota:'Provider báo hết hạn mức search; yêu cầu này cần đối soát credit.', search_unavailable:'Search chưa phản hồi; credit đang được giữ để đối soát.', search_usage_unknown:'Chưa xác định được credit đã dùng; hệ thống giữ phần dự trù.',
    search_period_conflict:'Kỳ search chồng với sổ đã có. Cần đối soát cấu hình trước khi tiếp tục.', learning_gap_required:'Nguồn học chỉ tìm cho tiêu chí chưa có bằng chứng đầy đủ.', learning_topic_unavailable:'Danh mục hiện tại chưa hỗ trợ chủ đề này.',
    invalid_admin_action:'Chọn thao tác và ghi lý do hợp lệ.', admin_account_protected:'Không thể khóa tài khoản quản trị bằng thao tác này.', account_state_conflict:'Trạng thái tài khoản đã thay đổi. Hãy tải lại danh sách.', report_conflict:'Báo cáo đã được xử lý hoặc vừa thay đổi.', report_limit:'Đã đạt giới hạn báo cáo trong ngày.', report_exists:'Bạn đã gửi báo cáo cho nội dung này và đang chờ xử lý.', roadmap_conflict:'Lộ trình đã thay đổi ở phiên khác. Hãy tải lại lộ trình đã lưu trước khi tiếp tục.', roadmap_no_gaps:'Không có mục tiêu cần học để lưu.', invalid_job_tags:'Chọn tối đa 10 tag hợp lệ từ danh mục CareerGap.', recruiter_company_already_bound:'Tài khoản recruiter đã gắn với một doanh nghiệp. Hãy cập nhật hồ sơ doanh nghiệp hiện có.', business_primary_required:'Hãy chọn công ty chính trong màn hình Công ty trước khi tiếp tục.', business_already_bound:'Tài khoản đã gắn với một công ty chính và không thể tự chuyển pháp nhân.', business_email_required:'Nhà tuyển dụng cần email tên miền doanh nghiệp, không dùng email cá nhân.', business_domain_unverified:'Hãy xác minh email và kiểm tra website HTTPS cùng tên miền doanh nghiệp.', business_verification_required:'Doanh nghiệp cần được admin xác minh trước khi công khai tin hoặc nhận hồ sơ mới.', business_conflict:'Hồ sơ đã thay đổi hoặc không còn ở trạng thái cho phép. Hãy tải lại trang.', business_legal_entity_locked: 'Tài khoản đã gắn với pháp nhân được duyệt; không thể tự đổi mã số thuế.',
  business_tax_exists:'Mã số thuế này đã được gắn với một tài khoản khác.', business_input_invalid:'Tên pháp nhân từ 3–200 ký tự; mã số thuế gồm 10 hoặc 13 chữ số.', invalid_history_filter:'Bộ lọc lịch sử không hợp lệ. Hãy mở lại Lịch sử phân tích từ thanh điều hướng.', invalid_report_filter:'Bộ lọc báo cáo không hợp lệ.', reported_job_unavailable:'Tin không còn tồn tại. Hãy tải lại chi tiết.', invalid_report:'Hãy ghi lý do báo cáo từ 3 đến 1.000 ký tự.'
  };
  if(messages[code])return messages[code];
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
  const response = await fetchResponse(path, { ...init, headers, credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(typeof problem?.title === 'string' ? problem.title : '', response.status);
  }
  return response.status === 204 ? undefined as T : response.json();
}
