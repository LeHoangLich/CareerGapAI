# Báo cáo tổng hợp: Diễn tập đánh giá mô phỏng CareerGap AI (Tác vụ 1.1 – 6)
*(Cập nhật hiệu chỉnh lần cuối ngày 30/10/2026 theo ma trận dự án)*

> **ĐÁNH GIÁ MÔ PHỎNG BẰNG AI**  
> Phiên diễn tập ngày 30/10/2026 được thực hiện trên môi trường demo localhost (`http://127.0.0.1:5190/`), cơ sở dữ liệu PostgreSQL synthetic độc lập (`careergap-handoff-demo-20261029`), provider mô phỏng `Fake`, **0 USD chi phí phát sinh** và **0 cuộc gọi ra ngoài**. Báo cáo này phục vụ kiểm tra kỹ thuật tự động trong phạm vi ca thử, **chưa tự nghiệm thu M6** và không thay thế nhận xét của giảng viên/người đánh giá thật.

---

## 1. Lịch sử đính chính và hiệu chỉnh kỹ thuật

1. **Phân biệt phạm vi diễn tập ngày 30/10 với toàn dự án:**
   - Phiên diễn tập ngày 30/10 sử dụng chế độ mô phỏng `Fake` để kiểm tra tích hợp luồng hệ thống cục bộ.
   - Về phía nhà cung cấp thật (real providers), dự án **đã có bằng chứng thực nghiệm lịch sử và đối soát độc lập đầy đủ**:
     - **DeepSeek (sau sửa ngày 27/10 — [stage-e-followup-2026-10-27.md](stage-e-followup-2026-10-27.md)):** 2/2 CV, 8/8 cặp xử lý thành công; F1 exact đường dẫn/giá trị đạt khoảng 0,623; trạng thái hệ thống khớp 34/44 nhãn Codex; nDCG@3 chiều CV→JD từ 1,000 xuống khoảng 0,917. Báo cáo ngày 27/10 đã ghi nhận rõ: Đây là **regression trên tập đã xem**, chưa chứng minh được chất lượng đại diện hay cải thiện baseline.
     - **Tavily (kiểm chứng thật & đối soát ngày 28/10 — [stage-e-external-evidence-2026-10-28.md](stage-e-external-evidence-2026-10-28.md)):** Đã hoàn tất đối soát dashboard tổng lúc 17:13 ngày 28/10 khớp 4/1000 credits (D2 + E2), gói Researcher Free, PAYG tắt. Vẫn còn giới hạn về chất lượng nguồn học thực tế.
     - Đồng bộ bàn giao ngày 29/10 ([handoff-sync-2026-10-29.md](handoff-sync-2026-10-29.md)).
   - Do đó, không xem dự án là “chưa có kiểm chứng provider thật” và không đề xuất chạy lại provider thật như điều kiện kỹ thuật mặc định.

2. **Thông tin người dùng thật:**
   - Dự án **đã có ghi nhận người dùng thật (chủ dự án / owner) dùng thử các luồng U1–U5 ngày 26/10 và bổ sung bằng chứng U1/U5 ngày 28/10** ([stage-e-external-evidence-2026-10-28.md](stage-e-external-evidence-2026-10-28.md)).
   - Phần còn thiếu hiện tại là: **biên bản / phản hồi của giảng viên hướng dẫn hoặc người đánh giá thực tế và nhận xét chấp nhận về phạm vi, chất lượng**; không phải hoàn toàn chưa có người dùng thật thao tác.

3. **Phạm vi đối soát dữ liệu:**
   - Được giới hạn chuẩn xác là: **"Đối soát các đối tượng trong phạm vi diễn tập"** (gồm CV `SIM-AI-U1-20261030`, đơn ứng tuyển `9376bc77-82e2-4913-ac42-90b346194196`, tin `DEMO-PHU` và tin synthetic quản trị `SIM-ADMIN-20261030`).

