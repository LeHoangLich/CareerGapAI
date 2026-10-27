# Hướng dẫn gốc từ M5 sang E và biên nhận thực thi

> Đã thực thi E theo freeze đóng gói v2; đây là hướng dẫn trước chạy được giữ để đối chiếu. Không rerun dispatch/setup/settlement hoặc reset ledger. Xem [báo cáo kết quả](../report/stage-e-2026-10-27.md) và [bộ dùng thử Fake](../handoff/stage-e-20261027/huong-dan-dung-thu.md). M6 vẫn mở.


Ngày27/10/2026: bước7 đã đối chiếu và chấp nhận M5 trong phạm vi chức năng localhost + giao thức thực nghiệm hẹp đã chốt. Không cần owner lặp lại quyền/giới hạn đã cấp. M3 chất lượng đại diện, kết quả P20, dùng thử giảng viên, bản bàn giao/restore sạch và M6 vẫn chưa được công nhận.

## 1. Kiểm tra đầu vào trước P20

Codex dùng [manifest M5](../evaluation/m5-local-acceptance-2026-10-27.json), [final freeze](../evaluation/stage-d-step6-final-freeze-2026-10-27.json), [protocol](../evaluation/stage-d-final-protocol-2026-10-27.md) và dữ liệu private `D:\CareerGapAI\.local\stage-d-20261023\step6-final-20261027\`.

Kiểm lại metadata/hash bằng lệnh sau trước khi đọc output cuối hoặc gọi API:

```powershell
python scripts/check-stage-d-freeze.py project-docs/evaluation/stage-d-step6-final-freeze-2026-10-27.json
```

Kết quả phải valid/ready, không errors/pending. Kiểm thêm hash input lock, dataset, labels, application bindings và source/DLL theo manifest; công cụ không tự cấp quyền API. Authoritative DLL cho E vẫn ở `step5/build-artifacts/bin/CareerGap.Api/debug/CareerGap.Api.dll`, SHA-256 `a21e45f59c8ff7982660cd233a3830a2d9064cbf1b0e5d02a32703a42aa29319`. Docker image vừa build chỉ là bằng chứng Linux của cùng source, không thay DLL đã freeze.

DB/volume chuẩn bị E ở container `careergap-step6-eval-20261027`, loopback55442, database `careergap_eval`, hiện dừng. Dump/hash/roles/versions/revisions và options nằm trong packet. Chỉ mở container này khi thực hiện E; không dùng DBứngdụng hoặc DBtest vừa dọn. Không rerun script nhập dữ liệu để tạo IDs mới; mọi pair/writing phải giữ revision/fingerprint đã ghim. Các confirmed references do Codex nhập theo ủy quyền, không giả human/owner review.

Đối soát read-only số dư/credits và ledger trước dispatch; không in key hoặc lấy key vào frontend. Trần phát triển2USD giữ nguyên: trackedspent0,03 +hold0,02, guarded1,95; active allocation E0,50, phần chưa phân bổ1,45. Search tối đa4creditsfree/0USD, không reset2credit cũ hoặc bậtPAYG; email0mới; demo dùng replay0calls. Thiếu số dư/freecredits hoặc có unknown/variance mới thì giữ chặn admission và xử lý bằng chứng, không tự tăng cap.

## 2. P20 — chạy đúng thí nghiệm đã đăng ký

1. Khởi động môi trường E riêng bằng options đã ghim; mặc định worker đang tắt, search/email Fake. Chỉ đưa keybackend vào tiến trình cần provider sau khi các kiểm tra đầu vào và ledger đạt; không sửa `.env` hoặc các tiến trình người dùng đang chạy. Period/cap phải khớp DB hiện có, không tạo kỳ/DB mới để cấp lại lượt.
2. Dùng `execution-plan.private.json`:14requests với idempotency keys cố định, gồm2CVextraction/8selfpairs/4writing. Nhận tuần tự, tổng≤28attempts, tổngtrần0,50USD. Timeout không tạo key mới để gửi lại; ghi job/attempt/unknown/cost đúng thực tế. Trước pair/writing kiểm expected revision/fingerprint. Không confirm raw extraction mới đè tham chiếu matching đã freeze.
3. Baseline chạy đúng dataset cuối với k3 bằng `--evaluate-keyword-baseline <dataset> <output> 3` trên DLL ghim. Lưu JSON trước tổng hợp, không ghi đè file cũ. Baseline text đã cố định theo source-review; parser captures/provenance hiện hành tách riêng, không tuyên bố benchmark này là kiểm parser end-to-end mới.
4. Lưu raw output/error/finish reason/usage trước sửa. Dùng capture endpoints đã có cho CV/pair; writing không tự accept/lưu version sản phẩm. Đầu vào evaluator CV đã chuẩn bị ở `cv-capture-dataset.final.private.json`. Sau khi E có baseline/captures thật, tạo envelope kết quả riêng từ template bằng hashes của đúng outputs và tham chiếu input lock; không tạo hash hoặc capture giả. Chạy final pair evaluator chỉ trên envelope này.
5. Tổng hợp extraction trước sửa; retrieval/ranking hai chiều cùng pool; status/quote/provenance và exclusions; writing wholefacts/groups; gap-topic. Chỉ search từ gap thật hỗ trợ topic đã ghim, tối đa Docker/Git×vi/en4credits; không có gap phù hợp thì báo coverage, không search để lấy mẫu thay thế. Ghi URL/title/source thực, kiểm access theo quan sát, dọn metadata theo retention.
6. Báo requested/admitted/succeeded/failed/missing riêng; median/p95 và usage/fee đúng mẫu số. Rerank tập baseline chọn, không thêm cặp; relevance0–3 không là goldscore0–100. Nếu kết quả không tốt vẫn giữ và báo, không tune theo final rồi gọi độc lập.

Bộ thực chỉ2CVAnh/4JD; không cóCVnativeViệt/JDViệtCNTT hoặcCVbốgụccũ. Giữ rõ source/parser/JD exposure, referenceCodex/human0,28criterionexclusions,5user_statement facts và giới hạn chiềuJD→CVpool2. Không bù bằng synthetic hoặc bản dịch, không tự chốt chất lượng đại diện/M3 từ kết quả hẹp.

## 3. P21 — ổn định và bàn giao

Sau P20 mới xử lý hồi quy/tồn đọng phù hợp; sửa phương pháp cần phiên bản mới và xử lý lại ảnh hưởng tới final. Thử backup/restore DB+objects+DataProtection+deletionledger cô lập; thử hướng dẫn ở môi trường sạch; đối chiếu expiry/retention thủ công; tổ chức dùng thử thực với giảng viên và ghi phản hồi thật; hoàn thiện báo cáo/slide/demo và video dự phòng có nhãn. Không coi ghi lịch hoặc replay là đánh giá người dùng mới. M6 chỉ được đóng khi đủ bằng chứng bắt buộc. Deploy vẫn hoãn; commit/push chưa được yêu cầu.

Lượt bước7 đã dừng/dọn API5087/Vite5177, PGtest55443 và Docker smoke5088 riêng; không để lại worker có thể gọi provider. Docker image M5 giữ local làm bằng chứng, không push. README vừa được cập nhật; chưa tuyên bố đã thử cài sạch/restore bàn giao ở phiên bản này.
