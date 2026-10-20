# Hiện trạng chuẩn — CareerGap AI

Ngày đối chiếu local: **19/10/2026**, Asia/Saigon; GitHub/CI vẫn là bằng chứng đã đối chiếu ngày 16/10. Đây là điểm đọc hiện trạng; nhật ký L01–L13 trong các báo cáo/task cũ giữ nguyên giá trị lịch sử theo từng lần chạy.

## Bản chuẩn và bằng chứng

- Baseline mã nguồn: `main`, **b392ef06f29ad20faacbd97f2bbc118de9de58e5**; HEAD, `origin/main` local và GitHub main khớp khi kiểm tra trực tiếp GitHub API trong giai đoạn A.
- [CI 38836054191](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38836054191): `completed/success`, đúng SHA trên; backend, frontend, docker đều success. Workflow này **không chạy Playwright E2E**.
- Thay đổi giai đoạn A gồm tài liệu, công cụ dữ liệu/preflight, sửa profile UI và các bài test; nằm ở working tree sau baseline, chưa commit/push và chưa có CI riêng. Không gán CI trên cho các thay đổi mới.
- Chi tiết kiểm tra local vừa chạy, checklist M1/M2 và đầu vào còn chờ: [báo cáo giai đoạn A](report/stage-a-2026-10-16.md). Kết quả cũ không thay thế lần chạy mới.
- Mới nhất sau **giai đoạn B/P04–P09**: **64/64** backend PostgreSQL, **3/3** frontend; **9/9** E2E regression và hai spec mới lặp ba lần **6/6 executions**; lint/typecheck/build đạt. PDF cuối đã kiểm tra text/geometry/render (2 trang/1 trang). Docker Linux parser child đạt. [Báo cáo đầy đủ B](report/stage-b-2026-10-16.md), [hợp đồng GĐ3](phase-03-extraction-contract.md). Giai đoạn A trước đó 53/3/7 và công cụ corpus/preflight 9/9 là bằng chứng lịch sử, không cộng vào lượt B.

**Bổ sung B ngày 19/10:** đã hoàn thiện capture/export chính chủ, evaluator raw offline và quy trình probe v3, chặn truncated completion, full output budget riêng; xem [report bổ sung](report/stage-b-followup-2026-10-19.md) và [checklist M3](phase-03-acceptance.md). DeepSeek/corpus chất lượng vẫn chờ; không triển khai C. Kết quả 19/10: **83/83 backend**, **3/3 frontend**, **9/9 E2E**; **16/16 Python**, **12/12 preflight self-test**, Docker/PDF/model check đạt. Sau hardening CLI chạy lại targeted 17/17; không cộng vào 83 hoặc cộng với 16/10.

## Phạm vi đã có

| Lát | Implementation hiện có | Giới hạn |
|---|---|---|
| L01–L08 | Nền tảng, tài khoản/profile, company/JD, CV private/version, ứng tuyển/snapshot, sàng lọc/note/block, CV public/derivative và retention | Local/Fake; backup/restore/expiry chạy tay; chưa chứng minh production/provider thật |
| L09 | Baseline từ khóa hai chiều, `retrieval_score` 0–1, evaluator offline | Không phải điểm phù hợp 0–100; chưa có quality corpus |
| L10–L11 | Revision skills CV v1 và yêu cầu JD theo version, owner rà soát/xác nhận | Lát gốc chỉ skills/tách dòng; B/L14 đã bổ sung full CV v3 và JD v2 |
| L12 + B/P09 | Diagnostic v2/projection on-demand, v1 confirmed + builder v2 + v3 confirmed đúng version/owner; ba alias có version | Self nhận user statements, public chỉ document spans; không score 100/ranking hay bảng projection DB mới |
| L13 + B/P08 | Builder `simple-v1` đã được chủ dự án chốt cho GĐ3; nạp v3 confirmed, phục hồi save dở sau reload, PDF → apply → snapshot đúng | Một mẫu một cột, chưa AI viết; session draft tạm theo user, không bảo đảm khi đóng/xóa phiên browser |
| L14/B | CV full schema v3, field-level quote/offset, sửa/xác nhận child; parser process con deadline/kill tree, thêm DOCX header/footer; JD v2 atomic/years/any_of | Synthetic/Fake, chưa DeepSeek thật hay quality corpus; parser/logic JD có giới hạn được ghi trong report |

Có **15 migrations** trong source. Việc áp dụng mới nhất lên PostgreSQL trống được kiểm tra trong instance B cô lập ngày 19/10; không suy ra schema cloud hoặc DB ứng dụng hiện hành. L12/L13 không thêm migration.

M1/M2/M3 **chưa nghiệm thu tổng thể**. M1/M2 còn dữ liệu hợp lệ/trao đổi đánh giá và baseline trên corpus đại diện có split/nhãn; B đã có hợp đồng full extraction, template đã chọn, PDF ứng tuyển và đánh giá trước sửa synthetic. M3 còn provider thật/corpus có quyền/nhãn để kiểm chứng chất lượng thực tế. Không làm lại CRUD đã có vì các điều kiện này còn mở.

## Quyết định tiếp tục áp dụng

Chỉ phát triển localhost; deploy/hosting/DNS/cloud migration dừng từ 15/10. Không tự push. Dự án một người, tổng ngân sách khoảng 4 USD/tháng; Fake dùng `test_credit`, tách USD. Chưa có dữ liệu đại diện/nhãn đã thẩm định hoặc chứng minh DeepSeek/search/email thật.

- [Ma trận schema/hợp đồng hiện thực](schema-implementation-status.md).
- [Corpus và giao thức đánh giá](evaluation/stage-a-corpus-protocol.md).
- [Chuẩn bị probe provider](evaluation/deepseek-probe-runbook.md).
- [Kế hoạch tuần tự A–E](tasks/CareerGapAI-danh-gia-va-ke-hoach-2026-10-16.md). A/B đã thực hiện phần local được phép; chưa bắt đầu C. Không tự probe trả phí hoặc mở lại deploy.