4. **Hành vi của tin tuyển dụng ở trạng thái `closed` (Đã đóng):**
   - Khi tin ở trạng thái `closed`, tin **không có trong danh sách tìm việc công khai** (`GET /api/jobs` chỉ lọc `status == "published"`).
   - Tin **không nhận hồ sơ mới** (`acceptingApplications` mang giá trị `false`; `POST /api/jobs/{id}/apply` từ chối nếu tin không ở trạng thái `published`).
   - Truy vấn trực tiếp bằng ID qua `GET /api/jobs/{id}` hiện trả về HTTP 200 kèm DTO mang `status: "closed"`.
   - **Đối chiếu đặc tả:** [01-system-specification.md](../01-system-specification.md) đề xuất các trạng thái tối thiểu (bản nháp, đang tuyển, đã đóng/hết hạn) và nêu tin nháp không xuất hiện trong tìm kiếm, nhưng **chưa quy định chi tiết về việc có cho phép xem chi tiết qua URL trực tiếp khi tin đã đóng hay không**. Do đó, kết luận chuẩn xác là: **code hiện cho xem chi tiết qua URL; cần xác nhận quy định sản phẩm**, không lấy code làm căn cứ duy nhất để tuyên bố đã chốt thiết kế nghiệp vụ.

5. **Phân biệt `resumeVersionId` và `fileId`:**
   - Các ID đã báo cáo ở các bước trước là `resumeVersionId` (khóa chính bảng `resume_versions`), liên kết tương ứng với các `fileId` (khóa chính bảng `stored_files`):
     - **Phiên bản 1:** `resumeVersionId`: `ca449162-bedb-4a40-a839-0c4b64a86c8b` · `fileId`: `eef6beb9-10c8-4df1-8601-bf0b2e239dc6`
     - **Phiên bản 2:** `resumeVersionId`: `f6b8a7d0-8a7b-4c27-bc92-93c909a2f4e9` · `fileId`: `bce6ae9f-4fa8-4c0b-b952-c0122e88fc10`
     - **Phiên bản 3:** `resumeVersionId`: `d807e670-2c58-4127-90ca-7a9f60842abd` · `fileId`: `09f1f55e-1a32-43c0-92c7-b5a45fcbbcb3`

6. **Phương pháp kiểm tra hash ở hậu kiểm:**
   - Hậu kiểm Tác vụ 6 đọc lại giá trị hash từ cột `sha256` trong bảng `stored_files` của cơ sở dữ liệu để kiểm tra tính nhất quán metadata; phân biệt với thao tác tải trực tiếp byte qua HTTP và tính lại SHA-256 từ byte thực tế đã thực hiện ở Tác vụ 2.2 và 3.2.

