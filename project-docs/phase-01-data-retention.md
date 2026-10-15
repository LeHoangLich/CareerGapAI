# Chính sách lưu/xóa dữ liệu — đã chốt

Người dùng xác nhận ngày 15/10/2026: **90 ngày / 7 ngày / 14 ngày**. Đây là yêu cầu sản phẩm cho triển khai GĐ2 trở đi; hiện prototype chưa có các API/worker xóa tương ứng, chưa có bằng chứng thực thi hoặc phục hồi đúng chính sách.

## 1. Các mốc và quyền truy cập

| Sự kiện | Thời hạn/quyền đã chốt | Hành vi cần triển khai |
|---|---|---|
| Đơn chuyển withdrawn hoặc rejected | Lưu tối đa 90 ngày từ thời điểm kết thúc của chính đơn đó | Recruiter phụ trách tin vẫn đọc được snapshot trong thời hạn; không sửa snapshot theo CV hiện hành |
| Nộp lại sau rút/bị loại | Cho phép nếu không có đơn còn hiệu lực và không bị chặn ở tin đó | Tạo application/snapshot/history mới với submission key mới; không đặt lại đồng hồ lưu giữ của đơn cũ |
| Recruiter chặn ứng viên | Chỉ tin hiện tại do recruiter đó phụ trách | Chặn đơn mới/nộp lại ở tin đó; không xóa hồ sơ cũ, không ảnh hưởng tin khác; recruiter phụ trách tin có thể bỏ chặn |
| Yêu cầu xóa tài khoản — thời điểm T0 | Thu hồi truy cập ngay | Tắt phiên/quyền đọc thông thường, gỡ công khai, ngừng phát hành AI và quyền recruiter đọc snapshot của tài khoản yêu cầu xóa, kể cả đơn còn trong 90 ngày |
| T0 đến trước T0+7 ngày | Có thể hủy yêu cầu sau xác minh danh tính | Chỉ mở luồng hủy có kiểm soát, không mở lại toàn bộ tài khoản để xác minh; nếu hủy thì tài khoản hoạt động lại, CV không tự công khai lại |
| Từ T0+7 ngày nếu không hủy | Xóa/ẩn danh dữ liệu chính | Thu hồi token/phiên, xóa nội dung cá nhân và file/revision/cache/snapshot liên quan theo mọi tham chiếu; giữ thống kê chỉ khi đã ẩn danh không còn truy ngược nội dung người dùng |
| Backup sau khi purge dữ liệu chính | Hết hạn trong tối đa 14 ngày tiếp theo | Mỗi bản sao chứa dữ liệu đã xóa phải bị xóa/hết TTL; trường hợp bình thường dữ liệu không còn trong backup chậm nhất T0+21 ngày |

Lưu thời gian UTC; ngày nghĩa là khoảng 24 giờ, dùng mốc server/database. Kiểm tra quyền bằng mốc thời gian khi đọc, không chờ job dọn mới ngừng cho recruiter xem đơn hết hạn. Quá hạn hoặc lỗi purge phải có trạng thái/lỗi vận hành và được xử lý; không tự dời thời hạn hoặc báo đã xóa khi mới enqueue.

Quy tắc 90 ngày áp dụng cho withdrawn/rejected, không tự kết thúc đơn đang submitted/reviewing/shortlisted hoặc tự chuyển trạng thái tuyển dụng. Nếu cần chính sách cho đơn tồn đọng/tin đóng mà chưa có trạng thái kết thúc, chốt bổ sung trước triển khai phần đó.

## 2. File, snapshot và dữ liệu dùng chung

