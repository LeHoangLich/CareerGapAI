# Bổ sung và chốt phần local giai đoạn B — 19/10/2026

**Đã hoàn thiện các phần local còn thiếu của B/P04–P09 được phép thực hiện.** DeepSeek do chủ dự án cung cấp sau, chưa gọi trả phí. Corpus đại diện có quyền/nhãn vẫn chưa được cung cấp; synthetic không phải bằng chứng chất lượng thực tế. **M3 chưa nghiệm thu tổng thể**, chưa triển khai C. Đây là phần tiếp nối [báo cáo B 16/10](stage-b-2026-10-16.md), không thay lịch sử bằng số đo mới.

## 1. Phạm vi, Git và môi trường

- Ngày báo cáo theo Asia/Saigon: 19/10/2026. Baseline HEAD vẫn `b392ef06f29ad20faacbd97f2bbc118de9de58e5`; giữ thay đổi A/B và file người dùng có sẵn, kể cả tài liệu `.doc` untracked. Không commit/push, không có CI mới chứng nhận working tree.
- Trước sửa lưu diff tại `.local/stage-b-20261019/before-followup.patch`. Thay đổi đang untracked từ lượt trước không được coi là đã commit trong patch; manifest nguồn cuối bổ sung hash file thực.
- Docker ban đầu tắt; đã mở Docker Desktop và tạo **container mới** `careergap-stageb-20261019`, PostgreSQL `16.10-bookworm`, bind `127.0.0.1:55435`, data tmpfs riêng, không volume ứng dụng. Integration tests tạo/drop DB `careergap_test_*` riêng. Áp 15 migration vào DB thử bằng bước riêng; không reset/migrate DB ứng dụng hoặc cloud.
- API Testing/Fake/Local 5080, Vite 5173, tài khoản/fixtures synthetic. Không đọc `.env` hay key thật; file env thử private ở `.local`, không Git. Không thêm dependency, nâng major, triển khai cloud hoặc thay snapshot/schema.

## 2. Những phần đã hoàn thiện thêm

| Hạng mục | Thay đổi và giá trị |
|---|---|
| P06: completion | Adapter giữ finish reason cùng usage. Worker yêu cầu stop; length dù JSON hợp lệ vẫn lỗi `ai_output_truncated`, không tạo revision và không tự retry cùng limit. Reason khác/thiếu có lỗi riêng; empty output khác empty fields hợp lệ. Known usage vẫn settle, unknown không tự giải phóng |
| P06: output budget | Full real CV phải cấu hình `Ai__FullCvMaxOutputTokens` riêng, guard 128–2048; không dùng ngầm 512 skills. Snapshot method mới có completion policy/parser version và config hash mới; dữ liệu/method cũ không bị backfill |
| P09: raw export | GET `/api/ai/jobs/{id}/evaluation-capture` chỉ Candidate chính chủ, kiểm tra version/file owner và ready. Export raw job trước sửa + source hash, method/config/prompt/params/parser, usage/cost biết được qua attempts, elapsed nhận→kết thúc. Child confirmed không đổi raw. Queued/running hoặc method cũ thiếu provenance trả 409; người dùng khác 404, recruiter 403 |
| P09: replay | `CvCaptureEvaluation` và CLI `--evaluate-cv-captures` chấm captures đã lưu bằng validator chung với worker. Không khởi tạo host/DB/HTTP/key. Metadata/quyền/hash/parser/split/ID được kiểm tra; nguồn nằm `.local`, bounded, không đi qua junction/symlink. Output mới, không ghi đè |
| P02/P09: corpus bridge | `prepare-cv-capture-dataset.py` nối manifest/nhãn hiện có với dataset replay, kiểm tra references private/bounded trước khi shared validator đọc. `assemble-cv-captures.py` ghép export được chỉ định, bind dataset/source hashes và cùng method/config; không quét CV hoặc gộp method âm thầm |
| P09: metrics | Exact path/value trước sửa, TP/FP/FN/P/R/F1 trên labels đọc được đã review; pending/ambiguous/unreadable/chưa nhãn không thành negative. Có language/format/field group/coverage/source disagreement. Invalid/missing/error có metrics null; valid empty output với gold present có recall/F1=0. Tổng hợp luôn kèm failed/scored counts |
| P03/P06: quy trình provider | Runbook full CV v3, plan v2 bỏ model/giá cũ khỏi cấu hình sẵn sàng; preflight vẫn blocked. Reservation decimal làm tròn từng attempt giống backend, luôn hai attempts; xuất cap backend probe cô lập bằng min của cap/limit/tiền còn lại/số dư. Không chỉ kiểm tra cap từng job |
| Hồ sơ M3 | [Checklist M3](../phase-03-acceptance.md), hợp đồng dữ kiện sang C, hướng dẫn nhãn/replay, README/current-status/AGENTS/roadmap/plan được cập nhật. Không tự đặt rubric/trọng số/ngưỡng hoặc coi giảng viên đã duyệt |

