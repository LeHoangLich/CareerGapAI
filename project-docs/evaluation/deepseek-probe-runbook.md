# P03/P06 — Probe full CV v3 từ localhost

> Cập nhật 21/10: [probe full CV thật đã hoàn tất](../report/stage-b-provider-2026-10-21.md) trong cap 0,05 USD, thuộc trần DeepSeek 2 USD toàn giai đoạn phát triển; còn 1,99 USD theo ledger bảo thủ. Key đã có, không hỏi lại hoặc tự cấp lại ngân sách. Plan public hiện không có probe mở; trạng thái “chưa được phép” phía dưới là lịch sử chuẩn bị 19/10, không thay xác nhận 21/10. Dùng ledger/đối chiếu ngoài app trước lần gọi tiếp theo.

Cập nhật **19/10/2026**. Chỉ chuẩn bị; **chưa được phép gọi trả phí**. Chủ dự án cung cấp DeepSeek sau. Chưa xác nhận key/số dư/kỳ/cap; không đọc secrets, tạo key hoặc nạp tiền. Mặc định tiếp tục Fake.

## Kiểm tra offline

```powershell
# Root, không .env/DB/key, không gửi mạng:
./scripts/check-deepseek-readiness.ps1
./tests/evaluation/test-provider-readiness.ps1
```

[Plan v2](deepseek-probe-plan.json) dùng `contract: cv-fields-v3`, output revision **schema 3**, không còn lấy probe skills v1 làm bằng chứng full CV. Model/giá/thời điểm kiểm tra hiện null, permission false; preflight phải exit 1. `readyForManualProbe` chỉ kiểm tra khai báo, không xác minh key/quyền/số dư hoặc cho phép chi tiền.

Trước call kiểm tra lại [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/), [JSON Output](https://api-docs.deepseek.com/guides/json_mode/), [Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode/). Không dùng snapshot 16/10 làm giá/model hiện hành. Chưa kiểm tra lại nguồn/provider thật trong lượt local 19/10.

## Đầu vào cung cấp sau

- Key riêng ở backend; chỉ báo trạng thái đã đặt, không gửi vào chat, plan hoặc `VITE_*`.
- Số dư dùng được/hạn credit; kỳ ngân sách dự án, tổng tiền còn lại sau các dịch vụ/lời gọi ngoài ứng dụng và phần DeepSeek.
- Cho phép trả phí với **tổng cap probe gồm retry**; quyền gửi đúng tài liệu synthetic đã chọn. Không tự chuyển toàn corpus/CV thật.

## Token và ngân sách

Full CV lặp quote/source nhiều field. Phải đặt **`Ai__FullCvMaxOutputTokens` riêng** (guard 128–2048), không ngầm dùng 512 của skills. Plan input 16000/output 2048 chỉ là đề xuất để kiểm tra; chưa chứng minh đủ cho CV dài. Nếu thiếu, đo trên tuning rồi điều chỉnh có kiểm thử/dự trù mới; không cắt dữ kiện/quote để làm pass.

Worker tối đa **hai attempts**, plan cũng phải hai. Dự trù decimal giống backend: `per_attempt = ceil_8_decimals((input_limit × input_price + output_limit × output_price) / 1_000_000)`; `reservation = per_attempt × 2`.

Đặt backend probe cô lập bằng **min(cap probe, hạn mức DeepSeek trong kỳ, tiền dự án còn lại, số dư dùng được)**; preflight xuất `requiredIsolatedProbeBackendLimitUsd`. Không chỉ so một reservation với cap rồi để nhiều jobs vượt tổng cap. Guard backend hiện tối đa 3 USD/kỳ không có nghĩa còn sẵn 3 USD; toàn dự án khoảng 4 USD/tháng gồm mọi môi trường/dịch vụ. Quota local không chặn call từ nơi khác.

## Trình tự sau khi đủ đầu vào và được phép

1. Điền plan private, khóa model/giá/thời điểm kiểm tra/kỳ/cap, không key. Chọn một CV synthetic ngắn với nhãn độc lập trước output, group/hash rõ. Manifest private phải có quyền gửi; `--for-provider` chỉ kiểm tra khai báo. Corpus đánh giá chất lượng thực tế cần quyền/nhãn riêng, không thay bằng synthetic.
2. Dùng PostgreSQL/storage mới cô lập, không job fake/real cũ; không bật DeepSeek trên DB ứng dụng. Migration bước riêng. Đặt provider/key/model/giá/`PricingVerifiedAt`/`PeriodStart/End`, `MonthlyLimit` bằng cap cô lập trên, input limit và **FullCvMaxOutputTokens**. Không deploy. Input admission tính bảo thủ UTF-8 bytes + framing, không gọi đó là tokenizer đo thật.
3. Candidate giả upload qua API; POST `/api/ai/jobs` với `{resumeVersionId, idempotencyKey, contract:"cv-fields-v3"}`. Giữ UUID idempotency khi theo dõi; không bấm key mới lúc chờ/timeout. Một job có thể hai HTTP calls, không hứa exactly-once.
4. Poll GET `/api/ai/jobs/{id}`. Worker yêu cầu `finish_reason=stop`, JSON/schema/quote/value hợp lệ trước success. `length` → `ai_output_truncated`, không retry cùng limit; reason khác/thiếu → `ai_completion_invalid`; trống có lỗi riêng. JSON còn hợp lệ nhưng `length` vẫn không tạo revision.
5. Chính chủ lấy **GET `/api/ai/jobs/{id}/evaluation-capture`** và lưu response private. Endpoint chỉ Candidate owner/version/file, job terminal mới có completion/parser provenance. Raw từ **job trước sửa**, không từ child confirmed. Export gồm source hash, method/config/prompt/parameters/parser, finish reason, tổng usage/cost biết được qua attempts, thời gian nhận→kết thúc. Queued/running hoặc job cũ thiếu metadata trả 409, không suy metadata. Sửa/xác nhận revision sau không đổi export gốc.
6. Job lỗi không có raw hợp lệ được giữ error/raw null, không JSON giả. Unknown usage/cost giữ null. Elapsed gồm queue/retry, không phải latency HTTP. Chuẩn bị nhãn/ghép capture và chạy theo [replay](cv-capture-evaluation.md). API thành công/probe nhỏ không chứng minh chất lượng full CV.
7. Đối soát attempts/reservations với usage/hóa đơn. Adapter tính bảo thủ theo giá cấu hình, không tự gọi là hóa đơn chính xác. Settlement một lần kể cả worker cũ; timeout/mất response giữ `outcome_unknown` và phân bổ. Chưa có công cụ đối soát unknown hoàn chỉnh: giữ khoản chưa rõ, dừng gửi mới; không sửa SQL hoặc giải phóng tùy tiện để trả quota.
8. Tắt `Ai__Enabled`, restart để ngừng nhận/dispatch mới; call đã gửi có thể tính phí. Dừng instance thử, trả provider Fake, giữ ledger/captures private. Ghi tổng chi/kết quả; không gọi thêm ngoài cap.

Adapter được kiểm tra bằng **HttpMessageHandler giả** về payload v3/token/usage/finish reason; worker full-contract retry/timeout/host replacement/truncation được test local. Đây chưa là DeepSeek thật. Không mua/nâng dịch vụ, search/email thật hoặc cloud trong quy trình này.
