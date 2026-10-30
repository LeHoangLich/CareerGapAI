# Bản bàn giao localhost — 30/10/2026

Bản này bổ sung **Xem kết quả đã lưu** cho cặp CV–JD, không tạo yêu cầu AI mới khi xem lại. Provider của demo luôn Fake. M6 chưa nghiệm thu; phần đánh giá giảng viên/người đánh giá thật đang được chủ dự án tạm hoãn.

- [Chạy và dùng thử bản mới](huong-dan-dung-thu.md).
- [Vận hành, sao lưu và quay lại phiên bản cũ](van-hanh-local.md).
- [Trạng thái M6 và phạm vi kiểm chứng](nghiem-thu-M6.md).
- [Video và ảnh của chức năng mới](demo.html).
- [Mã nguồn có manifest SHA-256](CareerGapAI-source-20261030.zip).
- [Ghi chú thuyết trình](thuyet-trinh.md).
- [Báo cáo đồng bộ và kết quả kiểm tra](../../report/handoff-sync-2026-10-30.md).

Tài khoản, mật khẩu, CV synthetic đã tạo và dữ liệu demo cũ được giữ tại `.local/handoff-demo-20261029/`. Đây là tên vùng dữ liệu, không phải phiên bản binary đang chạy. Runtime ngày 30/10 đặt riêng trong `releases/20261030/runtime`; `process.json` ghi release và hash DLL hiện hành.

Slide và video ba luồng ngày 29/10 là **tài liệu nền có ngày phiên bản riêng**, không chứng minh nút mới. Xem [bộ nền](../stage-e-20261029/README.md) khi cần; bằng chứng mới nằm trong bản 30/10. Không đổi hoặc đóng gói lại ZIP cũ. Bộ source mới không kèm key, tài khoản, database hoặc corpus thật.