7. **Đính chính payload tìm nguồn học (Tác vụ 5):**
   - Mã nguồn frontend [learning.tsx](../../frontend/src/learning.tsx#L9), controller backend [LearningService.cs](../../backend/CareerGap.Api/Learning/LearningService.cs#L22) và log request thực tế ghi nhận đúng:
     `{ "pairJobId", "requirementKey", "topicKey", "language", "level", "idempotencyKey" }`.
   - Báo cáo 5 ghi `pairJobId`, `requirementKey` là chính xác. Báo cáo 6 trước đó có sự nhầm lẫn khi ghi `jobRequirementId`, `resumeFactId`.

8. **Ghi chú về nhật ký kiểm toán:**
   - Bản ghi `admin_audits` chứa lý do có cụm từ *"Ẩn tin vi phạm theo quy trình quản trị."* được lưu giữ nguyên vẹn; chú thích rõ đây là dữ liệu diễn tập synthetic, không phải kết luận vi phạm thực tế.

---

## 2. Bảng tổng hợp kết quả diễn tập (Tác vụ 1.1 – 6)

| Tác vụ | Mục tiêu | Thao tác thực sự đã làm | Bằng chứng và đường dẫn | Kết quả | Actor | Can thiệp ngoài UI |
|---|---|---|---|---|---|---|
| **1.1** | Tải lên CV synthetic, trích xuất cấu trúc qua Fake AI, kiểm tra tính riêng tư ban đầu. | Đăng nhập Candidate, tải file DOCX 1,219 bytes (`CV-synthetic-dung-thu.docx`), Fake trích xuất 21 trường, hiển thị giao diện rà soát Revision 1. | [Ảnh Review CV](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_1_cv_full_review.png)<br>DB: `resumes`, `resume_versions` (v1) | **Đạt trong phạm vi thử** | AI tự động | Không |
| **1.2** | Đối chiếu 21 trường với file nguồn, bổ sung 1 kỹ năng tự khai, xác nhận Revision 2, bảo toàn Revision 1. | Rà soát 21 trường khớp DOCX nguồn. Bổ sung kỹ năng `DEMO_SKILL_OWNER_20261030` (`user_statement`), bấm xác nhận tạo Revision 2 (22 trường), Revision 1 giữ nguyên. | [Ảnh trước sửa](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_2_before_edit.png)<br>[Ảnh sau xác nhận](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_2_after_confirm.png) | **Đạt trong phạm vi thử** | AI tự động | Không |
| **1.3** | Recruiter tạo JD, tách yêu cầu; Candidate tìm việc baseline và chạy phân tích cặp CV–JD. | Recruiter tạo và công khai tin `DEMO-PHU` (`715fefc1-...`); Candidate chọn CV v1 tìm việc -> baseline 0.235; bấm phân tích cặp -> pair match 25.00/100 (React: `partial`, Docker: `no_evidence`). | [Ảnh Recruiter JD](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_3_recruiter_job.png)<br>[Ảnh Baseline](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_3_baseline_results.png)<br>[Ảnh Pair Analysis](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task1_3_pair_analysis.png) | **Có giới hạn** | AI tự động | Không |
| **2.1** | Candidate nộp đơn ứng tuyển cho tin `DEMO-PHU`, kiểm tra snapshot. | Candidate chọn CV v1, bấm nộp hồ sơ. Đơn ứng tuyển `9376bc77-82e2-4913-ac42-90b346194196` được tạo ở trạng thái `submitted`, gắn snapshot hash `f7739309...`. | [Ảnh trước nộp](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task2_1_before_apply.png)<br>[Ảnh sau nộp](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task2_1_after_apply.png)<br>[Ảnh danh sách đơn](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task2_1_applications_view.png) | **Đạt trong phạm vi thử** | AI tự động | Không |
| **2.2** | Recruiter tải CV đã nộp để kiểm tra byte hash; chuyển trạng thái `reviewing`; lưu ghi chú nội bộ và kiểm tra quyền riêng tư. | Recruiter tải file qua HTTP, tính SHA-256 từ byte tải về: `f7739309...` (khớp 100%, không có kỹ năng tự khai trong binary DOCX). Đổi đơn sang `reviewing`. Lưu ghi chú nội bộ. Candidate truy cập endpoint ghi chú bị từ chối đọc (HTTP 403 Forbidden). | [Ảnh Recruiter sàng lọc](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task2_2_recruiter_screening.png)<br>[Ảnh Candidate view](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task2_2_candidate_view.png)<br>File tải về: `scratch/recruiter_downloaded_resume.docx` | **Đạt trong phạm vi thử** | AI tự động | Không |
| **3.1** | Dùng Revision 2 sinh đề xuất soạn thảo CV tiếng Việt qua Fake AI, xem trước PDF, lưu thành Phiên bản 2. | Mở soạn thảo, chọn tiếng Việt. Fake sinh 3 mục ràng buộc factKey. Xem trước PDF client-side (15,562 bytes, hash `6dd7fd59...`). Lưu thành Phiên bản 2 (`f6b8a7d0-...`). Phiên bản 1 và đơn cũ trong phạm vi kiểm tra không bị thay đổi. | [Ảnh Proposal](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_1_writing_proposal.png)<br>[Ảnh PDF Preview](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_1_pdf_preview.png)<br>[Ảnh Saved v2](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_1_saved_version.png) | **Có giới hạn** | AI tự động | Không |
| **3.2** | Soạn CV tiếng Anh từ Revision 2, xuất PDF, lưu thành Phiên bản 3. Tải thực tế và tính SHA-256 từ byte cả 3 phiên bản. | Chọn `en`, Fake sinh đề xuất có nhãn tiếng Anh. Xuất PDF client-side (14,709 bytes, hash `0c34c160...`). Lưu thành Phiên bản 3 (`d807e670-...`). Tải thực tế cả 3 file qua API và băm SHA-256: v1, v2, v3 đều khớp 100%. | [Ảnh Proposal EN](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_2_writing_proposal_en.png)<br>[Ảnh PDF Preview EN](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_2_pdf_preview_en.png)<br>[Ảnh Saved v3](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task3_2_saved_version_en.png) | **Có giới hạn** | AI tự động | Không |
| **4** | Kích hoạt AI-Off bằng cờ cấu hình (`Ai:Enabled = false`). Kiểm tra chức năng độc lập và khôi phục demo. | Chạy launcher chế độ `fault`. Đăng nhập Candidate giữ phiên do cookie lưu DB. Tải 3 phiên bản CV, xem đơn, sửa form thủ công và render PDF preview hoạt động bình thường. Yêu cầu AI mới trả về 503 `ai_disabled`. Khôi phục bằng lệnh `start`. | [Ảnh AI Disabled](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task4_ai_disabled_alert.png)<br>[Ảnh Sửa thủ công & PDF](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task4_manual_edit_pdf_preview.png)<br>[Ảnh Khôi phục](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task4_demo_recovered.png) | **Có giới hạn** | AI tự động | Không |
| **5** | Gợi ý nguồn học cho tiêu chí thiếu (Docker) trong chế độ Fake, kiểm tra payload quyền riêng tư. | Mở lại kết quả phân tích cũ. Tại tiêu chí Docker, chọn `vi`, `beginner`, bấm tìm nguồn học. Payload chỉ gửi ID tham chiếu, topic `docker`, ngôn ngữ, trình độ. Backend Fake trả về `no_results`. UI hiển thị thông báo mô phỏng local. | [Ảnh Learning Controls](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task5_learning_controls.png)<br>[Ảnh Learning Fake Result](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task5_learning_result_fake.png) | **Có giới hạn** | AI tự động | **CÓ** (Route interception sửa `idempotencyKey` thành key cũ để UI mở lại kết quả) |
| **6** | Recruiter tạo tin synthetic riêng; Candidate báo cáo; Admin xử lý báo cáo, ẩn tin, khôi phục về đóng và kiểm tra nhật ký. | Recruiter tạo & công khai tin `SIM-ADMIN-20261030`. Candidate gửi báo cáo. Admin đánh dấu `Đã xử lý` (trạng thái tin vẫn `published`). Admin `Ẩn tin` (tin biến mất khỏi danh sách công khai, API direct trả 404). Admin `Khôi phục thành tin đã đóng` (tin chuyển sang `closed`, không nhận hồ sơ; API direct trả 200). Kiểm tra tab `Nhật ký` ghi nhận đủ 3 thao tác. | [Ảnh Recruiter Tin](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_recruiter_job_published.png)<br>[Ảnh Báo cáo](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_candidate_report_submitted.png)<br>[Ảnh Xử lý báo cáo](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_admin_report_resolved.png)<br>[Ảnh Ẩn tin](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_admin_job_archived.png)<br>[Ảnh Tin ẩn](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_candidate_job_hidden.png)<br>[Ảnh Khôi phục đóng](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_admin_job_restored_closed.png)<br>[Ảnh Nhật ký](file:///C:/Users/lichl/.gemini/antigravity/brain/c1168324-6469-485c-b336-44176cffbe23/task6_admin_audit_logs.png) | **Đạt trong phạm vi thử** | AI tự động | Không |

---

## 3. Các hạn chế kỹ thuật và thực tế đã phát hiện trong ca thử

1. **Fake Matching không đánh giá ngữ nghĩa thực tế:**
   - Điểm đối chiếu (25.00/100) và các trạng thái tiêu chí (`partial`, `no_evidence`) được sinh bởi quy tắc mô phỏng trong backend dựa trên fixture mẫu. Kết quả này không phản ánh năng lực đọc hiểu ngữ cảnh hay phân tích ngữ nghĩa thực tế của LLM.

2. **Baseline có từ trùng ngoài kỹ năng trong ca thử:**
   - Trong ca thử Tác vụ 1.3, thuật toán tính trùng lặp baseline ghi nhận các token đơn âm tiếng Việt như `dung`, `hop`, `tong`, `ung`, `xay` nằm trong tập từ giao thoa. Đây là quan sát trên ca thử, chưa đủ bằng chứng kết luận bổ sung stop-words sẽ cải thiện ranking tổng thể; việc tinh chỉnh cần căn cứ vào thử nghiệm thực tế có đối chứng.

3. **Mẫu CV tiếng Việt/tiếng Anh còn pha ngôn ngữ:**
   - Bản soạn thảo tiếng Việt còn câu tiếng Anh; bản tiếng Anh chỉ dịch nhãn cấu trúc còn nội dung dữ kiện gốc vẫn giữ tiếng Việt. Chưa đủ căn cứ yêu cầu tích hợp ngay bước dịch thuật dữ kiện; đây là ý tưởng mở rộng nếu có nhu cầu đa ngữ hoàn chỉnh.

4. **Giao diện người dùng thiếu luồng mở lại kết quả phân tích cũ:**
   - Trong Tác vụ 5, component `PublicJobCard` / `PairAnalysisButton` khi tải lại trang không hỗ trợ nút xem lại lịch sử phân tích cũ; khi bấm nút phân tích, React luôn sinh một `idempotencyKey` ngẫu nhiên mới. Kịch bản tự động hóa phải can thiệp sửa request mạng để tái sử dụng idempotency key cũ. Luồng mở lại kết quả cũ qua UI độc lập chưa được kiểm chứng đầy đủ.

5. **Kịch bản AI-Off chưa kiểm chứng sự cố mạng ngoài:**
   - Tác vụ 4 chỉ kiểm tra việc chủ động tắt AI bằng cờ cấu hình môi trường (`Ai:Enabled = false`) tại tầng khởi động; chưa kiểm chứng các sự cố mạng thực tế khi gọi provider ngoài (timeout, lỗi 429/500/503 từ DeepSeek hoặc hỏng gói tin giữa chừng).

6. **Search Fake chưa chứng minh chất lượng nguồn học thật:**
   - Tìm kiếm nguồn học hoạt động ở chế độ `Fake`, trả về danh sách liên kết rỗng local. Thử nghiệm này chỉ xác nhận controller backend không rò rỉ dữ liệu nhạy cảm ra payload tìm kiếm, nhưng chưa kiểm chứng được độ liên quan, độ tin cậy và chất lượng tài liệu học tập thực tế từ Tavily.

7. **Chưa có phản hồi và nhận xét của người đánh giá thật:**
   - Chuỗi tác vụ ngày 30/10 do AI tự động thực hiện trên tập dữ liệu synthetic.
   - Về phía người dùng thật, chủ dự án (owner) đã dùng thử U1–U5 ngày 26/10 và bổ sung bằng chứng U1/U5 ngày 28/10. Tuy nhiên, **chưa có phản hồi, nhận xét và chấp nhận về phạm vi, chất lượng từ giảng viên hướng dẫn hoặc người đánh giá thực tế**.
   - **Số lượng tác vụ đạt trong diễn tập mô phỏng không suy ra tỷ lệ hoàn thành dự án hay điều kiện nghiệm thu M6**.

---

## 4. Phân loại kết luận và công việc

### A. Những luồng chức năng đã kiểm tra được
1. **Kiểm soát vòng đời và phiên bản CV:** Tải lên DOCX, bóc tách cấu trúc, gán nhãn nguồn gốc (`span` vs `user_statement`), quản lý nhiều revision và phiên bản độc lập (v1 DOCX, v2 PDF tiếng Việt, v3 PDF tiếng Anh) mà không làm biến đổi file gốc.
2. **Bảo toàn dữ liệu ứng tuyển:** Các thay đổi thực sự đã kiểm tra (tạo thêm phiên bản CV mới, tạo/ẩn/khôi phục tin khác) không làm biến đổi bản ghi đơn ứng tuyển và các liên kết snapshot hash đã gắn.
3. **Phân quyền dữ liệu:** Recruiter truy cập đơn và tải file thành công; Candidate bị từ chối đọc ghi chú nội bộ tại endpoint đã thử (HTTP 403 Forbidden).
4. **Kiến trúc phân tách AI:** Khi tắt cờ cấu hình AI, các chức năng quản lý hồ sơ, xem xét đơn, chỉnh sửa form thủ công và render PDF client-side vẫn vận hành bình thường.
5. **Quy trình báo cáo và quản trị:** Luồng gửi báo cáo từ người dùng, tiếp nhận & đánh dấu xử lý độc lập từ quản trị viên, ẩn tin vi phạm khỏi tìm kiếm công khai và khôi phục về trạng thái đóng an toàn kèm ghi vết kiểm toán đầy đủ.

### B. Các vấn đề cần xác minh hoặc xem xét cải tiến (ở mức đề xuất)
- **Luồng mở lại kết quả phân tích cũ trên UI:** Đề xuất xem xét bổ sung nút mở lại kết quả phân tích đã lưu trong `PairAnalysisButton` khi đã có bản ghi trong `match_results` để cải thiện trải nghiệm người dùng (mức ưu tiên: Trung bình).
- **Quy định hiển thị tin `closed` qua URL trực tiếp:** Code hiện cho xem chi tiết qua URL trực tiếp (`GET /api/jobs/{id}` trả về HTTP 200 kèm `acceptingApplications: false`); cần xác nhận quy định sản phẩm xem tin đóng có cần che giấu URL trực tiếp hay không (mức ưu tiên: Thấp).
- **Hiện tượng pha ngôn ngữ:** Đề xuất ý tưởng dịch thuật dữ kiện khi xuất CV đa ngữ; chưa đủ căn cứ thay đổi phạm vi hay tích hợp ngay (mức ưu tiên: Thấp).
- **Từ trùng trong Baseline:** Ghi nhận hiện tượng từ trùng đơn âm; chưa đủ bằng chứng kết luận thêm stop-words sẽ cải thiện ranking (mức ưu tiên: Thấp).

---

## 5. Tình trạng demo và hướng dẫn dừng

Hệ thống demo hiện tại đang được duy trì phục vụ dùng thử:
- **Địa chỉ giao diện Web:** `http://127.0.0.1:5190/`
- **Địa chỉ API Health check:** `http://127.0.0.1:5190/api/health` (HTTP 200 `{"status":"ok"}`)
- **Cơ sở dữ liệu:** Docker container `careergap-handoff-demo-20261029` (PostgreSQL 16, port `55449`)
- **Tiến trình Backend API:** `dotnet CareerGap.Api.dll` (PID `3008`, chế độ `Fake`)
- **Tiến trình nền giám sát:** Daemon task `c1168324-6469-485c-b336-44176cffbe23/task-579` (chạy script `scratch/keep_running.py`)

### Hướng dẫn dừng demo đúng phạm vi (khi hoàn tất toàn bộ quá trình dùng thử):
Khi muốn dừng demo, thực hiện các thao tác sau (không tự ý chạy nếu vẫn đang phục vụ dùng thử):
1. **Dừng tiến trình API thuộc demo:**
   ```powershell
   Set-Location D:\CareerGapAI
   python scripts/handoff-demo.py stop
   ```
   *Lưu ý:* Lệnh này **chỉ dừng tiến trình `dotnet CareerGap.Api.dll` thuộc demo, giữ nguyên container và cơ sở dữ liệu synthetic**.
2. **Dừng container Docker (nếu muốn tắt hoàn toàn tài nguyên database):**
   ```powershell
   docker stop careergap-handoff-demo-20261029
   ```
3. **Hủy tiến trình daemon giám sát nền trong hệ thống agent:**
   - Dùng công cụ `manage_task` với hành động `kill` trên task ID `c1168324-6469-485c-b336-44176cffbe23/task-579`.

---

## 6. Danh sách công việc còn lại để xem xét M6 theo ma trận hiện hành

Theo các biên bản ngày 27/10, 28/10, 29/10 và ma trận dự án, để xem xét nghiệm thu mốc M6 (Hoàn thành nghiệm thu và bàn giao), các công việc còn thiếu gồm:
1. **Phản hồi của người đánh giá thật:** Thu thập biên bản dùng thử và ý kiến nhận xét trực tiếp từ giảng viên hướng dẫn hoặc hội đồng chuyên môn.
2. **Nhận xét chấp nhận về phạm vi và chất lượng:** Người đánh giá xem xét và chấp nhận các chỉ số chất lượng hiện hành (kết quả sau sửa là regression trên tập đã xem, F1 ~0,623, rerank nDCG@3 ~0,917 chưa cải thiện baseline; bộ mẫu 2 CV Anh × 4 JD; 4 credits Tavily đối soát dashboard).
3. **Xử lý góp ý có ảnh hưởng (nếu có):** Rà soát các phản hồi từ người đánh giá, xử lý các nội dung kỹ thuật/nghiệp vụ có ảnh hưởng và thực hiện tái kiểm tra nếu có yêu cầu.

---
*Báo cáo hiệu chỉnh hoàn tất ngày 30/10/2026. Toàn bộ mã nguồn, dữ liệu gốc và các bộ bàn giao trước đó được giữ nguyên vẹn.*
