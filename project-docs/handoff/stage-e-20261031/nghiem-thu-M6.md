# M6 — ĐÃ NGHIỆM THU NỘI BỘ TOÀN DIỆN (SẴN SÀNG BẢO VỆ)

**Ngày quyết định:01/11/2026. Người nghiệm thu: Lê Hoàng Lịch (Project Owner).** Loại nghiệm thu: `internal_owner_acceptance`. Trạng thái cuối: **`final_defense_ready`**. M6, Giai đoạn E/GĐ6 và phạm vi dự án nội bộ đã hoàn thành theo quyết định owner.

Release20261031 là **Defense Baseline**. Mã nguồn ứng dụng/công cụ được đóng băng bằng [manifest source freeze](../../evaluation/source-freeze-2026-11-01.json). Quyết định này thay điều kiện dùng thử ngoài không chính thức trước bảo vệ: thủ tục đó đã khép lại, không còn là gate của M6 nội bộ. Kết quả Hội đồng tại buổi bảo vệ chính thức sẽ được ghi theo thực tế, không được suy ra từ nghiệm thu này.

| Điều kiện nghiệm thu nội bộ hiện hành | Kết luận | Căn cứ |
|---|---|---|
| Chức năng cốt lõi và kiểm thử local | **ĐẠT** | Release31:160/160 backend PostgreSQL,15/15 unit,22/22 E2E; lint/typecheck/build đạt ngày31/10 |
| Đánh giá pilot và phạm vi chất lượng | **ĐẠT — chấp nhận giới hạn** | Owner chấp nhận2CVAnh×4JD,6nhóm,131nhãn final,8cặp,k3; không đòi metric đại diện hoặc rerank phải thắng baseline |
| Nhãn, dữ liệu và cách tính truy vết được | **ĐẠT** | 318nhãn rà kỹ thuật theoủyquyền;300tọa độ chuẩn hóa, thêm3needsReview; human direct review0 vẫn công khai |
| Kết luận phản ánh đúng thực nghiệm | **ĐẠT** | ExactF1=0.6231884057971014 trên95nhãnCV;34/44 trạng thái; nDCG0.9169956161990744 so baseline1.0 là regression; partial chưa đo |
| A12 và A13 được xử lý trong phạm vi | **ĐẠT** | A12 có mã nguồn/runtime và kiểm chứng; A13 rà đủ5tiêu chí, giữ giới hạn ngôn ngữ/video và không sửa adapter thừa |
| Các luồng và failure modes đã định nghĩa | **ĐẠT** | History sau reload, reset invalid→forgot, fault503, builder/download theo quyền; không suy thành mọi ca đều được bao phủ |
| Hướng dẫn, backup/restore và rollback | **ĐẠT** | Restore43bảng/7objects/DPkeys và nâng cấp khớp; rollback30→31/fault/recover; runtime29/30 giữ hash |
| Báo cáo, cẩm nang, phản biện và demo dự phòng | **ĐẠT** | Ma trận35mục đã xử lý/phân loại theo phạm vi, B01–B14 sẵn sàng; nội dung mô phỏng bổ sung ghi đúng nguồn; media và hướng dẫn theo phiên bản |
| Quyền quyết định và thủ tục nghiệm thu nội bộ | **ĐẠT** | Owner trực tiếp quyết định đóngM6, chốt release31 làm bản bảo vệ; không cần tiếp tục thu thập dùng thử ngoài trước buổi bảo vệ |

Các điều kiện trên là điều kiện **nghiệm thu nội bộ đã được owner điều chỉnh và chấp nhận**; không đổi sự thật của dữ liệu hoặc hồi tố rằng giảng viên đã dùng thử. A13 ĐẠT về việc đối soát/chấp nhận giới hạn, không có nghĩa nguồn tiếng Việt đã được chứng minh. Không nâng pilot thành representative/production-ready hoặc human gold.

[Biên bản nghiệm thu chính thức](../../report/m6-internal-acceptance-2026-11-01.md) · [Manifest hiện hành](../../evaluation/m6-internal-acceptance-2026-11-01.json) · [Cẩm nang](thuyet-trinh.md).

Receipt phát hành31 và nội dung trong ZIP nguồn31 giữ trạng thái tại ngày31/10. Hồ sơ nghiệm thu01/11 bổ sung trạng thái hiện hành; không ghi đè archive hoặc sửa metric/backup để hợp thức hóa quyết định.
