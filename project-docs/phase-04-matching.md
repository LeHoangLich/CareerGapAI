# GĐ4 — Hợp đồng matching thử nghiệm

Cập nhật 22/10/2026, thực hiện P10–P15 theo yêu cầu chủ dự án. [Biên bản và kết quả](report/stage-c-2026-10-22.md). Phạm vi localhost; M3 chất lượng trên corpus đại diện vẫn chờ đầu vào. Rubric chưa được giảng viên thẩm định.

## Đầu vào và phiên bản

- `pair-evidence-v1`, rubric `experimental-requirements-v1`. Chỉ dùng revision CV/JD đã được đúng owner xác nhận, cùng version tài liệu yêu cầu.
- CV v1: kỹ năng; v3: trường nghề nghiệp đã xác nhận; v2 builder: thông tin tự khai đã xác nhận. Loại trường liên hệ/tên khỏi projection; không gửi nguyên file/profile/note. Văn bản tự do vẫn có thể chứa thông tin người dùng nhập, nên đây không phải bộ ẩn danh ngữ nghĩa toàn diện.
- `self`: Candidate dùng CV riêng; JD phải là version hiện tại, đang công khai, chưa hết hạn.
- `public_pool`: recruiter owner JD chỉ dùng CV đang được chọn công khai. Bỏ `user_statement`; builder v2 chưa có revision dựa trên tài liệu sẽ báo thiếu dữ liệu phân tích, vẫn xem/tải theo quyền bình thường.
- `application`: quyết định chủ dự án **luôn dùng JD snapshot lúc nộp** cùng CV snapshot. Chọn revision đúng version đã tạo và xác nhận không muộn hơn `submitted_at`; ghim các ID đó vào input của lượt phân tích. Không sửa application hoặc backfill revision mới. Đơn cũ chưa có xác nhận phù hợp báo 409, giữ luồng sàng lọc thủ công. Đóng/sửa JD hiện tại không đổi kết quả theo snapshot.
- Giới hạn 180 fact CV, 30 requirement JD và token đầu vào; vượt giới hạn báo lỗi, không cắt âm thầm. Chưa dùng taxonomy/embedding hay bảng projection.

## Điểm và bằng chứng

LLM trả đúng một claim cho mỗi requirement: `met`, `partial`, `no_evidence`; tối đa bốn fact tham chiếu. Không nhận điểm hoặc giải thích tự do từ LLM. Backend kiểm schema, khóa tiêu chí, enum, quote nguyên văn bằng toàn bộ giá trị fact, nguồn fact và hỗ trợ từ khóa bảo thủ. Kiểm quote không chứng minh suy luận ngữ nghĩa đúng; các kết luận vẫn cần người dùng rà.

| Tiêu chí | Trọng số | Credit |
|---|---:|---|
| Bắt buộc | 2 | met = 1; partial = 0,5; no_evidence = 0 |
| Ưu tiên hoặc chưa phân loại | 1 | Như trên |
| Điều kiện sở thích nhận diện được: địa điểm, lương, work mode | 0 | `not_assessed`, không nằm trong mẫu số |

`score = round(100 × Σ(weight × credit) / Σ(weight), 2)`, tính bằng decimal ở backend. JD chỉ có điều kiện chưa đánh giá bị từ chối tạo điểm. Yêu cầu có năm kinh nghiệm hoặc `needsReview` không được đánh dấu `met`; cần người dùng đánh giá thêm. Yêu cầu bắt buộc chưa chứng minh đầy đủ hiển thị riêng dù điểm tổng cao. Không có ngưỡng AI tự quyết định sàng lọc.

Địa điểm/lương/hình thức của JD được hiển thị riêng; không suy ra mong muốn từ địa chỉ hay lịch sử CV. `no_evidence` là chưa thấy bằng chứng trong dữ liệu được phép dùng, không kết luận ứng viên không có năng lực. `not_assessed` không bị hiển thị thành thiếu năng lực. Lỗi provider/schema/quota không tạo điểm 0. Fake chỉ tạo `partial` khi có từ khóa trùng và luôn gắn nhãn mô phỏng.

## API và consumer

Các endpoint yêu cầu đăng nhập; POST bảo vệ CSRF và rate limit hiện hữu.

| Endpoint | Ý nghĩa |
|---|---|
| `POST /api/matching/pairs` | `{resumeVersionId, jobVersionId, idempotencyKey, context, applicationId?}`; nhận job bền vững |
| `GET /api/matching/pairs/{id}` | Người yêu cầu đọc trạng thái/điểm/tiêu chí khi quyền hiện tại còn hiệu lực |
| `GET /api/matching/pairs/{id}/evaluation-capture` | Cùng quyền, job terminal; input, raw output, method, token/cost để đánh giá offline |
| `POST /api/matching/recommendations` | `direction=jobs|public_resumes|applications`, CV hoặc job ID, `topK=1..3`, `page`, idempotency key |

Hai chiều gợi ý giữ nguyên L09 `weighted-keyword-overlap-v1`, chọn tối đa ba cặp rồi gọi cùng bộ chấm. `retrievalScore` 0–1 khác điểm sâu 0–100. Kết quả chưa chấm/thiếu dữ liệu không bị gán điểm 0. Chỉ các kết quả thành công được xếp theo điểm sâu trên giao diện.

