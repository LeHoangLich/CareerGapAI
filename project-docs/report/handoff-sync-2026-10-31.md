# Đồng bộ demo/bàn giao release20261031

Releaser: **Codex (Authorized by Project Owner)**. Approved by: **Lê Hoàng Lịch (Project Owner)** theo chỉ thị giao phát hành. `rollback_verified=true`; `m6_status=pending_external_review`. Không có phản hồi giảng viên mới; đánh giá ngoài vẫn hoãn.

## Phiên bản và dữ liệu

Launcher hỗ trợ29/30/31, mặc định start/fault chọn31; runtime mới ở `.local/handoff-demo-20261029/releases/20261031/runtime`. Guard không build source31 dưới nhãn29/30 khi runtime cũ thiếu. Binary và gói lịch sử giữ hash; không reset database/container/accounts/storage.

Backup offline `20261031T122502Z`: dump/objects/DPkeys giữ private; restore clone riêng khớp43bảng/7objects/DPkeys, clone kiểm đã dọn. Sau upgrade trước diễn tập,43nội dung bảng và7hashobjects khớp backup. Container demo ban đầu dừng làm lượt backup đầu thất bại; giữ log/thư mục dở, sau khi khởi động đúng container đã backup/verify lại thành công. Không coi lượt lỗi là đạt.

## Kiểm thử thực hiện mới

- Pipeline `.local/frontend-step3-20261031-192425-d924/`: backend160/160, frontend15/15, E2E22/22, lint/typecheck/build/migration/seed đạt; completed/cleanup true. Fake-only, không nạp dotenv, nonFakeMethods0/USDbudgets0. E2E gồm ca tích hợp và mock, không phải provider thật. Vite có cảnh báo bundle>500kB không chặn build.
- Runtime5190: Candidate synthetic đăng nhập, history qua reload trả job lịch sử đúng; mở Docker topics,0POSTmatching/recommendation;3versions/private và đơnreviewing/CVv1 giữ. Video/ảnh mới có actor diễn tập tự động.
- A12 trên runtime5190, không intercept: invalid token→HTTP400 invalid_or_expired_reset→thông báo cụ thể/nút gửi lại→formforgot; querytoken/lỗi cũ xóa,0requestemail tự động. Không đổi mật khẩu tài khoản demo. Screenshot đã xem, bố cục/thông báo/nút rõ. Token expired được kiểm trong backend regression; E2E dùng used token; UI5190 dùng invalid token, không đồng nhất ba cách tạo lỗi.
- Vòng31→30→31→fault31→31: mỗi lần health200 và đúng release/hashDLL; cookie vẫn authenticated trong vòng này. Fault31 đọc history, builder-content được; downloadCV200 khớp documentHash; writing mới503 ai_disabled. Kết thúc start31. Không tuyên bố vòng29 được chạy lại hoặc kiểm mọi lỗi mạng.
- Runtime Windows31 DLL SHA-256: `21b449d8110bbda1bf007cd1f18c74b8e16410ccb811de7253ce5b6cee270387`. Runtime30 vẫn `6f193cfddc4d7ff71ec223cc33f749388a7313efa694f6c12f7e2ed07d0aab6c`. DLL test có hash riêng, không đánh đồng binary. Không build imageLinux mới trong phạm vi này.

## Đóng gói và bảo toàn

[Bộ31](../handoff/stage-e-20261031/README.md) gồm hướng dẫn, rollback, M6, cẩm nang cập nhật, fixture synthetic/phiếu trống, video history, ảnh reset và ZIP nguồn sạch. `SOURCE-MANIFEST.json` trong ZIP ghim từng file; receipt cạnhZIP kiểm hash/archive và gói giải nén, tránh hash tự tham chiếu. Dependency/build output/DB/accounts/privatecorpus/.env không thuộc ZIP. Bản nguồn đóng gói theo bytes working tree đã kiểm, không giả định các sửa chưa commit nằm trong HEAD.

357 file được ghim trước thao tác gồm tài liệu/gói27/29/30, runtime29/30, ledger/accounts/config. Các gói và file cũ giữ nguyên; chỉ tạo bộ31. Cẩm nang31 ghi A12 đã có runtime; cẩm nang30 giữ nội dung theo thời điểm trước phát hành. PPTX nền29 giữ phiên bản riêng, không tạo slide mới hoặc gán thành bằng chứngA12.

Không thay rubric/method/corpus/nhãn/metric/retention hoặc ngân sách. Không providerAPI thật, không credit/USD mới, không deploycloud/push. Backup/restore và demo dùng synthetic local, không chứng nhận production hoặc chất lượng đại diện. Owner đã chấp nhận pilot; M6 vẫn pending_external_review.

[Manifest đồng bộ](../evaluation/handoff-sync-2026-10-31.json) · [Receipt gói](../handoff/stage-e-20261031/verification.json).
