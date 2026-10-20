# Giai đoạn A — Corpus và giao thức đánh giá v0.1

Ngày 16/10/2026. **Dự thảo, chưa được giảng viên duyệt.** Chuẩn bị trước L14/prompt; không có số liệu chất lượng trên dữ liệu đại diện.

## Kiểm kê hiện có và phần chờ

| Nguồn đã kiểm kê | Quyền/phạm vi | Số lượng và tình trạng |
|---|---|---|
| `matching-baseline-smoke.json` trong thư mục này | Nội dung tổng hợp của dự án, dùng smoke, không CV thật | 3 CV + 3 JD; 4 tiếng Việt, 2 tiếng Anh; JSON/text; 6 group riêng; 5 nhãn cặp kỹ thuật/9 cặp, 4 cặp chưa biết |
| `scripts/generate-fixtures.py` | Sinh synthetic nội bộ; không dữ liệu cá nhân thật | Đã sinh 2 DOCX vi/en có bảng, 2 PDF vi/en có hai cột và 1 PDF không text; chỉ kiểm tra parser/lỗi. Manifest private A có 5 file/2 group, hash file đã validate |
| Builder E2E | Synthetic sinh khi test | Kiểm tra round-trip/PDF/version; không nhãn extraction đại diện |
| CV/JD đại diện được phép dùng | **Chủ dự án xác nhận chưa có corpus được xác nhận quyền sử dụng** | 0 tài liệu được tiếp nhận cho quality corpus; trước mắt chỉ tự tạo synthetic cho phát triển/kiểm thử |

[Sổ kiểm kê smoke](corpus-register-2026-10-16.json) có ID/group/hash từng payload và hash file gốc; nhãn là synthetic, không phải nhận xét giảng viên. Hash payload theo JSON UTF-8 compact, **sort keys**, không ASCII-escape Unicode; nhãn quality chưa có. Fixtures sinh lại có thể khác byte do metadata thời gian: phải hash đúng file thực dùng khi đăng ký. Không dò `.env`, `.local/files` hoặc backup để tìm CV thật.

## Đăng ký và chia tập trước tinh chỉnh

Mẫu private: [manifest](templates/corpus-manifest.template.json), [nhãn trường](templates/field-labels.template.jsonl), [nhãn cặp](templates/pair-labels.template.jsonl). Sao chép vào `.local/evaluation/corpus/<version>/`; không commit bản điền thật, quote/contact hoặc giấy đồng ý. Mẫu JSONL là ví dụ synthetic để thay, không cộng vào số nhãn.

1. Tiếp nhận với `document_id` giả danh; ghi loại CV/JD, ngành, ngôn ngữ, định dạng/layout, SHA-256 file nguyên bản, nguồn và bằng chứng quyền ở kho private. URL công khai không tự cho quyền lưu/tái phân phối; không scrape CV. Quyền phải bao gồm gửi provider nếu định dùng LLM; chưa rõ thì quarantine, không dispatch.
2. Gán `group_id` chung cho cùng người/CV hoặc vị trí/JD gốc, mọi bản dịch, bản ẩn danh, format và version. So hash để tìm trùng; kiểm tra thủ công gần trùng. Giữ map nhận diện/consent private. Không công khai tên file nguồn có PII.
3. Gán group vào đúng một `demo`, `tuning` hoặc `final`; `smoke` riêng cho synthetic. Khóa split trước chỉnh prompt/rubric. Cặp chỉ thuộc split khi **cả hai group** cùng split; không nối CV tuning với JD final. Chọn một bản đại diện/group cho evaluator L09 hiện tại; các biến thể dùng extraction coverage và không chạy như query độc lập để tăng n.
4. Kiểm tra duplicate ID/hash, group xuyên split, quyền và label references trước export evaluator. Chụp manifest/nhãn bằng hash; mọi thay đổi split phải có version và lý do. Tập final không dùng để điều chỉnh prompt, alias hoặc trọng số.
5. Quyền rút lại: loại tài liệu/cặp khỏi lần chạy mới; ghi invalidation cho artifact cũ, dọn bản private theo phạm vi đồng ý. Artifact công khai chỉ chứa tổng hợp và ID giả danh đã xem xét quyền công bố.