Tập đơn riêng theo tin, phân trang 20, chọn tối đa ba hồ sơ đầu trang theo thứ tự nộp (`application-submission-order-v1-page20`), không tuyên bố đó là semantic retrieval. Các hồ sơ còn lại có nút phân tích từng hồ sơ và link đọc CV đã nộp. Điểm từng hồ sơ mở riêng không tự biến thành một lượt xếp hạng toàn kho. Không dùng ghi chú nội bộ, không thay đổi trạng thái đơn.

Không có parent recommendation job hoặc `recommendation_items` trong DB: request chọn tập trả danh sách; từng cặp là job bền vững có child idempotency key từ request/cặp/context. Retry cùng key của cặp trả input đã ghim. Tập chọn có thể thay đổi ở request sau nếu dữ liệu/quyền thay đổi; đây không phải bản lưu bất biến của toàn danh sách gợi ý. Không hứa phục hồi toàn bộ panel sau reload; pair job vẫn tồn tại để API tiếp tục đọc.

## Job, cache và ngân sách

- Migration 16 thêm `ai_jobs.job_type`, `match_inputs`, `match_results`. Extraction cũ mặc định `extract_cv`; matching dùng input riêng để recruiter không bị gán thành owner CV. FK ghép bảo vệ owner/version/revision; trigger giữ input/result/job type bất biến và ràng buộc snapshot application.
- Input chứa ID, payload và fingerprint; method gắn prompt/contract/rubric/model/parameters. Worker dùng lease/generation, timeout 70 giây, tối đa một retry. Kiểm quyền trước admission, trước dispatch, trước lưu và mỗi lần đọc. Consumer kiểm lại cả item chưa chấm trước khi trả danh sách.
- Cache theo requester, context/application, CV/JD version/revision, hash nội dung và method. Key mới trúng cache tạo job terminal riêng để idempotency không bị dùng lẫn; không tạo attempt/reservation. Ẩn/thay CV, đổi JD/đóng tin, yêu cầu xóa tài khoản hoặc hết hạn application chặn đọc cả endpoint pair, capture và generic job. Không thể thu hồi bản đã được người dùng tải/xem.
- `Ai__MatchMaxOutputTokens` bắt buộc 128–2048 với DeepSeek; mặc định chưa cấu hình nên không tự mở paid matching. `Ai__UserPeriodLimit` mặc định 0,25 USD trong kỳ, nằm trong `Ai__MonthlyLimit` chung; `Ai__UserMaxActiveJobs` mặc định 3 (clamp 1–10). Fake dùng 200 test_credit/user/kỳ, tách USD.
- Admission khóa transaction ngắn, tính usage đã biết + mọi reservation còn lại của tài khoản trong kỳ; dự trù hai attempts theo token cap. Không giữ transaction khi gọi provider. Unknown outcome giữ khoản dự trù; stale worker vẫn phải settle usage đúng một lần và không được phát hành kết quả.
- Hạn mức trong DB áp dụng instance/kỳ được cấu hình. **Trần 2 USD toàn giai đoạn phát triển qua mọi môi trường vẫn phải đối chiếu ledger và balance trước mỗi đợt trả phí**; quota local không kiểm soát gọi ngoài app. Không tạo kỳ/DB mới để cấp lại trần 2 USD. Tại 22/10 còn 1,98 USD được phép theo ledger bảo thủ; không phải cam kết số dư hóa đơn chính xác.
- Purge tài khoản và retention đơn xóa matching input/result/raw output liên quan, giữ attempt/usage cần đối soát. Đơn rút/bị loại hết quyền đúng 90 ngày; yêu cầu xóa tài khoản thu hồi sớm hơn.

## Kiểm chứng và giới hạn

[Report C](report/stage-c-2026-10-22.md) ghi các lượt chạy thực tế, migration mới/nâng cấp/Down, browser, lỗi đã sửa, chi phí và so sánh tuning. Không dùng kết quả synthetic/Fake để công nhận chất lượng thực tế đa ngành/Việt–Anh. Không mở GĐ5, deploy hay tự push trong giai đoạn này.

Chạy evaluator offline từ root với Python 3 (stdlib, không thêm dependency):

```powershell
python scripts/evaluate-pair-captures.py --dataset .local/stage-c-20261021/dataset.json --baseline .local/stage-c-20261021/evidence/baseline-top3.json --captures .local/stage-c-20261021/captures.json --criterion-gold .local/stage-c-20261021/criterion-gold.json --output .local/stage-c-20261021/evidence/pair-comparison.json
python -m unittest discover -s scripts -p test_pair_evaluator.py
```

Evaluator yêu cầu tuning split, hash dataset khớp baseline, một đại diện mỗi group, đủ nhãn cặp và capture đã chấm cùng method. Không mặc định cặp chưa gán nhãn là âm tính; không thay điểm lỗi bằng 0. Đánh giá rerank chỉ trong tập baseline đã chọn, giữ riêng recall chọn tập và NDCG/thứ tự. Chất lượng nhãn vẫn là trách nhiệm của protocol, không được nâng thành human-reviewed nhờ validator.