Code/tool chính: `Ai/LlmClients.cs`, `Ai/JobWorker.cs`, `Ai/JobService.cs`, `Matching/CvCaptureEvaluation.cs`, `Program.cs`, `scripts/prepare-cv-capture-*.py`, `scripts/assemble-cv-captures.py`, `scripts/evaluate-cv-captures.ps1`. [Hướng dẫn replay](../evaluation/cv-capture-evaluation.md), [runbook](../evaluation/deepseek-probe-runbook.md), [contract](../phase-03-extraction-contract.md).

## 3. Kiểm tra thật đã chạy

Evidence ở `.local/stage-b-20261019/evidence/`; lệnh root trừ frontend.

| Kiểm tra | Kết quả và artifact |
|---|---|
| `dotnet test tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj --no-restore --logger 'trx;LogFileName=stage-b-verified.trx' --results-directory .local/stage-b-20261019/evidence`, env test DB riêng/Fake | **83/83**, không fail/skip, khoảng 65 giây. Gồm 64 cũ và 19 case bổ sung: completion/mock adapter, replay metadata/rights/hash/error/missing/empty, full host replacement và truncated settlement. Full CV flow còn kiểm tra export sau sửa và owner/role |
| Sau hardening metadata/CLI, targeted CompletionTests + CvCaptureEvaluationTests | **17/17**, 5 giây, `offline-extraction-final.trx`; không cộng thành 100 test độc lập. CLI success/fault runs sau đó dùng build này |
| `python -m unittest discover -s tests/evaluation -v` | **16/16**, gồm 9 corpus cases + 7 capture-tool cases; `python-tests.txt`. Bổ sung chặn reference ngoài private trước khi đọc |
| `./tests/evaluation/test-provider-readiness.ps1` | **12/12** case offline; `provider-readiness-tests.txt`; dữ liệu ngân sách/giá trong self-test là giả để kiểm tra số học, không phải cho phép/model/giá thật |
| `./scripts/check-deepseek-readiness.ps1` | **Exit 1 có chủ đích**, `readyForManualProbe=false`, estimate/cap null, không provider; `provider-preflight-blocked.json` |
| Frontend `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` | Đạt, **3/3** unit. Sau thay message review chạy lint lại; Docker build cũng typecheck/build source cuối. Chunk PDF vẫn lazy, 1.199,13 kB/gzip 443,48 kB, cảnh báo kích thước còn |
| `npm run e2e -- --reporter=list,json` với env/demo/fixtures Fake và evidence directory mới | **9/9**, 0 fail/skip/flaky, **54,6 giây**, `e2e-final.json`. PDF/apply/snapshot và full review/reload đạt. Thông báo 503 synthetic_save_failure là fault test chủ đích |
| CV extraction smoke, replay normal và fault | 6 files mỗi run; chi tiết mục 4. Không provider/key/DB/call mới |
| `dotnet ef migrations has-pending-model-changes --no-build --project backend/CareerGap.Api`; kiểm tra DB thử | Không model diff, DB thử có **15 migrations**. Không thêm migration/DDL |
| `docker build -t careergap:stage-b-followup-20261019 .` | Đạt; image ID `sha256:6b7e004214ee49fe80e26d6c701bdb317dd1ada977a9945f1b38bdfbae184006`, `docker-image-id.txt`. Không publish/deploy |
| Docker `--probe-file` với fixture DOCX readonly, `--network none` | Exit 0, parser child Linux, 21 segments/667 ký tự, `docker-parser.txt` |
| PDF QA qua pdfplumber/Poppler và xem cả ba PNG | PDF dài **2 trang**, bản ngắn **1 trang**; chữ Việt/text/geometry/footer đạt, không cắt/chồng/mất dấu nhìn thấy. `pdf-geometry.json`, `long-reviewed-1/2.png`, `short-reviewed-1.png`. Không sửa template/PDF renderer lượt này |

Không chạy lại CI/GitHub hoặc baseline ranking trong lượt này vì không thay scorer/projection/ranking; bằng chứng baseline so sánh A/B ngày 16/10 giữ lịch sử, không gọi là đã chạy lại 19/10. Mock HTTP không chứng minh DeepSeek thật.

## 4. Replay và lỗi có chủ đích

Đã chạy root:

