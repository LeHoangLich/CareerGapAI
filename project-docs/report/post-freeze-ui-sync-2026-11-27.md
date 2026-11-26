# Báo cáo Kiểm chứng Nhánh Mở rộng UI/Sync & Hồi quy (`post-freeze-ui-sync-2026-11-27`)

- **Ngày chốt:** 27/11/2026
- **Nhánh:** `main` (kế thừa mở rộng sau `release20261031`)
- **Phạm vi hoàn thành:** Toàn bộ **52/56** đầu việc trong [taskboard_v2.md](../../taskboard_v2.md), bao gồm toàn bộ các hạng mục **P0–P2** (`BE-01..14`, `FE-01..13`, `UI-01..08`, `SYNC-01..14`, `LIM-01`, `LIM-06`, `LIM-07`).
- **Phạm vi bảo toàn Defense Baseline:** Không chỉnh sửa hoặc ghi đè gói **Defense Baseline `release20261031`** (`41b82c6`, 222 file đóng băng, 19 migrations) và các hồ sơ lịch sử tại `project-docs/handoff/stage-e-20261031/`, `project-docs/evaluation/source-freeze-2026-11-01.json`.
- **Hạng mục P3 dài hạn giữ mở (`4/56`):** `LIM-02` (mở rộng dữ liệu đánh giá AI ngoài `pilot_2cv_4jd_limited`), `LIM-03` (tự động hóa lịch sao lưu/dọn file lỗi), `LIM-04` (OCR & độ phủ ẩn liên hệ), `LIM-05` (triển khai Cloud/Production).

## 1. Tóm tắt thay đổi theo đợt

1. **Đợt 1 (`UI-02..08`):**
   - Chuẩn hóa toàn bộ token ngữ nghĩa trong `frontend/src/style.css` và `frontend/src/community.css`, loại bỏ mã hex cứng ngoài `:root` và lớp query trùng lặp trên bảng Admin mobile.
   - Bổ sung `.card-grid-2`, `.card-grid-3`, `.chip-row`, `.table-scroll`, `.page-header`, `.alert-box`, `.pagination-bar`, `.sub-tabs`, thống nhất kích thước logo/avatar và responsive `320px` cho `.workspace-topbar`.
   - Chuẩn hóa `CommunityMarkdown` gom danh sách `- ` thành `<ul><li>` ngữ nghĩa và cố định viền `.social-actions .link-button`.
2. **Đợt 2 (`SYNC-01..06`):**
   - Bổ sung `frontend/src/statusDictionary.ts` làm từ điển nhãn trạng thái và tone tiếng Việt dùng chung.
   - Mở rộng `frontend/src/ui.tsx` với `PageHeader`, `Alert`, `EmptyState`, `StaticLoading`, `Pagination` và `ConfirmDialog` (hỗ trợ `requireReason`).
   - Áp dụng đồng bộ trên `publicHome.tsx`, `analysisHistory.tsx`, `businessIdentity.tsx`, `administration.tsx`, `recruitmentNotifications.tsx`, `publicResumes.tsx`, `reportModeration.tsx`, `communityReports.tsx`, và chuyển sidebar `#community` sang `.sub-tabs` ngang.
3. **Đợt 3 (`SYNC-07..14`):**
   - Tinh gọn thẻ tin `#jobs` (chuyển form nộp đơn vào `#job?id=...`), gọn hóa `#recruiter-jobs` (thu gọn form tạo tin và công cụ đối chiếu/ứng viên vào `<details>`), thay `window.confirm` bằng `ConfirmDialog`.
   - Gom 166 trường `FullCvReview` vào `<optgroup>`, thêm xóa chip cho `SkillPicker`, gỡ/ẩn chuỗi kỹ thuật (`BASELINE L09`, `retrieval_score`, `Localhost`, `pdfProbe` chỉ nạp trong `import.meta.env.DEV`) và sửa lỗi phòng vệ `null`/`Array.isArray` tại `locationInput.tsx`, `salary.ts`.
4. **Đợt 4 (`LIM-01`, `LIM-07`):**
   - Cập nhật `scripts/test-frontend-regression.py` để seed thêm tài khoản `Admin` (`DEMO_ADMIN_EMAIL`/`DEMO_ADMIN_PASSWORD`) phục vụ luồng xác minh doanh nghiệp `F28` trong E2E.
   - Bổ sung helper `verifyRecruiterBusiness` trong `frontend/e2e/helpers.ts`, cập nhật các spec bị ảnh hưởng và thêm `frontend/e2e/p1-p3-flows.spec.ts`.

## 2. Kết quả kiểm chứng hồi quy (`20261127-032340-8554`)

| Hạng mục | Kết quả | Chi tiết |
| --- | --- | --- |
| Backend Tests (PostgreSQL 16) | **240/240 đạt** | `CareerGap.Api.Tests.dll` (0 failed, 0 skipped) |
| Frontend Lint & Typecheck | **Đạt (exit 0)** | `eslint` & `tsc --noEmit` |
| Frontend Unit Tests (Vitest) | **47/47 đạt** | 12 test files |
| Frontend Production Build | **Đạt (exit 0)** | `vite build` (`pdfProbe` không nằm trong bundle production) |
| Playwright E2E | **26/26 đạt** | Bao gồm 22 luồng nền tảng + `p1-p3-flows.spec.ts` + luồng xác minh `BusinessIdentity` |
| Chi phí & Provider | **0 USD / Fake** | `providers: "Fake"`, `non_fake_methods: 0`, `usd_budgets: 0` |

## 3. Tài liệu liên quan

- Manifest kiểm chứng: [post-freeze-ui-sync-2026-11-27.json](../evaluation/post-freeze-ui-sync-2026-11-27.json)
- Bảng công việc: [taskboard_v2.md](../../taskboard_v2.md)
- Hồ sơ Defense Baseline (giữ nguyên): [m6-internal-acceptance-2026-11-01.md](m6-internal-acceptance-2026-11-01.md)
