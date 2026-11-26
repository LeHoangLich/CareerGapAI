# Multi-view SPA — Bước 3, 29/10/2026

Bước 3 đã hoàn thành trong phạm vi hồi quy local Chromium/PostgreSQL, theo quyền owner và skill `design-taste-frontend`. Đã dùng thêm skill PDF để kiểm tra tài liệu tải từ ứng dụng. Không có lỗi sản phẩm mới cần patch trong lượt này; thay đổi nằm ở E2E, runner và tài liệu. Giữ code ứng dụng của Bước 1–2.

## Thay đổi

- Cập nhật đủ 9 bài E2E cũ trong 8 file để mở đúng màn hình đăng nhập, tab CV, profile, jobs, tài khoản, đơn và màn hình Recruiter. Kết quả matching được tìm tại cột phải. Helper `frontend/e2e/helpers.ts` điều hướng qua link hiện hành, giữ draft thay vì reload khi đổi view.
- Đối chiếu snapshot trước sửa xác nhận cả 133 dòng assertion `expect(...)` cũ vẫn còn nguyên trong các file tương ứng. Giữ kiểm tra version/revision, idempotency/recovery, profile, privacy, snapshot, status/history, block và file bytes; không bỏ assertion để thích nghi với lỗi.
- Thêm 5 bài: 2 bài workspace/navigation/state/responsive, 1 bài writing/matching qua worker Fake và ứng tuyển CV riêng, 2 bài UI mock cho loading/error/retry và storage bị chặn/keyboard skip-link. Tổng hiện tại 14 bài trong 11 spec.
- `frontend/playwright.config.ts` hỗ trợ output directory và JSON report qua biến môi trường. PDF/ảnh không còn ghi đè evidence lịch sử qua hai đường fallback cố định của foundation/stage-b; runner đặt thư mục riêng cho mỗi lượt.
- `scripts/test-frontend-regression.py` dựng PostgreSQL tmpfs có nhãn sở hữu, cổng API/Vite riêng, migration tường minh vào DB `careergap_test_ui_*`, tạo fixture/tài khoản synthetic và build artifacts riêng. Child environment dùng allowlist, không nạp `.env`; runtime loại `appsettings.Local.json`, ép provider Fake/key trống. Kết thúc dừng process và chỉ xóa container khớp nhãn của chính lượt chạy. Không xóa thư mục bằng chứng.
- Cập nhật README với lệnh chạy lại. Không thay dependency, migration, backend source, phương pháp AI, dữ liệu/bằng chứng E hoặc file Word của owner.

Snapshot test trước sửa: `.local/multiview-step3-snapshot-20261029-025547/`. Các thay đổi người dùng có trước lượt này được giữ nguyên.

## Lượt tổng hợp cuối

Manifest public: [frontend-multiview-step3-2026-10-29.json](../evaluation/frontend-multiview-step3-2026-10-29.json).

Lượt chính: `.local/frontend-step3-20261029-030649-530b/`, `completed=true`, `cleanup=true`. Runtime: Node 24.16.0, .NET SDK 10.0.401, PostgreSQL **16.15** từ image local `postgres:16-alpine`, Chromium trên Windows. Không gán kết quả này cho PG16.10, Linux DLL hoặc CI remote.

| Nhóm | Kết quả |
|---|---|
| Frontend lint/typecheck/build | Đạt; cảnh báo chunk React PDF lớn hơn 500 kB vẫn còn |
| Frontend unit | 11/11 đạt |
| Backend PostgreSQL | 159/159 đạt, 0 skipped; TRX tại `results/backend.trx` |
| E2E | 14/14 đạt, 0 skipped/flaky; 12 bài tích hợp API local và 2 bài mock-only |
| Writing/matching | 1 writing + 1 matching Fake đi qua worker; đúng payload/topK, kết quả ở cột phải; CV dùng nộp vẫn chưa công khai |
| Recovery/snapshot | Inject một response 503 ở bước lưu revision; reload hoàn tất đúng file đã upload, không nhân version; recruiter nhận đúng bytes PDF đã nộp sau khi CV hiện tại đổi |
| Responsive/state | 320/390/768/1024/1366/1440px, một view trong document, native file input cùng node và file vẫn còn, draft/profile/filter còn khi đổi view; Back/Forward/F5, onboarding, validation, reduced motion, keyboard skip-link |
| Diff/Python runner | `git diff --check` đạt; `python -m py_compile scripts/test-frontend-regression.py` đạt |

Backend test dependency, API build và DLL được serve có cùng SHA-256 `d5a6c62e3d1f32230b8a8e95c2b67a77ac381b363473126053656672cceefb84`; receipt `assembly-equivalence.json`. `source-sha256.json` ghim nguồn frontend/backend/test/runner của lượt chạy.

