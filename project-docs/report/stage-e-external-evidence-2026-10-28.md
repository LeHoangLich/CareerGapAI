# Bổ sung bằng chứng bên ngoài E — 28/10/2026

## 1. Dùng thử của chủ dự án

Chủ dự án xác nhận mình thực hiện các luồng U1–U5 ngày 26/10/2026. Đây là ngày do owner khai báo, trước bản bàn giao E sau sửa ngày 27/10; không gán lượt đó cho bản sửa. Ghi chép cũ được giữ nguyên, actor là chủ dự án, không phải giảng viên/người đánh giá độc lập.

U5 lịch sử: AI bị từ chối bằng thông báo chung, CV vẫn tải được; phần không nhớ giữ chưa xác nhận. Lượt kiểm lại được owner báo ngày 28/10 cho thấy thông báo rõ AI tắt/chưa tạo yêu cầu, CV tải và mở được, phiên đăng nhập giữ nguyên. Đây là owner report, không giả có HTTP capture của chính lượt owner.

U1 kiểm bổ sung: owner xác nhận thấy phần rà soát/nguồn, dữ liệu khớp nguồn và đã lưu xác nhận. Ba ảnh cho thấy chế độ mô phỏng/Fake, trường React kèm quote/locator page-1-block-5, revision 2 chờ xác nhận → revision 3 đã xác nhận và thông báo lưu. Hoàn thành chứng cứ UI U1 trong phạm vi thấy được; không suy ra độ đầy đủ mọi trường hoặc chất lượng DeepSeek. Ghi chép U2–U4 giữ nguồn owner, không nâng thành phản hồi giảng viên. Phiếu Word gốc không sửa.

## 2. Đối soát tổng Tavily đã hoàn thành

Lúc **17:13 ngày 28/10/2026, Asia/Saigon**, owner cung cấp ảnh Overview và Billing cùng hiển thị **4/1.000 credits**, gói Researcher Free, Pay-as-you-go tắt và 0 paygo credits. Owner xác nhận không dùng Tavily ngoài CareerGapAI từ 23/10 đến nay. Tổng này khớp journal **D2 + E bổ sung2 = 4 credits**.

Usage chi tiết bị khóa bởi gói trả phí. Các số 0 mờ trong màn bị khóa không được dùng làm số liệu thực hoặc để phủ nhận Overview/Billing. Đối soát ở mức tổng dashboard hoàn thành; chưa kiểm log từng request/hóa đơn độc lập, không kết luận nguyên nhân Usage API cũ chậm cập nhật. Các response API trước/sau của ngày 27/10 vẫn được giữ nguyên.

Ảnh Billing ghi Resets monthly; không thể xác định ngày/giờ/múi giờ reset chính xác từ ảnh. Không dùng khoảng ngày mờ của Usage để coi là chu kỳ thanh toán. Ledger ghi thêm biên nhận đối soát, giữ debit E2, D2, allowance0 và unused2 đã đóng; không refund/reset/cấp thêm lượt. Không request provider, chi phí hoặc thay đổi runtime mới trong bước ghi nhận này. Ảnh có thông tin tài khoản nên chỉ giữ private và ghim hash.

## 3. Phần còn cần người đánh giá

Đã có nguồn xác nhận ghi chép owner và bổ sung U1/U5; phần đối soát tổng Tavily không còn chờ. **M6 vẫn chưa nghiệm thu** vì chưa có biên bản/phản hồi của giảng viên hoặc người đánh giá thực tế cùng nhận xét về chất lượng và giới hạn. Chưa có ngưỡng chất lượng được người đánh giá chấp nhận.

Người đánh giá cần được xem rõ: kết quả sau sửa là regression trên tập đã xem; CV F1 khoảng0,623; rerank chưa cải thiện baseline; 2CVAnh×4JD, thiếu CV Việt/JD Việt CNTT và nhãn human benchmark. Dùng thử synthetic/Fake kiểm chức năng, không thay thế đánh giá chất lượng provider. Không cộng ghi chép UI vào số nhãn benchmark.

Source/DLL/method/dataset/metric, slide/ZIP/manifest và kết quả ngày27/10 giữ nguyên làm lịch sử. Bổ sung ngày28/10 là lớp bằng chứng mới, không thay kết quả thực nghiệm hoặc tự đóng M3/M6.

[Manifest bổ sung](../evaluation/stage-e-external-evidence-2026-10-28.json) · [Bộ dùng thử](../handoff/stage-e-fixed-20261027/README.md)