## Quy mô và thời gian đề xuất

Chủ dự án xác nhận dành **28 giờ/tuần cho dữ liệu và gán nhãn**. Chưa có corpus hợp lệ nên đây là **hai phương án dự trù**, không phải cỡ mẫu/ngưỡng đạt đã duyệt. Đếm group gốc; bản dịch/format không tăng số người/tin độc lập.

| Phương án | Demo | Tuning | Final | Công gán nhãn ước tính |
|---|---|---|---|---|
| Pilot để kiểm chứng giao thức | 2 CV + 2 JD | 4 CV + 4 JD, đủ 16 cặp | Chưa chạm; dành group mới sau pilot | 4–7 giờ: 8 extraction labels, 16 pair labels, kiểm tra manifest/review |
| Đánh giá nhỏ nếu có đầu vào | 2 CV + 2 JD | 8 CV + 6 JD, đủ 48 cặp | 8 CV + 6 JD, đủ 48 cặp độc lập | Khoảng 17–29 giờ cho 28 tài liệu + 96 cặp + review; chưa gồm demo/thu thập/API/sửa lỗi |

Dự trù 15–25 phút/tài liệu, 4–8 phút/cặp và 20% tự rà soát. Đo thời gian thực của pilot rồi cập nhật; không ép vào số tuần. Mỗi split cần cả vi/en và PDF/DOCX trong CV nếu có quyền/dữ liệu, ghi coverage thiếu; ưu tiên CNTT. Tập nhỏ không chứng minh nhiều ngành. Pilot dùng để chỉnh giao thức sẽ thuộc tuning, không đổi nhãn thành final về sau.

Với 28 giờ/tuần, pilot dự kiến chiếm 4–7 giờ và phương án nhỏ 17–29 giờ gán nhãn; phần còn lại dành thu thập/quyền/sửa nhãn/góp ý. Không hẹn hoàn thành trong một tuần khi dữ liệu chưa có. Synthetic hiện giữ ở `smoke`, không đổi thành quality final; khi tự tạo thêm bản phục vụ demo/tuning vẫn đánh dấu `synthetic: true` và báo cáo riêng.

## Bổ sung B ngày 19/10/2026

Nhãn field v3 dùng `skills[0]`, không dùng path template cũ `skills[0].name`. Công cụ [capture/replay](cv-capture-evaluation.md) nhận manifest private, kiểm tra quyền/hash/group/split, tách pending/ambiguous/unreadable; chấm raw trước sửa bằng validator chung với worker. Quy tắc exact path/value v1 và hạn chế item ordering được ghi rõ; không gọi API hoặc tự coi output là quality evidence.

## Công cụ chạy ngay trên localhost

Python **3.10+**, chỉ thư viện chuẩn, không thêm dependency backend/npm. Chạy từ root:

```powershell
python scripts/prepare-synthetic-corpus.py
python scripts/validate-evaluation-corpus.py .local/evaluation/synthetic-corpus.json --verify-files --output .local/evaluation/corpus-check.json
python -m unittest discover -s tests/evaluation -v
```

Lệnh đầu tạo sổ metadata của smoke, không dò hoặc sao chép tài liệu thật. `--verify-files` kiểm tra hash đúng file/payload được chỉ đích danh; không bật thì chỉ kiểm tra metadata. Với bản manifest private đã điền và có quyền, dùng đường dẫn manifest tương ứng. Có thể ghi nhãn inline hoặc qua `field_labels_ref`/`pair_labels_ref` tới JSONL bên cạnh manifest; template placeholder phải được thay trước khi validate.

Validator chặn duplicate ID/hash khác group, group xuyên split, tuning/final chưa khóa, quyền thiếu, nhãn tham chiếu sai/hash cũ và cặp xuyên split. `--for-provider` **chỉ kiểm tra khai báo quyền gửi**, không gọi API và không cấp quyền chi tiền; smoke mặc định sẽ bị chặn. Output chỉ counts/error codes; bằng chứng quyền và chất lượng nhãn vẫn cần người thực hiện rà. Không tự kiểm chứng ngữ nghĩa quote trong PDF hoặc nhận diện mọi bản gần trùng. `reviewed` là khai báo, không phải chữ ký/giảng viên đã duyệt.

