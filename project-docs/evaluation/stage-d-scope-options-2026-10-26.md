# Các phương án để điều chỉnh phạm vi đánh giá

Ngày 26/10/2026. Owner đã trả lời **“Cần điều chỉnh phạm vi”** cho đề xuất 4×4. Chưa có phương án thay thế được chọn; bảng này hỗ trợ quyết định, không đăng ký dữ liệu mới, cấp quyền transfer hoặc giữ chỗ ngân sách.

**Đã chốt sau đó:**4×4 là mục tiêu có điều kiện, không là minimum; pool theo nhóm độc lập thực có đủ nhãn/quyền, giữ k=3/trần dự toán0,50USD, ưu tiên CNTT và báo coverage Việt/Anh riêng. Không bù bằng biến thể/synthetic, không cấp chi/call/transfer. Bảng k=2 dưới đây chỉ giữ lịch sử tham khảo, không còn là lựa chọn hiện hành. [Protocol v2](stage-d-final-protocol-2026-10-26.md) là hướng dẫn hiện tại.

| Quy mô canonical | k minh họa | Jobs E: extraction + cặp + writing | Dự trù AI E, gồm tối đa 2 attempts/job | Nếu sau này thực sự cần thêm 8 jobs demo live |
|---|---|---|---|---|
| 2 CV × 3 JD | 2 | 2 +6 +4 =12 | 0,17418240 USD | Tổng 0,29030400 USD |
| 3 CV × 3 JD | 2 | 3 +9 +4 =16 | 0,23224320 USD | Tổng 0,34836480 USD |
| 4 CV × 4 JD — đề xuất cũ, chưa chốt | 3 | 4 +16 +4 =24 | 0,34836480 USD | Tổng 0,46448640 USD |

Writing minh họa dùng hai CV đã chọn × hai nhãn ngôn ngữ, không tự dịch facts. Chi phí dựa cùng cấu hình và giá bảo thủ của [bản dự toán](stage-d-evaluation-budget-2026-10-26.json); chưa live balance hoặc allocation. Demo mặc định replay bằng chứng synthetic hiện có, không cần call mới. Search/email tính riêng: đề xuất cũ 4 free search credits chưa được cấp, email 0 mới. Không bật PAYG.

Quy mô nhỏ hơn giảm số nhãn cặp và chi phí, nhưng giảm coverage; với hai CV/k=2, chiều JD→CV chọn toàn pool nên không đo được mất mát do chọn tập con ở chiều đó. Một CV làm ranking JD→CV suy biến. Mọi phương án vẫn là thí nghiệm nhỏ, không tự đủ đại diện. k=1 chỉ có một phần tử để rerank nên không thể chứng minh cải thiện thứ tự của tập đã chọn. Không chọn k sau khi xem kết quả.

Có thể đổi **coverage** mà chưa đổi số nhóm: ưu tiên CNTT, CV gốc Việt/Anh, PDF/DOCX theo nguồn có thật. Bản dịch/định dạng khác cùng nguồn không tăng nhóm độc lập; không dùng synthetic thay CV gốc Việt. Đổi coverage không tự cấp quyền gửi provider hoặc biến final cũ thành mới chưa chạm.

Phản hồi cần chốt: số nhóm CV/JD; ngôn ngữ/định dạng/ngành ưu tiên; giới hạn chi phí muốn đổi nếu có. Sau đó đăng ký nguồn/quyền thực, nhãn trước output, sửa protocol/budget/top-k và ghim bản mới. Không cần lặp thao tác writing/PDF/email/search đã có bằng chứng; không gửi secret trong chat.
