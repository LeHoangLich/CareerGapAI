# Danh sách File / Thư mục Cần Rà Soát Dọn Dẹp — CareerGap AI

> **TRẠNG THÁI THỰC HIỆN (21/11/2026):** Theo phê duyệt của chủ dự án, đã untrack `frontend/%SystemDrive%` khỏi Git và **xóa toàn bộ các mục Nhóm A, Nhóm B và Nhóm C** (riêng `.local\frontend-step3-20261101-141707-55ce`, `output\design-guide-20261029`, cặp `scripts\test-frontend-regression.py` / `test_frontend_regression.py` và toàn bộ **Nhóm D** được giữ nguyên an toàn).

---

## Nhóm A — Rác hệ thống / Cache kiểm thử / Lỗi tạo thư mục ngoài ý muốn *(Đề xuất xóa, rủi ro rất thấp)*

| Đường dẫn tuyệt đối | Loại | Số file | Dung lượng | Trạng thái Git | Lý do & Đánh giá rủi ro |
|---|---|---:|---:|---|---|
| `D:\CareerGapAI\frontend\%SystemDrive%` | Thư mục | 4 | 966 KB | **Tracked** (`frontend/%SystemDrive%/ProgramData/Microsoft/Windows/Caches/*.db`) | Thư mục cache Windows bị tạo nhầm do lệnh chạy không mở rộng biến `%SystemDrive%` và bị commit nhầm vào Git. Nên chạy `git rm -r --cached frontend/%SystemDrive%` rồi xóa thư mục khỏi đĩa. |
| `D:\CareerGapAI\.playwright-cli` | Thư mục | 31 | 529,5 KB | Ignored | Log console, snapshot `.yml`, ảnh `.png` và 1 PDF mẫu từ các phiên chạy Playwright CLI cũ (22/10, 28/10, 01/11). Không dùng trong runtime/test tự động. |
| `D:\CareerGapAI\frontend\.playwright-cli` | Thư mục | 6 | 24 KB | Ignored | Log console và file tạm từ phiên Playwright CLI trong `frontend/`. |
| `D:\CareerGapAI\frontend\test-results` | Thư mục | 1 | 0 KB | Ignored | File trạng thái `.last-run.json` do Playwright test runner sinh ra mỗi lần chạy. |
| `D:\CareerGapAI\tests\CareerGap.Api.Tests\TestResults` | Thư mục | 2 | 490,5 KB | Ignored | File báo cáo `.trx` (`foundation.trx`, `milestones-l01-l09.trx`) sinh ra tự động khi chạy `dotnet test`. |
| `D:\CareerGapAI\scripts\__pycache__` | Thư mục | 13 | 174,4 KB | Ignored | Bytecode `.pyc` biên dịch tự động bởi Python khi chạy script. |
| `D:\CareerGapAI\tests\evaluation\__pycache__` | Thư mục | 4 | 90,4 KB | Ignored | Bytecode `.pyc` biên dịch tự động bởi Python khi chạy unit test evaluation. |
| `D:\CareerGapAI\clean-room` | Thư mục | 0 | 0 KB | Untracked / Rỗng | Chỉ chứa thư mục rỗng `clean-room\logs`, không có file mã nguồn hay log nào. |

---

## Nhóm B — Dữ liệu tạm & Build Cache dung lượng lớn *(Có thể tái tạo tự động khi build/test)*