## Nhãn trích xuất và JD

- Tài liệu nguyên bản → nhãn độc lập → parser/LLM output **trước người dùng sửa**. Không lấy revision confirmed/builder làm ground truth cho AI extraction.
- Mỗi field: `field_path`, giá trị thật trong tài liệu, trạng thái `present`/`absent`/`unreadable`/`ambiguous`, locator trang/đoạn, quote chính xác và offset khi có. `user_statement` ở lớp riêng, không tạo quote file. Giá trị liên hệ giữ private; báo cáo chỉ tổng hợp.
- Đơn vị là từng item/field CV (contact, summary, skills, experience, projects, education, certifications). JD tách yêu cầu nguyên tử, `required`/`optional`/`unspecified`, số năm và lựa chọn A/B khi có nguồn rõ; không ép “A hoặc B” thành hai bắt buộc.
- Ghi riêng lỗi parser (mất/thứ tự/bảng/cột/Unicode/no-text) và lỗi LLM (bịa/thiếu/sai field/sai nguồn). Không tính field unreadable là negative. Giữ raw output và error riêng theo run, không ghi score 0 giả.

## Nhãn cặp và quy tắc đo

Mỗi nhãn tham chiếu hai group, document/version/hash cụ thể, người gán nhãn giả danh, phiên bản hướng dẫn, timestamp và tình trạng review. Relevance thử nghiệm: 0 không liên quan theo nội dung, 1 liên quan yếu, 2 một phần phù hợp, 3 phù hợp rõ; ghi lý do theo JD, bằng chứng CV, và yêu cầu bắt buộc chưa có bằng chứng. Không suy ra năng lực thực của người từ thiếu thông tin.

Một người gán nhãn rồi tự rà lại một lượt; ghi `self_reviewed`, không gọi là hai người đồng thuận. Chỗ mơ hồ giữ nhận xét/trạng thái chờ; giảng viên góp ý sau nếu có. Ghi riêng nhãn tạm/nhãn đã review; không coi 5 nhãn smoke là thẩm định. Ma trận thưa giữ `unknown`, không tự thêm relevance 0.

- Extraction: đếm TP/FP/FN theo field/item đã nhãn đọc được; precision/recall/F1, field accuracy và quote/source errors; báo denominator và unreadable/ambiguous. Chốt quy tắc normalization trên tuning trước final; không gộp Java/JavaScript hoặc SQL/PostgreSQL.
- Matching: baseline L09 và phương pháp mới cùng corpus/split/query. Đo recall chọn tập trước ranking hai chiều; precision/nDCG chỉ khi query có đủ nhãn. Evaluator hiện coi relevance > 0 là positive; ghi nguyên convention này khi so sánh, đổi phải version method/protocol. `k=20` hiện là mặc định code, chưa phải top-k được duyệt; pilot chọn thêm k nhỏ hơn theo pool và đóng băng trước final.
- Báo coverage theo vi/en, PDF/DOCX/layout và query; không cộng biến thể cùng group thành mẫu độc lập. Lưu metric null khi không đủ nhãn. Claim/evidence, required-no-evidence, độ ổn định, latency và chi phí được bổ sung khi consumer tương ứng đã có.
- Run manifest: corpus/split/label hashes, commit + dirty state/source archive, assembly, parser/schema/prompt/method/config/model, timestamp, cache, output/error IDs, token/cost/unit. Đóng băng trước final; output lưu private. L09 manifest đã có một phần thông tin, không giả định có quality evaluator đầy đủ.

## Việc chủ dự án/giảng viên còn cần cung cấp

Nguồn được phép dùng và quyền gửi provider; quy mô/coverage, relevance/rubric, top-k và lịch góp ý. Giờ dữ liệu đã chốt 28/tuần, không hỏi lại. Chưa liên hệ thay người dùng, chưa có phản hồi/duyệt. Có thể thực hiện L14 bằng synthetic trong khi chờ; điều này chưa đóng chất lượng M1/M2/M3.