Đối soát UI DB cuối: 3 job `extract_cv`, 1 `suggest_cv`, 1 `match_pair`, tất cả `succeeded`; provider methods ngoài Fake **0**, USD budgets **0**. Budget duy nhất là Fake/test_credit, spent 5, reserved 0. Đây là đơn vị thử nghiệm, không phải USD hay benchmark chất lượng. Mutation nghiệp vụ thật chỉ nằm trong DB test. Không gọi provider thật hoặc đối soát lại ledger phát triển E.

## PDF và ảnh

PDF từ lượt tổng hợp cuối trong `evidence/`: builder dài 2 trang, builder ngắn 1 trang, probe 3 trang. `pdf-review/checks.json` xác nhận A4, có text từng trang, tên/dấu tiếng Việt đọc được, không replacement character hoặc text ra ngoài biên. Script `scripts/check-generated-cv-pdfs.py` kiểm footer hiển thị đúng 1/2, 2/2 và 1/1 cho hai PDF builder.

Đã render/xem cả ba trang builder của lượt cuối, và xem ba trang probe tương đương từ lượt E2E trước cùng frontend source. Đã xem ảnh workspace desktop/mobile từ lượt `030042-3c70`. Text/geometry probe cuối cũng đạt. Đây là kiểm tra file xuất thực, không dùng iframe headless làm bằng chứng layout PDF.

## Chạy lại

Tại root, với Docker đang chạy, image PostgreSQL local và dependency dự án đã cài. Runner tự chuẩn bị Chromium và thử mở browser; lần đầu tải browser cần Internet:

```powershell
python scripts/test-frontend-regression.py
```

Runner tự in đường dẫn kết quả mới, giữ log/source hashes/JSON/TRX/PDF/ảnh trong đó. Không cần truyền tài khoản demo hoặc sửa `.env`.

Chạy hẹp một nhóm khi cần tái hiện lỗi:

```powershell
python scripts/test-frontend-regression.py --skip-backend --skip-frontend --tests workspace.spec.ts
```

`--skip-backend` bỏ backend suite; vẫn build API phục vụ E2E. `--skip-frontend` bỏ lint/typecheck/unit/build frontend; vẫn chạy Vite và E2E. Runner kết thúc sẽ dừng server riêng, nên URL cổng ngẫu nhiên trong manifest không phải demo lâu dài.

## Lịch sử lượt chạy và giới hạn

- `025652-62db`: 3 bài tích hợp tài khoản/deletion/foundation đạt.
- `030042-3c70`: 14/14 E2E đạt, backend suite được chủ động bỏ qua.
- `030330-9d73`: backend 159/E2E 14 đạt nhưng runner kết thúc lỗi vì SQL audit mới dùng tên bảng `ai_methods` thay vì `ai_method_versions`; không coi đây là lượt tổng hợp thành công. Đã sửa đúng schema, giữ log cũ và chạy lại toàn bộ ở lượt chính.
- Không cộng các lần chạy lại vào số bài đạt. Snapshot/source và kết quả cũ không bị ghi đè.
- Firefox/WebKit không có binary trên máy nên chưa chạy. Zoom trình duyệt thực 200% chưa kiểm chứng; kiểm viewport không được gọi là kiểm zoom.
- Không tuyên bố kiểm chứng production, remote CI, SLA worker hoặc chất lượng AI đại diện. M6/feedback giảng viên vẫn theo hồ sơ hiện hành.
- Sau cleanup chỉ container demo persistent cũ còn chạy; trang demo `5183` và API jobs qua proxy đều trả HTTP 200 khi kiểm tra read-only. Không dùng DB/volume demo để chạy test, không push/deploy/commit.

## Bổ sung: sửa race khi khởi động PostgreSQL

Owner chạy lại tại `.local/frontend-step3-20261029-034306-c82b/`: tạo container thành công nhưng `postgres-version` exit 2 với lỗi Unix socket không tồn tại. Đây là lỗi readiness của runner: `pg_isready` dùng socket có thể thấy server tạm trong quá trình khởi tạo image; server tạm dừng trước khi server chính khởi động. Giữ nguyên kết quả lỗi và log cũ, không coi lượt này là test suite đã chạy.

`scripts/test-frontend-regression.py` hiện chờ truy vấn `SELECT current_database(), 1` qua TCP `127.0.0.1` vào đúng DB, xác thực bằng password trong environment. Chỉ chấp nhận exit 0 và đúng kết quả DB; retry tối đa 60 giây, giới hạn từng probe 5 giây. Lệnh đọc version cũng qua TCP. `postgres-ready.log` giữ từng lần thử; nếu lỗi runner lưu thêm Docker log trước cleanup.

