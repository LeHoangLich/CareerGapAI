# Hiện trạng chuẩn — CareerGap AI

**Mới nhất — giai đoạn C ngày 22/10:** P10–P15 đã hiện thực `match_pair`, điểm/giải thích, quota tài khoản, hai chiều giới hạn ba cặp, cache kiểm quyền và phân tích tập đơn theo **JD/CV snapshot lúc nộp**. Có migration 16, thử nghiệm tuning synthetic với 15 calls DeepSeek ngày 21/10, evaluator so sánh offline và kiểm tra local tiếp ngày 22/10. Xem [biên bản C](report/stage-c-2026-10-22.md) cho kết quả cuối và [hợp đồng matching](phase-04-matching.md). Ngân sách toàn giai đoạn còn **1,98/2 USD** theo ledger bảo thủ; không phát sinh paid call ngày 22/10. M3 chất lượng đại diện vẫn chờ đầu vào; không deploy/commit/push/CI mới. Các đoạn sau ghi bằng chứng lịch sử theo ngày, không thay thế trạng thái C này.

**Mới nhất — M3 local tiếp 21/10:** [biên bản](report/m3-local-acceptance-2026-10-21.md). Đã có native DOCX/PDF, corpus 6 CV/4 nguồn + 8 JD/6 nhóm, 292 nhãn Codex; parser 6/6 file và 205/205 giá trị present khôi phục. JD parser **v3**, schema vẫn **2**, sửa theo tuning rồi khóa trước holdout. **94/94 backend, 9/9 E2E, 16/16 Python**, PDF 2/1 trang đạt. Không schema/migration mới (15); code factory/test/tài liệu chưa commit/push/CI. Không call/phí mới, C/deploy; đã dừng môi trường thử riêng. M3 tổng thể chưa đóng: thiếu native vi CV, người rà nhãn/quy tắc và quyền transfer corpus mới; không còn thiếu DOCX gốc.

**Bổ sung đầu vào M3 mới nhất 21/10:** đã tiếp nhận pilot có license upstream, **4 CV/3 nguồn + 8 JD/6 nhóm, 246 nhãn Codex dự thảo**, manifest/hash/split và adapter CV tuning/final. Dữ liệu private, không phí mới. Chưa có người rà nhãn, native vi CV, DOCX gốc hoặc độ phủ đại diện; provider transfer còn false. [Report đầu vào](report/m3-inputs-2026-10-21.md). M3 chưa đóng; ghi nhận chưa có corpus trong lượt kiểm tra trước là lịch sử.

**Lượt kiểm tra M3 mới nhất ngày 21/10:** chủ dự án yêu cầu thực hiện nghiệm thu chất lượng. Validator/replay mới khớp 122/0/0, nhưng chỉ có synthetic/3 nhóm và toàn nhãn present; không đủ kết luận chất lượng đại diện. M3 **chờ đầu vào dữ liệu**, không còn chỉ tạm hoãn theo yêu cầu cũ. [Biên bản M3](report/m3-quality-review-2026-10-21.md). Không thêm phí hoặc sửa runtime; chưa thực hiện C.

Ngày đối chiếu local: **21/10/2026**, Asia/Saigon; GitHub/CI vẫn là bằng chứng đã đối chiếu ngày 16/10. Đây là điểm đọc hiện trạng; nhật ký L01–L13 trong các báo cáo/task cũ giữ nguyên giá trị lịch sử theo từng lần chạy.

## Bản chuẩn và bằng chứng

