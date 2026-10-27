# Kiểm tra bước 4 và hoàn thành bước 5

Ngày chốt: 26/10/2026, Asia/Saigon. **Bước 4 được kiểm lại và đạt đủ 8 điều kiện trong phạm vi đã chốt. Bước 5 đã thực hiện, sửa lỗi quyền có bằng chứng và đạt kiểm thử liên quan.** Công việc kế tiếp là bước 6; chưa freeze phương pháp, nghiệm thu M5 hoặc mở E.

## 1. Kiểm tra toàn bộ bước 4 trước khi chuyển

Audit read-only độc lập kiểm 52/52 artifact, hash report/checklist, 12 source hashes, 55 frozen entries, 114 source hashes và DLL candidate. Bản nhãn v2 sửa đúng một importance, giữ giá trị/status/locator và lịch sử; validator đạt 7 tài liệu/164 nhãn/4 cặp, gate provider vẫn chặn đúng 7. Các số liệu parser, JD, writing, baseline và topic khớp raw/summary. 17 artifact workflow writing và bằng chứng search/cleanup tiếp tục khớp.

Tám điều kiện [bước 4](stage-d-step3-step4-2026-10-25.md) đủ để chuyển bước 5. Các kết quả deterministic, synthetic, owner review và provider thật lịch sử vẫn được phân biệt; không coi là chất lượng đại diện hoặc M5 đã đạt.

Trước khi cập nhật checklist, đã lưu bản guide/report/manifest bước 4 vào private `step5/entry-snapshot/`, giữ đúng hash đã audit. Manifest bước 4 không sửa; guide hiện hành có phiên bản mới phục vụ bước 5. Đối chiếu lịch sử phải dùng snapshot tương ứng, không lấy hash guide cũ áp cho nội dung hiện tại.

## 2. Lỗi quyền đã tái hiện và sửa

**Phát hiện S5-01, mức cao:** recruiter tìm CV công khai có thể nhận tên/headline profile theo quyền chia sẻ cũ nếu ứng viên thu hồi chia sẻ trong lúc đọc file. Nếu Admin khóa ứng viên khi đọc, hồ sơ vẫn có thể xuất hiện trong kết quả. Hậu kiểm cũ mới đối chiếu public selection/version/hash, chưa lấy lại profile visibility và account status.

Đã thêm kiểm thử HTTP deterministic: tạm dừng đúng lượt đọc file, thực hiện thu hồi chia sẻ hoặc khóa tài khoản qua API, rồi cho request tiếp tục. **Cả hai case thất bại trước sửa**, đúng tại assertion dữ liệu bị trả trái quyền.

[KeywordMatchingService.cs](../../backend/CareerGap.Api/Matching/KeywordMatchingService.cs) nay lấy nhãn profile tại hậu kiểm sau I/O, cùng exact CV/version/hash/file ready, Candidate/Recruiter active và JD hiện hành còn hợp lệ. Tên không chia sẻ trả “Ứng viên”, headline không chia sẻ trả null; item hết quyền bị loại. Thời điểm công khai lấy theo selection hiện tại. Điểm và thứ tự baseline giữ nguyên trên cùng tập đủ quyền; số counts vẫn mô tả tập khảo sát ban đầu.

**Phát hiện S5-02, mức cao:** batch gợi ý có thể giữ tên profile mà baseline đã chọn, dù ứng viên thu hồi chia sẻ trong lúc admission chờ khóa DB. Kiểm quyền cặp cuối không tự lọc lại chuỗi label đã lấy trước đó.

Kiểm thử riêng chặn admission sau baseline, thu hồi chia sẻ qua HTTP rồi tiếp tục. Với fixture hợp lệ, **assertion tên bị trả trái quyền thất bại trước sửa**. [PairRecommendationService.cs](../../backend/CareerGap.Api/Matching/PairRecommendationService.cs) nay truy vấn lại tập nhãn public cùng quyền hiện hành sau toàn bộ admission/access checks; lọc item đã mất quyền và cập nhật label trước trả batch. Các nhánh jobs/applications, snapshot, quota và scoring không đổi.