- Thêm `scripts/test_frontend_regression.py`: 6/6 bài đạt, kiểm TCP/DB/password ngoài argv, retry kết nối, kết quả DB sai, lỗi SQL, timeout từng probe và deadline tổng. Runner tự chạy nhóm này trước khi tạo container.
- Khởi động mới 5 container PostgreSQL tmpfs độc lập: 5/5 truy vấn thành công sau 3 lần thử mỗi container; cả 5 quan sát hoàn tất init và cleanup đúng nhãn. Bằng chứng `.local/postgres-readiness-20261029-034603/result.json` và log từng container.
- Chạy lại toàn bộ với bản sửa tại `.local/frontend-step3-20261029-034613-f1c1/`: `completed=true`, `cleanup=true`; readiness 3 lần thử, frontend unit 11/11, backend 159/159, E2E 14/14, lint/typecheck/build đạt. `git diff --check` và Python compile đạt.
- API phục vụ và dependency backend test cùng SHA-256 `c25815d1bc920535df86f86c115b6a2a6c5ec2366498b054cd29738c3b3e9832`, ghim trong `assembly-equivalence.json`. Inventory nguồn so với lượt chính chỉ khác runner; không đổi app frontend/backend hoặc test nghiệp vụ.
- Audit DB mới: 5 job Fake succeeded, test_credit spent 5/reserved 0, non-Fake methods 0, USD budgets 0. Sau cleanup chỉ còn container demo persistent cũ. Không gọi provider thật hoặc dùng DB demo; không chạy lại đánh giá chất lượng AI/M6.

Lệnh chạy lại giữ nguyên: `python scripts/test-frontend-regression.py`.

## Bổ sung: chuẩn bị browser trước khi chạy hồi quy

Lượt owner `.local/frontend-step3-20261029-034958-3816/` vượt qua PostgreSQL và các frontend/backend checks nhưng cả 14 bài E2E dừng ngay khi mở browser: không tìm thấy `chromium_headless_shell-1248/chrome-headless-shell-win64/chrome-headless-shell.exe`. Không có assertion giao diện nào thực thi trong lượt lỗi này; `completed=false`, `cleanup=true`. File lỗi cũ được giữ nguyên. Trong phiên kiểm tra sau đó executable có mặt và mở được; không đủ bằng chứng kết luận vì sao nó không truy cập được ở thời điểm owner chạy.

Runner hiện dùng trực tiếp CLI Playwright đã ghim tại `frontend/node_modules/playwright/cli.js install chromium` để chuẩn bị đúng browser của dependency dự án. Sau đó thử mở/đóng Chromium headless bằng cùng environment với E2E, trước khi tạo Docker container hoặc chạy build/suite. Cache đúng phiên bản được dùng lại; lần tải đầu cần Internet. `PLAYWRIGHT_BROWSERS_PATH` được giữ trong allowlist khi owner cấu hình cache riêng. Không đổi phiên bản npm dependency, browser channel, assertion hoặc nghiệp vụ ứng dụng.

Log riêng `playwright-install.log` và `playwright-browser.log` phân biệt lỗi cài browser với lỗi bài test. [Tài liệu Playwright chính thức](https://playwright.dev/docs/browsers) mô tả yêu cầu browser theo phiên bản và lệnh cài đặt. Kiểm tra hiện hành dùng Playwright 1.64.1/Chromium headless shell 156.0.8078.4.

Lượt kiểm chứng đầy đủ sau sửa `.local/frontend-step3-20261029-035256-5f50/`: hai bước Playwright exit 0; runner tests 6/6, frontend unit 11/11, backend PostgreSQL 159/159, E2E 14/14 (không skipped/flaky), lint/typecheck/build đạt; `completed=true`, `cleanup=true`. Browser đã có sẵn trong cache ở lượt kiểm chứng nên không kiểm tải mới qua mạng. API phục vụ và backend dependency cùng SHA-256 `cfda439a31f8ebf7ea4eb7111169bd9a61d3bae0e8405ed978a1231474b80444`, receipt `assembly-equivalence.json`. Nguồn ứng dụng và test nghiệp vụ giữ nguyên; chỉ sửa runner và bổ sung test runner vào inventory.

Audit DB: 5 job Fake succeeded, non-Fake methods 0, USD budgets 0, test_credit spent 5/reserved 0; sau cleanup chỉ còn container demo persistent cũ. Không có provider thật hoặc thay đổi DB demo. Lệnh chạy lại vẫn là `python scripts/test-frontend-regression.py`.
