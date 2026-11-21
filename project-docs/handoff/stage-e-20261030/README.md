# Bản bàn giao localhost — 30/10/2026

**Cập nhật tài liệu 31/10, không thay runtime/ZIP:** [Cẩm nang thuyết trình và phản biện](thuyet-trinh.md) đã đồng bộ quyết định chấp nhận pilot, rà318nhãn, ma trận35mục và A12/A13. M6 vẫn chờ đánh giá ngoài đang hoãn. A12 đã đạt backend160/unit15/E2E22 ở source mới, **chưa có trong demo5190/ZIP30/10**; test của release30 vẫn backend160/unit11/E2E21. A13 đã đối soát5tiêu chí, không phát hiện bug chức năng trong phạm vi rà; nguồn tiếng Việt chưa được chứng minh. Xem [biên bản đồng bộ tài liệu](../../report/defense-docs-sync-2026-10-31.md).

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