- Baseline đối chiếu lịch sử ngày 16/10: **b392ef06f29ad20faacbd97f2bbc118de9de58e5**, GitHub/CI được kiểm tra khi đó. Ngày 21/10, `main`/HEAD và remote-tracking `origin/main` local cùng **cca4378ea6752e078de389804a4ee64fdae11f75**; không kiểm tra trực tiếp GitHub/CI của SHA mới trong lượt này.
- [CI 38836054191](https://github.com/LeHoangLich/CareerGapAI/actions/runs/38836054191): `completed/success`, đúng SHA trên; backend, frontend, docker đều success. Workflow này **không chạy Playwright E2E**.
- Các thay đổi A/B trước đây đã được chủ dự án commit trong cca4378; working tree sạch lúc bắt đầu 21/10. Thay đổi plan/tài liệu của lượt 21/10 chưa commit/push; không gán CI lịch sử cho chúng hoặc tự nhận CI của cca4378 đã đạt.
- Chi tiết kiểm tra local vừa chạy, checklist M1/M2 và đầu vào còn chờ: [báo cáo giai đoạn A](report/stage-a-2026-10-16.md). Kết quả cũ không thay thế lần chạy mới.
- Mới nhất sau **giai đoạn B/P04–P09**: **64/64** backend PostgreSQL, **3/3** frontend; **9/9** E2E regression và hai spec mới lặp ba lần **6/6 executions**; lint/typecheck/build đạt. PDF cuối đã kiểm tra text/geometry/render (2 trang/1 trang). Docker Linux parser child đạt. [Báo cáo đầy đủ B](report/stage-b-2026-10-16.md), [hợp đồng GĐ3](phase-03-extraction-contract.md). Giai đoạn A trước đó 53/3/7 và công cụ corpus/preflight 9/9 là bằng chứng lịch sử, không cộng vào lượt B.

**Bổ sung B ngày 19/10:** đã hoàn thiện capture/export chính chủ, evaluator raw offline và quy trình probe v3, chặn truncated completion, full output budget riêng; xem [report bổ sung](report/stage-b-followup-2026-10-19.md) và [checklist M3](phase-03-acceptance.md). DeepSeek/corpus chất lượng vẫn chờ; không triển khai C. Kết quả 19/10: **83/83 backend**, **3/3 frontend**, **9/9 E2E**; **16/16 Python**, **12/12 preflight self-test**, Docker/PDF/model check đạt. Sau hardening CLI chạy lại targeted 17/17; không cộng vào 83 hoặc cộng với 16/10.

**Chốt chức năng B ngày 21/10:** key backend đã xác thực; DeepSeek thật chạy **6/6 jobs, 6 attempts, không retry**, 122/122 fields trên 6 file synthetic/3 nguồn trước sửa. Xác nhận child/builder đạt cả sáu; một browser workflow real-output → PDF → apply → đúng snapshot đạt, không phát sinh call thêm. Cost usage bảo thủ **0,00655320 USD**, ghi nợ ngân sách **0,01/2 USD** cho DeepSeek toàn giai đoạn phát triển (còn **1,99 USD** theo ledger, không reset theo tháng/môi trường); cap riêng probe 0,05 USD. Regression mới **83/83 backend Fake**, 16/16 Python, 12/12 readiness, PDF đạt. [Report 21/10](report/stage-b-provider-2026-10-21.md). M3 chất lượng trên corpus đại diện vẫn hoãn; chưa thực hiện C.

## Phạm vi đã có

| Lát | Implementation hiện có | Giới hạn |
|---|---|---|
| L01–L08 | Nền tảng, tài khoản/profile, company/JD, CV private/version, ứng tuyển/snapshot, sàng lọc/note/block, CV public/derivative và retention | Local/Fake; backup/restore/expiry chạy tay; chưa chứng minh production/provider thật |
| L09 | Baseline từ khóa hai chiều, `retrieval_score` 0–1, evaluator offline | Không phải điểm phù hợp 0–100; chưa có quality corpus |
| L10–L11 | Revision skills CV v1 và yêu cầu JD theo version, owner rà soát/xác nhận | Lát gốc chỉ skills/tách dòng; B/L14 đã bổ sung full CV v3 và JD v2 |
| L12 + B/P09 | Diagnostic v2/projection on-demand, v1 confirmed + builder v2 + v3 confirmed đúng version/owner; ba alias có version | Self nhận user statements, public chỉ document spans; không score 100/ranking hay bảng projection DB mới |
| L13 + B/P08 | Builder `simple-v1` đã được chủ dự án chốt cho GĐ3; nạp v3 confirmed, phục hồi save dở sau reload, PDF → apply → snapshot đúng | Một mẫu một cột, chưa AI viết; session draft tạm theo user, không bảo đảm khi đóng/xóa phiên browser |
| L14/B | CV full schema v3, field-level quote/offset, sửa/xác nhận child; parser process con deadline/kill tree, thêm DOCX header/footer; JD v2 atomic/years/any_of | DeepSeek thật đã qua synthetic probe 21/10; chưa quality corpus/đánh giá đại diện, parser/JD còn giới hạn |
| C/P10–P15 | Pair job bền vững, rubric/score 0–100, matching hai chiều và application snapshot, user quota, cache/quyền, capture/evaluator | Synthetic tuning nhỏ, chưa thẩm định rubric/chất lượng đại diện; không bảng parent recommendation/projection |

Có **16 migrations** trong source sau C. Migration 16 được kiểm tra trên PostgreSQL 16.10 trống, nâng cấp từ 15 có dữ liệu và Down/Up trong DB test riêng; không tự áp dụng lên DB ứng dụng dùng chung/cloud. L12/L13/B không thêm migration.

M1/M2/M3 **chưa nghiệm thu tổng thể**. M1/M2 còn dữ liệu hợp lệ/trao đổi đánh giá và baseline trên corpus đại diện có split/nhãn; B đã có hợp đồng full extraction, template đã chọn, PDF ứng tuyển và đánh giá trước sửa synthetic. M3 còn corpus đại diện có quyền/nhãn để kiểm chứng chất lượng thực tế; provider thật đã qua probe synthetic 21/10. Không làm lại CRUD đã có vì các điều kiện này còn mở.

## Quyết định tiếp tục áp dụng

Chỉ phát triển localhost; deploy/hosting/DNS/cloud migration dừng từ 15/10. Không tự push. Dự án một người, tổng ngân sách khoảng 4 USD/tháng; Fake dùng `test_credit`, tách USD. Chưa có dữ liệu đại diện/nhãn thẩm định; DeepSeek full CV v3 đã qua probe thật trên synthetic, search/email thật chưa kiểm chứng.

- [Ma trận schema/hợp đồng hiện thực](schema-implementation-status.md).
- [Corpus và giao thức đánh giá](evaluation/stage-a-corpus-protocol.md).
- [Chuẩn bị probe provider](evaluation/deepseek-probe-runbook.md).
- [Kế hoạch tuần tự A–E](tasks/CareerGapAI-danh-gia-va-ke-hoach-2026-10-16.md). A/B đã thực hiện phần local được phép; chưa bắt đầu C. Probe 21/10 đã đóng; các call tiếp theo phải kiểm tra ledger và trần DeepSeek 2 USD đã được cho phép, không tự mở lại deploy.
