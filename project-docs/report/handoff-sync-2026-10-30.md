# Đồng bộ bản demo/bàn giao 30/10/2026

Đã chuyển demo localhost5190 sang release20261030 có **Xem kết quả đã lưu**, giữ runtime29/10 và toàn bộ tài khoản/dữ liệu synthetic hiện có. Gói cũ giữ hash lịch sử. Phần giảng viên/người đánh giá thật được chủ dự án tạm hoãn; **M6 chưa nghiệm thu**.

## Phiên bản và dữ liệu

Launcher `scripts/handoff-demo.py start|fault|stop --release 20261030` chọn runtime mới tại `.local/handoff-demo-20261029/releases/20261030/runtime`. Start/fault mặc định chọn20261030. Bản cũ ở `runtime/`; `--release 20261029` chỉ dùng runtime lịch sử đã có, không build source mới dưới nhãn cũ. `process.json` ghi release/runtime/DLL hash. Không reset database, không migration/schema mới; dữ liệu vẫn ở container/volume cùng tên ngày29/10 và storage cũ.

Backup offline trước chuyển tại `.local/handoff-demo-20261029/backups/20261030T043210Z/`: restore database riêng khớp43bảng,7objects và DP keys. Sau chuyển lần đầu, đối chiếu toàn bộ43nội dung bảng và hash7objects với backup đều khớp trước khi diễn tập thêm. Các proof này không phải kiểm chứng restore/replay ledger dữ liệu production. Phiếu Word gốc, file tiến độ, ledger thật và gói29/10 được kiểm giữ hash.

## Kiểm tra mới

- Chạy `python scripts/test-frontend-regression.py` trên DB/runtime tạm riêng: công cụ6/6, frontend11/11 + lint/typecheck/build, backend160/160, E2E21/21 đạt, không skip. E2E gồm tích hợp và mock; không gọi toàn bộ21ca là provider thật. Run `.local/frontend-step3-20261030-113503-4cad/` completed/cleanup true, non-Fake methods0, USD budgets0, chỉ Fake/test_credit5 và reserved0. Container tạm đã dọn.
- Trên bản static production frontend/API thực tế5190: login Candidate synthetic hiện có, chọnCVv1, tìm lại tin, mở kết quả cũ qua nút mới sau reload toàn trang. Kết quả đúng job lịch sử; mở tiêu chí Docker/nguồn học qua UI, không sửa request hoặc giả response. Pha xem lại có0POST matching/recommendation, CV vẫn3versions/private và đơn cũreviewing/CVv1. Ghi ảnh/video có banner diễn tậpAI/Fake; không quay mật khẩu/cookie.
- Đã chạy vòng quay lại runtime29/10 → runtime30/10 → fault30/10 → start30/10. Health200 và cookie dùng được trong vòng thử này; đọc lịch sử được khi AI tắt, yêu cầu writing mới bị503 `ai_disabled`. Kết thúc mode start/release20261030. Không suy thành bảo đảm cookie sống qua mọi kiểu restart.
- Docker image mới `careergap:handoff-20261030` đã build. Smoke Linux dùng database clone synthetic tạm riêng từ backup: health/jobs/root/staticJS/workerJS và authenticated latest lookup đạt khi AI tắt. Không dùng database ứng dụng hoặc storage demo cho Linux smoke; container và cloneDB tạm được dọn. DLL Windows demo, DLL integration test và imageLinux có hash riêng, không gán chung kết quả nhị phân.
- Gói source mới có manifest SHA-256 từng file và kiểm tra bí mật; không kèm `.env`, key, account, DB, corpus thật, dependency hoặc build output. Kiểm tra giải nén/build được ghi bằng receipt cạnhZIP để tránh vòng hash tự tham chiếu. Giải nén trên cùng máy Windows, có cache dependency, không gọi là thử một OS/máy hoàn toàn mới.

Video đầu thử chưa hoàn thành do locator select của script không khớp accessible label; chỉ sửa script kiểm tra sang select đúng vùng đã quan sát, không sửa sản phẩm hoặc response. Lượt hoàn chỉnh và ảnh cuối được giữ public, lượt dở/log private giữ để truy vết.

## Bộ bàn giao và giới hạn

[Bộ mới](../handoff/stage-e-20261030/README.md) có hướng dẫn dùng nút xem lại, vận hành/rollback, ma trậnM6, ghi chú thuyết trình, blank synthetic fixtures, ảnh/video mới và ZIP source. Slide/video nền29/10 được ghi rõ là phiên bản nền; không sửa lại slide cũ hoặc gọi video cũ là bằng chứng của nút mới. Source mới giữ các assets nền cần theo link, nhưng không lồng ZIP cũ.

[Manifest đồng bộ](../evaluation/handoff-sync-2026-10-30.json) ghi hash từng binary/artifact và proof. [Sửa chức năng](pair-history-followup-2026-10-30.md) vẫn giữ kết quả trước đồng bộ làm lịch sử. Các gói27/29/10 không đổi. Không push/deploy cloud; commit cục bộ nếu có được ghi bằng receipt riêng sau đóng gói, không giả commit nằm sẵn trong nội dung ZIP.

Không đổi rubric, baseline, method/prompt, corpus, nhãn hoặc kết quả thực nghiệm27/10; không gọi DeepSeek/Tavily/Resend thật và không chi phí provider mới. Số liệu chất lượng vẫn là regression tập đã xem, chưa chứng minh tính đại diện hoặc LLM tốt hơn baseline. Những đề xuất dịch thuật/stop-words chưa tự mở rộng thành yêu cầu mới. Không thêm human labels hoặc phản hồi giảng viên.

**M6 giữ chờ, đánh giá ngoài hoãn cho đến khi chủ dự án mở lại.**
