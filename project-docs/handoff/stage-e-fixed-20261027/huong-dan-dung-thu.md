# Hướng dẫn dùng thử CareerGap AI

Bộ sau sửa chuẩn bị ngày 27/10/2026 cho chủ dự án và giảng viên/người đánh giá. **Chưa có buổi dùng thử thực tế hoặc kết quả điền sẵn.** Toàn bộ buổi này dùng dữ liệu synthetic và AI/search/email Fake, không gửi dữ liệu cho nhà cung cấp và không cần key thật.

## 1. Bật bản thử trên máy hiện tại

1. Bật Docker Desktop, chờ engine hoạt động.
2. Mở PowerShell tại `D:\CareerGapAI`, chạy:

   ```powershell
   python .local/stage-e-followup-20261027/trial-runtime.py start
   ```

3. Chờ dòng `Trial ready`, mở `http://127.0.0.1:5183/` trên chính máy này. Cổng này khác ứng dụng cũ ở 5173. Nếu cổng bận hoặc không có dòng ready, đọc `.local/stage-e-followup-20261027/trial-logs/`; không bật provider thật để chữa lỗi demo.
4. Mở riêng file `D:\CareerGapAI\.local\stage-e-followup-20261027\trial-accounts.private.md`. File có tài khoản **candidate** và **recruiter**, đều synthetic. Không đưa file này vào báo cáo public. Có thể dùng hai cửa sổ trình duyệt riêng để giữ hai phiên.
5. Dùng CV `D:\CareerGapAI\project-docs\handoff\stage-e-fixed-20261027\CV-synthetic-dung-thu.docx` và JD có tên **SYNTHETIC E FIXED - Web developer**. Các tên/tài khoản này không thuộc corpus đánh giá thật.
6. Mở [phiếu dùng thử](Phieu-dung-thu-CareerGapAI.docx), điền thông tin buổi thử và phiên bản. Không đánh dấu đạt trước khi thao tác. Dành khoảng 30–45 phút là thời lượng đề xuất, chưa phải thời gian đã quan sát.

Đây là môi trường đã dựng sạch trên máy hiện tại, không phải bộ cài độc lập cho máy khác. Với máy khác, làm theo README gốc, dùng Fake mặc định và tạo DB/tài khoản demo riêng. Không sao chép nguyên `.local` vì chứa corpus, credentials và chứng cứ private.

## 2. Chuẩn bị JD bằng vai trò recruiter

1. Đăng nhập bằng recruiter private, tìm mục **Công ty và tin tuyển dụng**.
2. Mở JD synthetic đã chuẩn bị. Chọn **Tách yêu cầu từ JD (local)**.
3. Đọc từng yêu cầu, chọn đúng phân loại và **Xác nhận yêu cầu JD**. Ghi lại nếu parser hiểu khác văn bản. Đây là thao tác người đánh giá, không phải nhãn benchmark.
4. Tin đã công khai để candidate nộp. Nếu thử sửa JD, giữ ghi chú version trước/sau; khi xem đơn sau này đối chiếu snapshot lúc nộp.

## 3. U1 ứng viên đọc và xác nhận CV

1. Đăng nhập candidate ở cửa sổ riêng. Tại **CV riêng của bạn**, chọn file CV synthetic qua **Thêm vào CV**, đặt tên dễ nhận biết và bấm **Tạo CV riêng tư** một lần.
2. Bấm **Đọc đầy đủ v1** (hoặc số version tương ứng) của phiên bản vừa tải, chờ trạng thái kết thúc. Màn hình phải ghi chế độ mô phỏng/Fake. Nếu lỗi, ghi nguyên thông báo vào phiếu trước khi xử lý; không báo đạt chỉ vì đã bấm nút.
3. Tại **Rà soát toàn bộ dữ liệu CV**, đọc nguồn và dữ liệu. Sửa thông tin thực sự sai, không thêm thành tích ngoài nguồn. Bấm **Xác nhận toàn bộ dữ liệu CV**, chờ thông báo lưu bản xác nhận.
4. Kiểm tra CV vẫn riêng tư. Chưa công khai file chỉ vì đã xác nhận dữ liệu. Ghi U1, thời gian và số lần cần trợ giúp.

## 4. U2 gợi ý và nộp hồ sơ

