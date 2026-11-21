# Bàn giao localhost — release20261031

**M6 ĐÃ NGHIỆM THU NỘI BỘ TOÀN DIỆN — SẴN SÀNG BẢO VỆ**, theo quyết định Lê Hoàng Lịch ngày01/11/2026. Release20261031 là **Defense Baseline**; mã nguồn đã đóng băng, phạm vi dự án nội bộ hoàn thành. Trạng thái: `final_defense_ready`; loại: `internal_owner_acceptance`.

Đã đồng bộ A12 (lỗi reset cụ thể và nút yêu cầu link mới) cùng chức năng xem kết quả cặp đã lưu từ30/10. Demo5190 dùng **Fake/Synthetic**, không nạp.env hoặc gọi provider thật. Thủ tục dùng thử ngoài trước bảo vệ đã khép lại theo owner; buổi bảo vệ chính thức được ghi nhận riêng theo thực tế.

- [Hướng dẫn dùng thử](huong-dan-dung-thu.md)
- [Vận hành, backup và rollback](van-hanh-local.md)
- [Cẩm nang bảo vệ](thuyet-trinh.md)
- [Trạng thái M6](nghiem-thu-M6.md)
- [Ảnh/video mới](demo.html)
- [Source ZIP31](CareerGapAI-source-20261031.zip), kèm `SOURCE-MANIFEST.json` trong ZIP
- [Verification receipt](verification.json)
- [Biên bản phát hành](../../report/handoff-sync-2026-10-31.md)
- [Biên bản nghiệm thu nội bộ01/11](../../report/m6-internal-acceptance-2026-11-01.md)
- [Manifest trạng thái hiện hành](acceptance-status.json) và [source freeze222file](../../evaluation/source-freeze-2026-11-01.json)

Regression riêng: backend160/160, frontend15/15, E2E22/22, lint/typecheck/build đạt. UI trên runtime5190 kiểm history qua reload và reset invalid→forgot, không intercept. Rollback30→31/fault31/recover31 đạt; backup restore43bảng/7objects/DPkeys khớp. Không gán test này thành chất lượng AI hoặc phản hồi giảng viên.

Runtime31 ở `.local/handoff-demo-20261029/releases/20261031/runtime`; dữ liệu/accounts/storage vẫn tại vùng demo29. Runtime29/30 và gói27/29/30 được giữ nguyên. ZIP nguồn không chứa runtime/DB/accounts hoặc corpus private; rollback trên máy hiện tại dùng runtime cũ đã giữ, không build source31 dưới nhãn cũ trên máy mới.

Slide và video nền29/10 ở [bộ nền](../stage-e-20261029/README.md) giữ nguyên phiên bản; ảnh reset/video history31 là bằng chứng mới. Phiếu dùng thử trong bộ này là mẫu trống, không phải phản hồi bên ngoài.

**Cách dùng hồ sơ cuối:** sourceZIP31 và verification31 giữ nguyên hash/phạm vi lịch sử. Tài liệu nghiệm thu01/11 trong thư mục này và manifest mới bổ sung trạng thái hiện hành; việc cập nhật Markdown không tạo binary hoặc ZIP thay thế. Giữ cả nguồn31 và phụ lục nghiệm thu01/11 khi bàn giao để bảo vệ.
