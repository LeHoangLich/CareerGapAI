# Chốt luồng chức năng B và kiểm chứng DeepSeek — 21/10/2026

**Đã chạy full CV v3 bằng DeepSeek thật và hoàn tất luồng xác nhận → builder → PDF → ứng tuyển → recruiter tải đúng snapshot trên localhost.** B/P04–P09 đã có bằng chứng chức năng local và provider thật trong phạm vi synthetic. **M3 chất lượng tổng thể tiếp tục hoãn**, do chưa có corpus đại diện có quyền/nhãn và đánh giá thực tế; không dùng kết quả synthetic để tự nghiệm thu. Chưa bắt đầu C.

Tiếp nối [B 16/10](stage-b-2026-10-16.md), [bổ sung 19/10](stage-b-followup-2026-10-19.md) và [checklist M3](../phase-03-acceptance.md). Ngày/giờ theo Asia/Saigon.

## 1. Quyền dùng dịch vụ và phạm vi ngân sách

- Chủ dự án đã đặt key backend vào `.env`; xác nhận số dư DeepSeek và **giới hạn 2 USD cho toàn bộ giai đoạn phát triển**. Đây là trần DeepSeek của giai đoạn, không cấp lại 2 USD cho mỗi tháng, DB, môi trường hoặc lần probe. Vẫn theo ngân sách toàn dự án khoảng 4 USD/tháng và đối chiếu các khoản ngoài ứng dụng.
- Tự giới hạn **0,05 USD cho lượt B này**, gồm cả retry, nằm trong trần trên. Chỉ gửi tài liệu synthetic của dự án được chọn trong manifest, không CV thật hoặc dữ liệu người dùng khác. Không nạp tiền/mua/nâng dịch vụ, không deploy.
- Key không được in, đưa vào frontend, tool arguments hoặc Git. Xác thực qua `GET /user/balance` chỉ đọc, không dùng request chat để thử key. Tài liệu endpoint: [DeepSeek balance API](https://api-docs.deepseek.com/zh-cn/api/get-user-balance/). Có đọc số dư trước/sau; file chứa số dư tài khoản giữ private, không đưa vào repo.
- [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/) được kiểm tra ngày 21/10: alias `deepseek-flash`, phiên bản được tài liệu liệt kê `DeepSeek-V4.1-Flash-1017`; dự trù peak/cache-miss **0,30 USD/triệu input và 1,20 USD/triệu output**. Đây là snapshot giá cấu hình, không hứa giá/model alias bất biến. [JSON Output](https://api-docs.deepseek.com/guides/json_mode/) và [Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode/) phù hợp payload JSON/non-thinking đã dùng.
- Limit input 16000/output full 2048, tối đa hai attempts/job. Reservation bảo thủ **0,01451520 USD/job**; backend đặt cap **0,05 USD** cho instance thử, không mở backend limit thành 2 USD. Preflight private đạt trước dispatch.

## 2. Bản nguồn và môi trường cô lập

- `main`/HEAD và remote-tracking `origin/main` local cùng **`cca4378ea6752e078de389804a4ee64fdae11f75`**, working tree sạch lúc bắt đầu. Chủ dự án đã commit các thay đổi trước; không coi chúng còn uncommitted sau b392ef0. Chưa kiểm tra trực tiếp GitHub/CI cho SHA mới, không gán CI lịch sử 38836054191 cho bản này.
- Không sửa runtime code, dependency hoặc schema trong lượt này. Chỉ cập nhật plan/tài liệu/trạng thái. Scripts điều khiển probe, synthetic corpus, captures, credentials thử và artifacts nằm `.local/stage-b-20261021/`, bị Git ignore; không commit CV hoặc secrets.
- PostgreSQL **16.10**, container mới `careergap-stageb-20261021`, bind **127.0.0.1:55436**, data tmpfs riêng. Áp riêng **15 migrations**. Không reset/migrate DB ứng dụng, không dùng cloud.
- API Testing/DeepSeek/Local 5080, Vite 5173 dùng source hiện tại. `.env` chỉ được nạp để dùng key; provider, giá, hạn mức và DB của probe được override trong process riêng, không sửa cấu hình `.env` của người dùng. Không bật real provider trên DB có jobs fake cũ.

## 3. Lời gọi thật, dữ liệu và kết quả trước sửa

Chạy qua **API → JobService → worker → DeepSeek → validator → revision v3**; không gọi chat API ngoài ledger/quota của ứng dụng. Mỗi file có UUID idempotency giữ ổn định. Poll/re-run driver chỉ đọc lại các job đã có, không tạo job/key mới. Có tổng **6 jobs/6 provider attempts**, tất cả succeeded, **không retry**.

| Tài liệu synthetic | Layout/ngôn ngữ | Nhóm nguồn | Field gold trước sửa | TP/FP/FN | Input/output tokens |
|---|---|---|---|---|---|
| full-cv-vi DOCX | Paths/paragraphs, vi | IT-001 | 21 | 21/0/0 | 868/723 |
| full-cv-vi PDF | Hai cột, vi | IT-001 | 21 | 21/0/0 | 482/786 |
| full-cv-en DOCX | Paths/paragraphs, en | IT-001 | 21 | 21/0/0 | 795/671 |
| full-cv-en PDF | Hai cột, en | IT-001 | 21 | 21/0/0 | 409/734 |
| natural-accountant-vi DOCX | Heading tự nhiên, vi | Accounting-002 | 19 | 19/0/0 | 1312/808 |
| natural-engineer-en DOCX | Heading tự nhiên, en | Engineering-003 | 19 | 19/0/0 | 794/574 |

Tổng **122 TP, 0 FP, 0 FN**, source disagreements 0, không prediction chưa gán nhãn trong các output này. Parser phục hồi đủ value gold; full validator kiểm tra quote/segment/value trước lưu. Precision/recall/F1=1 chỉ trên bộ smoke nhỏ này, **không phải 100% chất lượng AI thực tế**. Sáu file thuộc **ba nguồn synthetic**, không sáu ứng viên độc lập; bốn bản IT chung group. Hai mẫu heading tự nhiên được viết/gán gold trước call, không tinh chỉnh prompt trên kết quả rồi báo lại như tập độc lập.

Các nhãn được tạo từ dữ kiện synthetic, không phải giảng viên/nhóm đánh giá thẩm định. Chưa có final evaluation hoặc dữ liệu đại diện có quyền. Không suy độ phủ nhiều ngành từ ba hồ sơ giả. Các case no-text/broken/JSON/quota/restart tiếp tục có regression local, không cố gửi file lỗi ra provider để tốn tiền.

Corpus/labels đã validate quyền local/provider, ID/group/hash và synthetic flags. Captures lấy từ **GET `/api/ai/jobs/{id}/evaluation-capture`**, trước chỉnh sửa; assembler khóa cùng method/config/dataset/source hashes. Evaluator offline không phát sinh lời gọi mới. Artifacts chính:

- `.local/stage-b-20261021/corpus-expanded.json`, `cv-dataset-expanded.json`, `captures/`, `captures.json`, `mapping.json`.
- `.local/stage-b-20261021/evidence/deepseek-extraction-before-correction.json`: breakdown/metadata/hash/kết quả trước sửa, không in value/contact/raw trong report tổng hợp.
- Method snapshot có model alias, prompt/contract, parser, completion policy, giá/token limits; manifest có hash dataset/capture/raw/assembly và source revision. Phiên bản model bên trong alias được ghi theo tài liệu provider tại thời điểm chạy, không tự nhận là model ID trả về đã được pin bất biến.

## 4. Xác nhận và luồng PDF/snapshot với dữ liệu model thật

Trên cả sáu kết quả: xem revision schema 3, thêm một kỹ năng synthetic tự khai, xác nhận thành child. Kỹ năng bổ sung là `user_statement`, quote null; builder-content nhận được thông tin này. Export lại job vẫn giữ nguyên raw AI ban đầu; nhãn và đánh giá không dùng child đã sửa.

Một workflow trình duyệt riêng dùng **job DeepSeek đã hoàn tất**, chặn mọi POST tạo AI job mới:

1. Candidate đăng nhập vào tài khoản synthetic, đọc kết quả/revision v3 và nạp bản đã xác nhận vào `simple-v1`.
2. Preview, lưu PDF thành version private mới; các nhóm liên hệ/summary/skills/experience/project/education/certificate hiện đầy đủ.
3. Recruiter synthetic tạo/công khai JD; Candidate chọn PDF vừa tạo và ứng tuyển.
4. Candidate sửa summary, tạo thêm version PDF; Recruiter mở CV theo application nhận **đúng byte PDF đã nộp**, không phải version mới. Recruiter truy cập file ID trực tiếp nhận 404.
5. Raw job export vẫn không đổi, không thêm AI job/provider call. `browser-provider-proof.json` ghi các assertion, hash và `additionalPaidCalls=0`.

PDF trước/sau đều **một trang**, đã kiểm tra text/Unicode/geometry/footer bằng pdfplumber, render Poppler và xem cả hai trang: dấu Việt rõ, không chồng/cắt chữ, counter 1/1 nằm trong trang. PDF/PNG/browser screenshot là evidence synthetic private, không file người dùng thật. Không sửa PDF renderer hoặc thêm template.

## 5. Usage, settlement và ngân sách còn lại

- **4660 input + 4296 output tokens**, 6 attempts đã settlement, 0 unreconciled, **0 USD reservation còn treo**.
- Cost theo usage và giá peak/cache-miss cấu hình: **0,00655320 USD**, dưới cap probe 0,05 USD. Adapter tính tất cả input ở cache-miss; không gọi đây là hóa đơn chính xác khi provider có cache/off-peak.
- Balance API chỉ trả **hai chữ số thập phân**; mức giảm hiển thị là 0,01 USD, không đủ độ phân giải để đối chiếu hóa đơn tới 8 chữ số. Không suy đây là phí chính xác hoặc lỗi tính tiền. **Ghi nợ ngân sách phát triển 0,01 USD** bằng max(cost bảo thủ, giảm hiển thị), còn **1,99 USD** trong trần DeepSeek 2 USD theo ledger theo dõi của lượt này.
- Ledger private `.local/ai-budget/development-phase.json` ghi scope toàn giai đoạn/all environments, allocation/settlement của probe, không tự reset theo tháng/DB. Đây là sổ theo dõi local, không kiểm soát lời gọi từ công cụ khác; các lần sau phải đọc ledger và đối chiếu khoản ngoài ứng dụng, không tự cấp lại 2 USD nếu thiếu ledger.
- `reconciliation.json`, `budget-final.json`, `budget-summary.json`, `balance-check.json`, `balance-after.json` giữ private. Không đưa số dư chi tiết tài khoản vào repo. Không có outcome_unknown trong probe này; không giải phóng khoản chưa rõ bằng SQL.

## 6. Kiểm tra chạy mới và giới hạn kết luận

| Kiểm tra ngày 21/10 | Kết quả |
|---|---|
| Full backend regression, DB PostgreSQL test riêng, **Fake và key process rỗng** | **83/83**, 0 fail/skip, khoảng 71 giây; `stage-b-provider-regression.trx`. Test suite không dùng key thật/gọi trả phí |
| Targeted completion + capture evaluator trước probe | **17/17**, 8 giây; `provider-preflight-local.trx`; không cộng vào 83 thành số test độc lập |
| Python corpus/capture tools | **16/16**, `python-tests.txt` |
| Offline readiness cases | **12/12**, `readiness-tests.txt` |
| Live full-CV integration | **6/6 jobs**, 6/6 attempts succeeded, không retry; captures/settlement có bằng chứng |
| Live-output browser workflow | **1 workflow riêng đạt**, tạo PDF/apply/snapshot đúng, không call AI thêm; không gọi đây là chạy lại toàn bộ 9 E2E specs |
| PDF text/geometry/render/visual | Hai PDF × một trang đạt; `pdf-geometry.json`, `provider-pdf-v1/v2-1.png` |
| Database | Áp riêng 15 migrations vào PostgreSQL mới; không migration/schema change |

Không chạy lại frontend unit/build, Docker image build hoặc toàn bộ chín E2E specs vì source runtime không đổi; kết quả ngày 19/10 giữ giá trị lịch sử, không gán là lượt 21/10. Không kiểm chứng production/S3/search/email, AI viết CV hoặc matching score 0–100 trong B.

## 7. Trạng thái chốt và vận hành sau probe

**B đã hoàn tất phần chức năng local và tích hợp DeepSeek full CV v3 theo phạm vi được phép.** Checklist nghiệm thu M3 vẫn mở ở chất lượng trên corpus đại diện có quyền/nhãn và góp ý đánh giá, như quyết định tạm hoãn M3. Không cần hỏi lại mẫu simple-v1, giờ dữ liệu, key đã có hoặc giới hạn DeepSeek 2 USD; không tự mở C khi chưa có yêu cầu thực hiện.

Đã ngừng API/Vite, vô hiệu hóa môi trường probe và đóng active plan để không vô tình gửi thêm. `.env` người dùng giữ nguyên, chỉ key backend, không tự bật real trên ứng dụng thường. Snapshot kế hoạch đã được phép được giữ private; plan public biểu thị **không có probe đang mở**, không phủ nhận quyền phát triển trong ngân sách còn lại đã chốt.

Dump DB synthetic của probe đã lưu private sau khi API dừng; container riêng được dừng sau regression. Có source manifest/hash, summary kiểm tra và cleanup. Không xóa DB ứng dụng/dịch vụ khác, không commit/push/deploy, không công bố raw CV hoặc key.

Rà soát tài liệu cuối: **10 tệp Markdown, 82 liên kết local**, code fences và `git diff --check` không lỗi. `execution-summary.json` đối chiếu TRX/live captures/browser proof; `source-manifest.json` ghi hash nguồn/config/docs và assembly. Các hash không thay cho bằng chứng kiểm thử; source runtime không đổi trong lượt này.