1. Chọn đúng CV/version đã xác nhận, dùng **Tìm việc theo CV** để xem retrieval. Đọc nhãn retrieval score và sự khác nhau với điểm đối chiếu AI.
2. Tìm JD theo tên **SYNTHETIC E FIXED - Web developer**. Khi cả CV và JD đã xác nhận, mở đối chiếu nếu nút có sẵn. Đọc điểm, trạng thái và bằng chứng; ghi điều khó hiểu, không xem score là quyết định tuyển dụng.
3. Ở điều khiển public của CV, chỉ công khai đúng version synthetic khi cần mở luồng nộp đơn. Chọn đúng version và **Nộp hồ sơ** một lần.
4. Chờ thông báo thành công và xem đơn trong danh sách ứng tuyển. Ghi mã/version dùng trong phiếu; không sửa CV trước khi đã ghi nhận snapshot.

## 5. U3 recruiter xem đơn và bằng chứng

1. Quay sang cửa sổ recruiter, tải lại danh sách đơn của đúng JD.
2. Mở đơn vừa nộp, tải file qua ngữ cảnh đơn và so với version candidate đã chọn.
3. Nếu chạy đối chiếu lại, dùng JD snapshot lúc nộp theo quyết định đã chốt. Sau khi thử sửa JD hiện tại, kiểm đơn cũ vẫn giữ snapshot ban đầu.
4. Thử ghi chú/trạng thái sàng lọc thủ công. Ghi rõ ai đã thay đổi; AI không tự quyết định. Nếu lỗi quyền hoặc khác snapshot, đánh dấu chưa đạt và dừng thao tác liên quan.

## 6. U4 duyệt đề xuất và tạo phiên bản CV

1. Trở lại candidate. Trong builder chọn đúng CV/version đã xác nhận rồi **Nạp dữ liệu CV**. Chưa sửa bản nháp trước khi lấy đề xuất.
2. Chọn ngôn ngữ, **Tạo đề xuất soạn CV**. Đọc nhãn Fake và so dữ kiện với nguồn. Có thể chọn **Bỏ đề xuất** để kiểm CV giữ nguyên; muốn thử accept thì tạo đề xuất mới trên dữ liệu synthetic.
3. Chỉ chọn **Chấp nhận và đưa vào bản nháp** nếu đồng ý. Sau khi accept, không nạp lại nguồn hoặc reload làm mất bản nháp.
4. **Tạo bản xem trước PDF**, kiểm dữ kiện, dấu, bố cục và chọn/copy text. Nếu đạt thì **Lưu thành phiên bản CV** một lần.
5. Kiểm version mới xuất hiện, bản cũ giữ nguyên và lựa chọn public không tự đổi. Lặp ngôn ngữ còn lại nếu muốn, ghi riêng quan sát; simple-v1 có tiêu đề mục tiếng Việt kể cả nội dung Anh.

## 7. U5 khi AI không sẵn sàng

1. Ghi hoặc lưu xong bản nháp trước. Chủ dự án chạy:

   ```powershell
   python .local/stage-e-followup-20261027/trial-runtime.py fault
   ```

2. Tải lại trang và thử một yêu cầu AI mới. API phải từ chối khi AI tắt; giao diện báo rõ AI đang tắt, yêu cầu mới chưa được tạo và vẫn có thể xem/tải CV, thao tác thủ công. Ghi thông báo thật. Kiểm vẫn đăng nhập, đọc CV/JD và thao tác thủ công được. Không coi mode này là test mất mạng/timeout/provider HTTP thật.
3. Khôi phục bằng lệnh `start` ở bước 1. Không dùng key thật hoặc tăng quota. Nếu job từng được nhận, kiểm trạng thái trước khi tạo yêu cầu khác.

## 8. Kết thúc

1. Điền nhận xét trong phiếu, giữ các mục chưa thử. Người đánh giá tự xác nhận phần đã thực hiện; chủ dự án ghi việc cần sửa. Không gửi CV thật, mật khẩu hoặc token trong phản hồi.
2. Đưa đường dẫn phiếu đã điền hoặc biên bản private cho Codex để đối chiếu và xử lý tồn đọng; đây mới là đầu vào dùng thử thực tế.
3. Dừng dịch vụ thử:

   ```powershell
   python .local/stage-e-followup-20261027/trial-runtime.py stop
   docker stop careergap-step6-eval-20261027
   ```

4. Nếu không chạy được demo, mở [video dự phòng](demo.html). Banner luôn ghi **tự động / synthetic / Fake**; video không thay kết quả dùng thử giảng viên.

Bản sửa đã chạy đầy đủ 2 CV/8 cặp trên tập đã xem. M6 còn mở vì chưa có dùng thử/nhận xét giảng viên thật; F1 và rerank có giới hạn đã báo cáo, không coi lượt sau sửa là final độc lập mới. [Báo cáo E](../../report/stage-e-followup-2026-10-27.md) ghi chi tiết, gồm kết quả âm và giới hạn mẫu.