| Đường dẫn tuyệt đối | Loại | Số file | Dung lượng | Trạng thái Git | Lý do & Đánh giá rủi ro |
|---|---|---:|---:|---|---|
| `D:\CareerGapAI\backend\CareerGap.Api\bin` | Thư mục | 356 | 1.046,04 MB | Ignored | Thư mục output build .NET (~1,05 GB). Chạy `dotnet build` hoặc `dotnet clean` sẽ tự tạo lại. |
| `D:\CareerGapAI\backend\CareerGap.Api\obj` | Thư mục | 57 | 10,20 MB | Ignored | Thư mục trung gian build .NET. Tự tạo lại khi `dotnet restore` / `dotnet build`. |
| `D:\CareerGapAI\tests\CareerGap.Api.Tests\bin` | Thư mục | 902 | 1.027,95 MB | Ignored | Thư mục output build test .NET (~1,03 GB). Tự tạo lại khi chạy `dotnet test`. |
| `D:\CareerGapAI\tests\CareerGap.Api.Tests\obj` | Thư mục | 39 | 3,72 MB | Ignored | Thư mục trung gian build test .NET. Tự tạo lại khi `dotnet restore` / `dotnet test`. |
| `D:\CareerGapAI\backend\CareerGap.Api\.local\l01-l09-validation` | Thư mục | 118 | 49,98 MB | Ignored | Dữ liệu kiểm thử tạm L01–L09 còn sót lại trong thư mục `.local` nội bộ của project API. |
| `D:\CareerGapAI\tests\CareerGap.Api.Tests\.local\l01-l09-validation` | Thư mục | 436 | 44,21 MB | Ignored | Dữ liệu kiểm thử tạm L01–L09 còn sót lại trong thư mục `.local` nội bộ của project Tests. |
| `D:\CareerGapAI\frontend\dist` | Thư mục | 34 | 3,73 MB | Ignored | Bản build production cũ của Vite (`npm run build`). Tự tạo lại mỗi lần build. |
| `D:\CareerGapAI\.local\frontend-step3-2026*` *(21 thư mục chạy test cũ)* | Thư mục con | — | ~3.112 MB (~3,1 GB) | Ignored | Kết quả tạm (`node_modules`/build/log) từ các lượt chạy `scripts/test-frontend-regression.py` ngày 29/10–01/11. **Lưu ý:** Giữ lại `frontend-step3-20261101-141707-55ce` nếu cần đối chiếu log nghiệm thu 01/11; 20 thư mục cũ hơn có thể dọn để giải phóng ~2,9 GB. |
| `D:\CareerGapAI\.local\dotnet` | Thư mục | — | 1.359,19 MB (~1,36 GB) | Ignored | Bộ nhớ đệm/cài đặt `.NET` cục bộ trong `.local\`. Chỉ xóa nếu máy đã dùng `.NET` cài toàn cục ổn định (kiểm tra trước bằng `dotnet --info`). |

---

## Nhóm C — File trùng lặp / Tài liệu tham khảo ngoài / Không dùng trong runtime *(Cần chủ dự án xem trước khi quyết định)*

| Đường dẫn tuyệt đối | Loại | Số file | Dung lượng | Trạng thái Git | Lý do & Đánh giá rủi ro |
|---|---|---:|---:|---|---|
| `D:\CareerGapAI\frontend\topcv-design` | Thư mục | 8 | 397,7 KB | Ignored | Bộ skill thiết kế `topcv-design` bị đặt nhầm vào `frontend/`, trùng hoàn toàn vai trò với `D:\CareerGapAI\.agents\skills\topcv-design`. Không được import trong `frontend\src\`, `index.html`, hay `e2e\`. *(Lưu ý: `source-freeze-2026-11-01.json` từng ghi lẫn 7 file ở đây nhưng đồng thời liệt kê loại trừ; giữ gói ZIP31 riêng là an toàn).* |
| `D:\CareerGapAI\frontend\logo_careergapai.jpg` | File | 1 | 110 KB | Ignored | Ảnh logo cũ nằm ở gốc `frontend/`. Đã kiểm tra toàn bộ `frontend\src\`, `index.html`, `e2e\` không có nơi nào tham chiếu (`index.html` đang dùng `/favicon.svg`). |
| `D:\CareerGapAI\design-ref` | Thư mục | 27 | 2,77 MB | Ignored | Tài liệu tham khảo UI/UX trích từ Glints, ITviec, TopCV dùng lúc lên ý tưởng giao diện. Không liên quan đến mã chạy hoặc kiểm thử của dự án. |
| `D:\CareerGapAI\.freebuff` | Thư mục | 2 | 0,1 KB | Ignored | File cấu hình `project-id` và `.gitignore` của công cụ CLI bên ngoài (`freebuff`), không thuộc hệ thống CareerGap AI. |
| `D:\CareerGapAI\output\playwright` | Thư mục | 49 | 2,91 MB | Ignored | Ảnh chụp và script check tạm thời từ các bước `multiview-step1`, `multiview-step2`, `release20261031` (báo cáo chính thức đã lưu bên `project-docs/report/` và `project-docs/handoff/`). |
| `D:\CareerGapAI\output\ui-ux-pro-max-20261101` | Thư mục | 13 | 325,1 KB | Ignored | Prototype HTML/CSS/JS minh họa cho đề xuất `project-docs/proposals/ui-unification-2026-11-01.md`. Chỉ xóa nếu bạn đã xem xong và không cần mở lại bản HTML mẫu. |
| `D:\CareerGapAI\output\hybrid-ux-review-20261101` | Thư mục | 11 | 134,7 KB | Ignored | Kết quả review UX trung gian ngày 01/11. |
| `D:\CareerGapAI\output\feature-comparison-20261102` | Thư mục | 2 | 29 KB | Ignored | Báo cáo so sánh tính năng ngày 02/11 (nguồn lập backlog `taskboard.md`). |
| `D:\CareerGapAI\output\mvc-feature-backlog-20261102` | Thư mục | 7 | 133,1 KB | Ignored | Tài liệu phân tích backlog P1–P3 ngày 02/11 (đã tổng hợp vào `taskboard.md`). |

### Lưu ý phân biệt — Hai mục dưới đây KHÔNG PHẢI rác trùng lặp:
1. **`D:\CareerGapAI\output\design-guide-20261029` (3 file `.png`, 294,6 KB — Git Tracked):** Đây là ảnh bằng chứng được theo dõi trong Git phục vụ báo cáo [frontend-style-guide-2026-10-29.md](file:///d:/CareerGapAI/project-docs/report/frontend-style-guide-2026-10-29.md). Không nên xóa trừ khi muốn bỏ hẳn bằng chứng lịch sử Style Guide.
2. **`D:\CareerGapAI\scripts\test-frontend-regression.py` (17,2 KB) và `D:\CareerGapAI\scripts\test_frontend_regression.py` (3,8 KB) (cả hai đều Git Tracked):** Đây **không phải** 2 bản trùng tên — file gạch ngang (`-`) là **regression runner chính**, còn file gạch dưới (`_`) là **bộ unit test của chính runner đó** (được gọi trực tiếp tại dòng 155 của `test-frontend-regression.py`). **Không xóa.**

---

## Nhóm D — Cảnh báo KHÔNG ĐƯỢC XÓA *(Quan trọng cho bảo vệ đồ án & vận hành local)*

- `D:\CareerGapAI\project-docs\handoff\stage-e-20261031\` *(cùng các gói `stage-e-20261027`, `stage-e-fixed-20261027`, `stage-e-20261029`, `stage-e-20261030`)* — Bộ bàn giao và gói nguồn đóng băng **Defense Baseline**.
- `D:\CareerGapAI\.local\ai-budget\`, `D:\CareerGapAI\.local\backups\`, `D:\CareerGapAI\.local\secrets\`, `D:\CareerGapAI\.local\handoff-sync-20261031\`, `D:\CareerGapAI\.local\m6-internal-acceptance-20261101\`, `D:\CareerGapAI\.local\m3-inputs-20261021\`, `D:\CareerGapAI\.local\stage-e-20261027\`, `D:\CareerGapAI\.local\stage-e-followup-20261027\` — Sổ cái ngân sách AI/provider thật, bản sao lưu cục bộ, bộ dữ liệu/biên nhận nghiệm thu và runtime demo `5190`.
- `D:\CareerGapAI\.env` — Biến môi trường cục bộ phục vụ kết nối PostgreSQL/local dev.
- `D:\CareerGapAI\.agents\` và `D:\CareerGapAI\docs\reviews\` — Bộ kỹ năng agent workspace và hồ sơ triage góp ý bảo vệ.

---

## Nhật ký dọn dẹp đã thực hiện (21/11/2026)
- Đã gỡ theo dõi Git (`git rm -r --cached`) và xóa `D:\CareerGapAI\frontend\%SystemDrive%` cùng toàn bộ Nhóm A.
- Đã xóa toàn bộ build cache/validation tạm và 20 bản chạy cũ trong Nhóm B (giữ `D:\CareerGapAI\.local\frontend-step3-20261101-141707-55ce`).
- Đã xóa toàn bộ thư mục tham chiếu ngoài/trùng lặp trong Nhóm C (giữ `D:\CareerGapAI\output\design-guide-20261029` và cặp script regression).
- Bảo toàn 100% các mục trong **Nhóm D**.