```powershell
./scripts/evaluate-cv-extraction.ps1
python scripts/prepare-cv-capture-smoke.py --output-directory .local/stage-b-20261019/replay-success
./scripts/evaluate-cv-captures.ps1 -Dataset .local/stage-b-20261019/replay-success/dataset.json -Captures .local/stage-b-20261019/replay-success/captures.json -Output .local/stage-b-20261019/evidence/replay-success.json
python scripts/prepare-cv-capture-smoke.py --output-directory .local/stage-b-20261019/replay-faults --with-faults
./scripts/evaluate-cv-captures.ps1 -Dataset .local/stage-b-20261019/replay-faults/dataset.json -Captures .local/stage-b-20261019/replay-faults/captures.json -Output .local/stage-b-20261019/evidence/replay-faults.json
```

| Run synthetic | Scored/6 | TP/FP/FN trên documents scored | Các trạng thái khác |
|---|---|---|---|
| Normal Fake captures | 4 | 84/0/0 | Hai PDF lỗi no-text/broken: metrics null, không tạo FN giả |
| Controlled faults | 2 | 41/0/1 | Một field bị test bỏ có FN=1; response length và JSON sai không scored; hai PDF lỗi không scored |

Bốn bản valid là **một CV gốc**, không bốn người. Tổng ba groups gồm một CV valid và hai nhóm file lỗi. Đếm theo document variants để kiểm tra coverage, không diễn giải là mẫu độc lập. Cả hai report `providerCalled=false`, `independentQualityAcceptance=false`; fault do test tiêm, không lỗi đo từ DeepSeek. Replay không tạo số đo latency/chi phí mới.

## 5. Lỗi trong lượt và giới hạn

- Build đầu gặp nullable warnings-as-errors ở replay guard; đã sửa luồng guard với `DoesNotReturnIf`, build/tests sau đạt.
- Full backend đầu **82/83**: khi mở test restart thành hai contract, assert đếm toàn DB fixture dùng chung nên thấy job của case trước. Đã scope assert theo đúng job ID; lượt full sau **83/83**. Artifact đầu giữ `stage-b-followup.trx`.
- E2E đầu **8/9**: file env demo do lượt này chuẩn bị sai cú pháp khiến foundation thiếu biến; đã tạo credentials synthetic mới/ghi từng dòng và chạy lại **9/9**. Không dùng secrets thật. Artifact `e2e-initial-env-failure.json` giữ riêng; không gán lỗi chuẩn bị này thành bug sản phẩm.
- Giá trị đúng quote chưa chứng minh đúng ngữ nghĩa field; exact path/value phạt item ordering, đã ghi protocol v1. Parser/JD phức tạp, đồng thời nhiều upload, sandbox OS/RAM hard limit, độ phủ ngành và corpus thật vẫn có giới hạn cũ.
- Known usage settlement đã test; unknown không bị xóa. Chưa có công cụ đối soát provider unknown hoàn chỉnh; runbook giữ khoản và dừng gửi, không hứa vận hành production.
- Chưa có DeepSeek/model/giá thật hoặc corpus được xác nhận quyền/nhãn đại diện. Không có góp ý/ngưỡng giảng viên được coi là đã duyệt. Một probe sau này chỉ chứng minh tích hợp, phải đánh giá trước sửa để nghiệm thu chất lượng.

## 6. Trạng thái kết thúc

Các phần chuẩn bị/coding/tooling local còn thiếu đã được bổ sung, có kiểm thử. Phần chờ thật sự: dịch vụ/quyền chi do chủ dự án cung cấp sau; corpus có quyền/nhãn và kết quả model thực tế. [Checklist M3](../phase-03-acceptance.md) phân biệt phần local đạt và điều kiện tổng thể chưa đạt. Không làm rubric điểm/match_pair/AI viết CV/nguồn học hoặc C–E.

Sau kiểm tra dừng API/Vite và container riêng, xóa file demo-env tạm; chỉ giữ artifacts/config DB thử bị ignore. `cleanup.json` xác nhận. Không dừng/xóa dịch vụ/dữ liệu khác, không reset DB ứng dụng, không commit/push hoặc deploy. Source manifest/hash và summary test được lưu private cùng evidence.

Rà soát cuối: 12 tệp Markdown/89 liên kết local và code fences không có lỗi; `git diff --check` đạt với cảnh báo LF/CRLF theo cấu hình Git. `execution-summary.json` đối chiếu số test từ TRX/Playwright, `source-manifest.json` ghi hash source/config/docs thực ở working tree; không coi hash hoặc CI baseline là chứng nhận cho kiểm thử chưa chạy.