Hai file kiểm thử: [MatchingTests.cs](../../tests/CareerGap.Api.Tests/MatchingTests.cs) và [PairRecommendationAccessTests.cs](../../tests/CareerGap.Api.Tests/PairRecommendationAccessTests.cs). Test dùng dữ liệu synthetic và barrier/interceptor, không dùng CV thật hoặc provider. Một lỗi setup JSON của test batch và một lỗi dọn dữ liệu shared fixture đã được sửa trong quá trình kiểm; giữ các lượt đỏ trung gian riêng, không nhận chúng là lỗi sản phẩm mới hoặc cộng vào tổng test cuối.

## 3. Đối chiếu ba luồng và cơ chế nền

| Phạm vi | Đối chiếu và kết luận |
|---|---|
| Candidate dùng CV private tìm JD | Owner/version, JD current published/chưa hết hạn, revision và baseline; hồi quy matching đạt |
| Recruiter tìm CV public | Tách profile sharing/public CV, hậu kiểm version/hash/account/visibility; hai đường vừa sửa có regression đạt |
| Recruiter xử lý application | Owner tin, submitted CV/JD snapshot, revision as-of SubmittedAt, không backfill, 90 ngày và quyền tài khoản; hồi quy application/pair đạt |
| Writing → accept → private version → apply | Owner/revision, decision bất biến, không tự lưu CV; builder/version/snapshot giữ riêng. Mã writing/builder/application không đổi, dùng đúng bằng chứng lịch sử đã kiểm |
| Job/lease/idempotency/quota | Admission/settlement khóa và reload, lease generation chặn worker cũ, unknown giữ dự trù, cache kiểm quyền. Mã lõi không đổi; pair/quota/revocation trong hồi quy liên quan đạt |
| Learning/email/admin | Fixed topic/URL policy, không PII, unknown không resend, sender/recipient/cap; admin không đọc private CV. Rà mã và test/bằng chứng lịch sử; không phát hiện lỗi chặn mới trong phạm vi rà |

Không thấy đường sửa này đổi trạng thái ứng tuyển, ghi đè CV đã nộp hoặc sửa snapshot. Các trigger bất biến và revision theo ngữ cảnh giữ nguyên. Không thay API DTO, parser, prompt, rubric, trọng số, adapter provider, schema, migration hoặc dependency.

## 4. Kiểm thử và phiên bản bằng chứng

Restore locked/offline và build dùng output riêng trong `.local/stage-d-20261023/step5/build-artifacts/`; nguồn NuGet rỗng, không thêm package. Tiến trình có allowlist môi trường; không source `.env` hoặc nạp key provider. Fixture ép LLM/search/email Fake.

PostgreSQL **16.10**, container tmpfs riêng `careergap-step5-20261026`, chỉ bind loopback 55441. Fixture tạo/drop database ngẫu nhiên `careergap_test_*`. Kết thúc còn **0 database test**, container tạm đã dừng/dọn; không reset hoặc thay đổi DB/container ứng dụng đang dùng.

| Kiểm tra | Kết quả |
|---|---|
| Reproduce baseline trước sửa | 2/2 thất bại đúng assertion quyền |
| Reproduce batch với fixture hợp lệ trước sửa | 1/1 thất bại đúng assertion label |
| Build bản cuối | Đạt |
| Hồi quy matching/pair/confirmed revision/application trên bản cuối | **28/28 đạt**, gồm cả 3 case mới |
| Review độc lập hai patch | Không có finding cần sửa thêm trong phạm vi patch |
| Whitespace/diff và kiểm hồ sơ bàn giao | Đạt |

Không cộng các lượt trung gian vào 28. Không chạy lại full backend, E2E, frontend, Docker app build hoặc CI vì phạm vi sửa là quyền trả dữ liệu backend và hồi quy liên quan đã phủ. **133 backend/9 E2E/3 frontend/8 readiness là bằng chứng D lịch sử**, không gán thành lượt chạy mới hoặc toàn bộ suite trên DLL mới.

