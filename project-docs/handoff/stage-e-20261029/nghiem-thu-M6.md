# Đối chiếu nghiệm thu M6 — 29/10/2026

**Kết luận: bộ bàn giao kỹ thuật đã đồng bộ; M6 chưa đạt toàn bộ.** Quyền ủy nhiệm cho agent không thay thế buổi dùng thử hoặc quyết định chất lượng của người đánh giá. Phần nghiên cứu được chấp nhận ở phạm vi nào vẫn cần người đánh giá xác nhận.

| Điều kiện roadmap | Kết luận và bằng chứng |
| --- | --- |
| Chức năng và đánh giá bắt buộc hoàn tất | Chức năng local đã có kiểm thử; **chưa đầy đủ** vì còn buổi dùng thử/phản hồi giảng viên và nhận xét phạm vi/chất lượng |
| Dữ liệu, nhãn, cấu hình và mã kiểm tra lại được | Đạt trong phạm vi mẫu hẹp2CVAnh×4JD; input/method/nhãn/captures/metric có lineage. Raw corpus/output giữ private, không phát kèm source |
| Kết luận phản ánh kết quả | Đạt về trình bày: giữ E đầu thất bại, F1 sau sửa0,623, rerank không cải thiện, tập sau sửa đã xem và human labels0 |
| Không còn blocker ba luồng/sai quyền nghiêm trọng | Đạt các ca local đã chạy: backend159/159 và bộ E2E21/21 có ca tích hợp + mock; production rehearsal ba luồng dùng worker Fake và snapshot bytes khớp |
| Hướng dẫn chạy/khôi phục phù hợp bản giao | Launcher static mới đã chạy; backup offline/restore43bảng và toàn bộ objects/DPkeys khớp. Replay deletion ledger/quyền cookie cũ giữ chứng cứ E27/10 trên backend source không đổi, không gán là lượt replay mới |
| Diễn tập và giải thích kết quả | Diễn tập tự động synthetic/Fake có video mới; owner U1/U5 có receipt. Chưa có bằng chứng giảng viên dùng thử hoặc đánh giá thay cho chủ dự án |

## Đầu vào ngoài agent còn cần

1. Giảng viên thực sự xem/thử tác vụ, ghi vai trò, ngày giờ, bản được xem, kết quả và nhận xét. Chấp nhận quan sát qua chia sẻ màn hình khi ghi đúng hình thức; không gọi thao tác của owner là thao tác độc lập của giảng viên.
2. Nhận xét về rubric, mẫu/nhãn và giới hạn chất lượng. Hiện thiếu CV gốc Việt/JD Việt CNTT và human review benchmark; chưa có ngưỡng được duyệt. Nếu phạm vi2×4 được chấp nhận, báo đúng giới hạn. Nếu cần mở rộng, bổ sung dữ liệu có quyền, nhãn và kế hoạch đánh giá riêng trước khi chạy.
3. Xử lý góp ý có ảnh hưởng rồi kiểm lại và tạo revision bàn giao mới. Không sửa phương pháp theo output rồi gọi chính tập đó là final độc lập mới. Không bắt buộc LLM phải thắng baseline, nhưng kết luận phải dựa trên kết quả thực tế.

[Phiếu mẫu trống](Phieu-dung-thu-CareerGapAI.docx) · [Hướng dẫn](huong-dan-dung-thu.md). Các khoản credits/quota đã đối soát không cần owner xác nhận lại. Chưa deploy hoặc mở thêm allowance provider.