- Đơn hết 90 ngày phải ngừng truy cập qua ngữ cảnh application và dọn dữ liệu riêng của đơn. File còn được một CV đang dùng hoặc snapshot khác hợp lệ tham chiếu không bị xóa chỉ vì một đơn hết hạn; phải kiểm tra tất cả tham chiếu và quyền từng ngữ cảnh.
- Yêu cầu xóa tài khoản ưu tiên hơn thời gian giữ đơn 90 ngày: không dùng việc recruiter từng nhận hồ sơ để giữ quyền đọc sau T0. Không có ngoại lệ giữ CV riêng ngầm định hoặc lưu vô hạn.
- Gỡ công khai phải gỡ cả bản gốc ở public context, bản ẩn liên hệ, preview, cache và kết quả AI liên quan. Bản đã tải xuống không thể bị thu hồi; signed URL đã phát hành chỉ mất hiệu lực theo TTL đã cấp.
- Xóa tài khoản không cascade xóa CV/đơn thuộc người khác. Với recruiter yêu cầu xóa, phải đóng/ẩn tin và ngừng quyền owner; các tham chiếu lịch sử của ứng viên khác cần tách khỏi thông tin nhận dạng recruiter, không tự chuyển owner hoặc xóa hồ sơ người khác.
- Những bản version/revision/result vốn bất biến chỉ được purge bằng quy trình lưu/xóa riêng có kiểm soát. Thêm migration tiến, thứ tự xóa/backfill và review FK/trigger; không tắt toàn bộ constraints hoặc sửa lịch sử bằng API thông thường.
- Metadata usage còn chờ đối soát không được bỏ mất để giả chi phí bằng 0. Giữ tối thiểu số liệu/attempt cần đối soát và loại liên kết nhận dạng theo thiết kế được review; không giữ toàn CV, prompt hoặc liên hệ để chờ hóa đơn.

## 3. Backup và phục hồi

- Có sổ xóa tối thiểu gồm ID kỹ thuật, thời điểm yêu cầu/purge và trạng thái dọn; không ghi lại CV, liên hệ, password/token. Giữ sổ đủ bao phủ thời gian còn backup có thể khôi phục dữ liệu đó.
- Restore chỉ chạy cô lập; áp lại sổ xóa/đối soát trước khi mở truy cập. Một bản restore không được hồi sinh tài khoản hoặc quyền recruiter đã bị thu hồi.
- Phạm vi backup gồm DB, Object Storage và DP key/certificate. Không xóa DP key đang được tài khoản khác sử dụng chỉ để xóa một tài khoản; dữ liệu cá nhân được purge khỏi các bản sao theo TTL.
- Hosting có sleep không bảo đảm worker chạy đúng lịch. Cơ chế vận hành phải chứng minh mốc 7 ngày/14 ngày trước khi mở chức năng xóa tài khoản; không thêm ping giữ gói miễn phí thức.

## 4. Tiêu chí kiểm thử khi hiện thực

- Đọc đơn ở T_kết_thúc+90 ngày bị chặn dù file chưa được worker dọn; nộp lại không kéo dài hạn của đơn cũ.
- Chặn ở tin A không chặn tin B, kể cả cùng recruiter. Hai thao tác chặn/nộp đồng thời có thứ tự transaction nhất quán; AI không là actor chặn.
- T0 khóa truy cập ngay ở file, job/result, cache và notification link; luồng xác minh để hủy chỉ thực hiện hủy yêu cầu.
- Hủy trước 7 ngày khôi phục tài khoản nhưng không tự công khai CV; sau mốc purge không hứa phục hồi từ backup.
- Purge kiểm tra file dùng chung, bản dẫn xuất và snapshot; lỗi object deletion không báo hoàn thành giả. Không xóa dữ liệu người khác do FK/cascade.
- Backup chứa dữ liệu đó hết hạn trong 14 ngày sau purge; restore cô lập áp lại sổ xóa trước mở quyền.

Theo dõi triển khai tại [task GĐ1](tasks/phase-01-foundation.md) và thiết kế đề xuất [database](../project-docs/04-database-design.md). Việc chốt chính sách không đồng nghĩa migration, tác vụ dọn hoặc tính năng đã hoạt động.