Ở đầu vào, 114 source hashes khớp candidate cũ; sau sửa có **112 file giữ nguyên, 2 file runtime thay đổi có chủ đích**. Manifest bước 5 ghim đầy đủ hash source, test source, DLL mới và TRX. DLL kiểm thử ở output riêng; DLL candidate cũ và nguồn entry snapshot được giữ để truy vết. Provider evidence thuộc phiên bản lịch sử; các file adapter/contract provider vẫn không đổi, không nhận là vừa probe lại DLL mới.

Không thay tiến trình API/Vite ứng dụng đang chạy. Bản sửa được build/kiểm chứng trong môi trường test riêng; khi dùng bản mới ở phiên localhost kế tiếp cần build/khởi động API từ nguồn đã sửa. Không có tuyên bố hot reload hoặc phát hành đang phục vụ người dùng.

## 5. Danh sách giới hạn còn lại

Chủ trì các mục dưới đây là **chủ dự án**, Codex hỗ trợ thực hiện; không giả định có thêm thành viên. S5-01/S5-02 đã đóng bằng patch và regression. Không phát hiện lỗi quyền/snapshot hoặc thiếu chức năng cốt lõi còn chặn bước 5 trong phạm vi đã rà; các điều kiện nghiệm thu cuối được giữ rõ:

| Mục | Ảnh hưởng và mức xử lý | Thời điểm |
|---|---|---|
| Native CV Việt, coverage, trạng thái nhãn và quyền provider của corpus | Điều kiện đánh giá/M5 còn mở, không suy chất lượng thực từ synthetic/Codex | Chốt phạm vi ở bước 6 trước nghiệm thu M5 |
| Final độc lập theo method; JD final cũ có lịch sử exposure | Chưa final freeze; không dùng nhóm tuning thay final | Bước 6, trước P20/E |
| Trần phát triển 2 USD | Ledger/guard vận hành kiểm tổng; runtime chỉ kiểm period/DB, không biết mọi môi trường | Đối soát trước mỗi paid call/đổi DB hoặc kỳ; không cấp lại cap |
| Writing whole facts/simple-v1 | Không dịch/viết lại tự do; PDF nội dung Anh vẫn có heading Việt | Ghi đúng phạm vi ở bước 6; mở rộng khi có yêu cầu sau |
| Catalog 21 topic, 32/47 annotation pilot ngoài danh mục | Báo chưa hỗ trợ, không tạo URL giả; giới hạn coverage, không lỗi chặn luồng local | Ghi trong protocol bước 6; mở rộng sau khi có nhu cầu/đánh giá |
| Search/email allowance 0; email owner-only và unknown đầu | Không gọi thêm, không hoàn unknown; chưa chứng minh gửi rộng/domain riêng | Trước lần cấp allowance hoặc mở rộng phạm vi mới |
| Backup/retention thủ công, chưa offsite/scheduler/production | Phạm vi localhost; không cam kết vận hành production | Restore/README bàn giao ở E; deploy vẫn dừng |
| Working tree dirty, chưa CI/push bản mới | Có hash source/DLL/test local; chưa có kết quả CI cho bundle mới | Ghim bundle ở bước 6; chỉ commit/push khi được yêu cầu |

## 6. Chốt bước 5

Đã đối chiếu ba luồng, sửa hai phát hiện quyền có bằng chứng, kiểm thử bản sửa và ghi mức ảnh hưởng/người phụ trách/thời điểm cho các phần còn lại. Private `step5/step5-exit-criteria.json`, `step5-current-source-manifest.json`, `step5-artifact-manifest.json` và TRX/log ghi lineage.

**Bước 5 hoàn thành; tiếp theo là bước 6 — freeze phương pháp, giao thức, tập cuối và dự toán E.** Chưa thực hiện bước 6 trong lượt này; M5/final freeze/E vẫn mở. Không provider request/phí, không mở quyền corpus, không deploy, commit hoặc push.
