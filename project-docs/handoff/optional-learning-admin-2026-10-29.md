# Kịch bản bổ sung — Nguồn học và quản trị

Ngày soạn: 29/10/2026. Dùng khi còn khoảng 10–15 phút sau ba luồng cốt lõi. Đây là kịch bản chuẩn bị, **không phải biên bản đã dùng thử hoặc nghiệm thu**. Chỉ dùng dữ liệu synthetic và bản demo Fake.

Bản chạy: <http://127.0.0.1:5190/>. Nếu chưa mở, tại thư mục dự án chạy `python scripts/handoff-demo.py start`. Tài khoản candidate, recruiter và admin nằm trong `.local/handoff-demo-20261029/accounts.private.md`; mở từng vai trò trong browser profile/cửa sổ riêng. Không gửi thông tin đăng nhập lên chat hoặc đưa vào báo cáo public.

## 1. Chuẩn bị trước khi gặp giảng viên

### Một bộ dữ liệu phụ, tách khỏi ba luồng chính

Recruiter tạo một tin với nội dung sau tại Công ty → Tin & JD:

| Trường | Nội dung synthetic để nhập |
| --- | --- |
| Chức danh | DEMO-PHU — React và Docker |
| Mô tả công việc | Tin tổng hợp dùng để kiểm tra gợi ý học tập và quy trình quản trị. Xây dựng giao diện React và đóng gói ứng dụng bằng Docker. |
| Yêu cầu và kỹ năng | Bắt buộc: React<br>Bắt buộc: Docker |

Bấm **Tạo tin nháp**, **Tách yêu cầu từ JD (local)**, rà lại hai yêu cầu, **Xác nhận yêu cầu JD**, rồi **Công khai**. Nếu công ty chưa có hồ sơ, tạo công ty synthetic ở mục Công ty trước. Không tạo lặp khi chưa rõ kết quả.

Candidate dùng một CV phụ chỉ có dữ kiện React, không khai Docker. Có thể tạo trong Soạn & Cải thiện CV:

| Trường | Nội dung synthetic để nhập |
| --- | --- |
| Tên hiển thị | Ứng viên demo phụ |
| Giới thiệu | Tôi xây dựng giao diện bằng React. Đây là dữ liệu tổng hợp cho buổi dùng thử. |
| Kỹ năng, mỗi dòng một mục | React |

Tạo PDF, lưu CV riêng và dùng bản có dữ liệu đã xác nhận. Không thêm Docker vào CV chỉ để đạt điểm. Nếu dùng CV có sẵn, kiểm dữ kiện thực có trong bản đó trước khi chọn tin phụ.

Mở Khám phá việc làm, chọn đúng CV, bấm **Tìm việc theo CV**. Tìm đúng dòng tin phụ trong kết quả từ khóa rồi bấm **Phân tích cặp CV–JD** của dòng đó. Có thể dùng **Gợi ý và phân tích tối đa 3 cặp** nếu tin phụ thực sự nằm trong nhóm được chọn. Bộ lọc Từ khóa của danh sách tin không được coi là đã thay tập chọn của AI. Nếu chưa thấy tin trong kết quả từ khóa, dừng và kiểm lại tin đã công khai/dữ liệu CV; không đổi thuật toán/topK hoặc ghi rằng đã phân tích một tin chưa được chọn. Trước buổi thử, xác minh thực tế có một tiêu chí chưa đủ bằng chứng và có chủ đề phù hợp để mở phần nguồn học. Nếu chưa có thì để mục này là chưa chuẩn bị, không tự sửa kết quả AI.

Chuẩn bị sẵn [báo cáo thực nghiệm](../report/stage-e-followup-2026-10-27.md) và [biên nhận đối soát Tavily](../report/stage-e-external-evidence-2026-10-28.md). Phần search thật đã dùng hết allowance; giữ bản demo Fake và không đổi key/provider để lấy thêm kết quả cho buổi thử.

## 2. Nguồn học — khoảng 4–5 phút

### Lời giới thiệu để nói với giảng viên

> “Em minh họa cách đi từ một yêu cầu còn thiếu bằng chứng sang chủ đề học tham khảo. Candidate tự yêu cầu tìm nguồn. Bản thao tác này dùng search mô phỏng; em trình bày riêng bằng chứng Tavily thật đã kiểm trước đó.”

### Thao tác

1. Đăng nhập candidate, mở **Khám phá việc làm**. Chọn đúng CV và mở kết quả đối chiếu đã chuẩn bị.
2. Bấm **Xem tiêu chí và bằng chứng**. Chọn tiêu chí thực sự hiển thị **Có thông tin một phần** hoặc **Chưa có bằng chứng**, ưu tiên Docker nếu kết quả đó có sẵn.
3. Bấm **Tìm nguồn học cho tiêu chí này**.
4. Nếu có danh mục, chọn **Chủ đề** phù hợp, **Ngôn ngữ tìm → Tiếng Việt** và **Trình độ → Cơ bản**. Bấm **Tìm tài liệu học** một lần.
5. Quan sát thông báo. Với Fake, giao diện ghi **“Search đang dùng mô phỏng local; chưa tìm kiếm web thật.”** Danh sách không có liên kết là phù hợp với bản mô phỏng hiện hành. Đừng trình bày thông báo Fake như lỗi search thật hoặc bằng chứng tài liệu thực tế tốt.
6. Kiểm vẫn đọc được điểm, tiêu chí và bằng chứng của CV–JD; kết quả đối chiếu không bị thay bằng thông báo search.
7. Nếu danh mục rỗng, ghi đúng **“Chưa có chủ đề tìm kiếm phù hợp trong danh mục hiện tại.”** Không chọn chủ đề không liên quan hoặc đổi dữ kiện CV/JD giữa buổi chỉ để có kết quả.

### Trình bày bằng chứng tích hợp thật đã có

- Đợt đánh giá đã chạy hai truy vấn Docker Việt/Anh, mỗi truy vấn ba liên kết; báo cáo ghi rõ đó là bằng chứng cũ, không phải lời gọi đang diễn ra trong demo.
- Tổng dashboard Tavily được owner đối soát ngày28/10 là **4/1.000 credits**, gồm D2 và E2, Pay-as-you-go tắt. Đây là bằng chứng tổng sử dụng, không phải chứng minh chất lượng từng liên kết.
- Nhãn “ngôn ngữ truy vấn” không bảo đảm ngôn ngữ toàn trang. Các kết quả truy vấn Việt có tiêu đề Anh; chất lượng nguồn Việt chưa được công nhận. Một số YouTube chưa đọc được bằng công cụ; không suy ra phải trả phí.
- Có thể mở metadata/kết quả thật còn được phép lưu tại máy để trao đổi nếu chuẩn bị được trước buổi thử. Chúng ở private và có retention riêng; không chép raw metadata/corpus vào source public hoặc tự chạy search mới khi không còn allowance.

### Câu hỏi xin góp ý

1. Từ tiêu chí thiếu bằng chứng sang chủ đề học như vậy có dễ hiểu và phù hợp không?
2. Thầy/cô có phân biệt được kết quả đối chiếu, truy vấn tìm nguồn và chế độ mô phỏng không?
3. Với kết quả thật được trình bày, còn cần kiểm gì về độ liên quan, uy tín, ngôn ngữ và khả năng truy cập nguồn?

**Chứng cứ cần ghi:** tiêu chí đã chọn, chủ đề/ngôn ngữ/trình độ, thông báo thực tế, kết quả đối chiếu còn xem được hay không và nhận xét. Không ghi “search thật đạt” từ lượt Fake này.

## 3. Quản trị — khoảng 6–8 phút

### Lời giới thiệu để nói với giảng viên

> “Em minh họa cách xử lý một báo cáo trên tin giả lập, ghi lý do và lưu nhật ký. Quản trị có phạm vi xử lý tài khoản/tin/báo cáo và xem usage; quyền này không mặc nhiên cho phép đọc CV riêng của mọi người.”

Làm phần nguồn học trước. Phần quản trị sau đó sẽ ẩn chính tin phụ, nên không dùng tin thuộc ba luồng chính.

### A. Candidate gửi báo cáo trên tin phụ

1. Candidate tìm tin **DEMO-PHU — React và Docker**.
2. Bấm **Báo cáo nội dung**.
3. Nhập **Lý do báo cáo**:

   > “Dữ liệu tổng hợp để dùng thử quy trình báo cáo và quản trị; không phản ánh vi phạm thực tế.”

4. Bấm **Gửi báo cáo**, chờ **“Đã gửi báo cáo tới quản trị viên.”** Hủy dialog phải giữ nguyên dữ liệu, không được coi là đã gửi.

### B. Admin xử lý báo cáo

1. Đăng nhập admin, mở **Quản trị** → **Báo cáo**.
2. Tìm báo cáo vừa tạo bằng nội dung/lý do và thời điểm. Danh sách có phân trang; không xử lý báo cáo khác chỉ vì nằm đầu trang.
3. Nhập **Lý do xử lý**:

   > “Đã rà tin synthetic trong buổi dùng thử; đánh dấu hoàn tất quy trình báo cáo.”

4. Bấm **Đã xử lý** của đúng báo cáo. Quan sát trạng thái và thông báo đã ghi nhật ký.
5. Giải thích rằng đánh dấu báo cáo đã xử lý và ẩn tin là hai thao tác riêng; không mặc định báo cáo đóng thì tin tự biến mất.

### C. Ẩn và khôi phục tin phụ

1. Admin mở **Tin tuyển dụng**, tìm đúng tin phụ. Nhập lý do **“Ẩn tin synthetic để kiểm chứng thao tác quản trị.”** rồi bấm **Ẩn tin**.
2. Candidate tìm lại/tải lại danh sách tin: tin phụ không còn xuất hiện công khai. Ghi nhận nếu giao diện vẫn hiển thị dữ liệu cũ sau tải lại.
3. Admin nhập lý do **“Kết thúc thử nghiệm, khôi phục tin về trạng thái đã đóng.”** rồi bấm **Khôi phục thành tin đã đóng**.
4. Quan sát tin trở về **Đã đóng**, không tự được công khai lại. Chỉ recruiter phụ trách chủ động công khai khi cần một lượt dùng thử tiếp theo.

### D. Nhật ký, tài khoản và hạn mức

1. Admin mở **Nhật ký**: đối chiếu các thao tác vừa thực hiện, người xử lý, lý do và thời điểm hiển thị. Ghi nhận đúng những dòng thực sự nhìn thấy.
2. Mở **Tài khoản**; thử **Tìm email tài khoản** bằng email synthetic. Khi xong, xóa từ khóa và bấm **Tìm tài khoản** trước khi chuyển tab, để bộ lọc không làm danh sách khác trông như rỗng.
3. Phần này chỉ cần quan sát quản lý tài khoản. Nếu muốn kiểm tạm khóa/mở lại, chuẩn bị trước một candidate phụ không tham gia ba luồng chính, ghi lý do, kiểm trạng thái rồi mở lại. Không dùng admin hoặc tài khoản giảng viên đang thao tác làm đối tượng thử khóa.
4. Mở **Hạn mức và lỗi**, xem các nhóm **AI — đơn vị từng sổ**, **Search credit**, **Email**, **Lỗi AI**. Trong database demo, Fake/test_credit là đơn vị thử nghiệm, không quy đổi thành USD/credits tài khoản Tavily. Nhóm chưa có dữ liệu có thể rỗng.
5. Giải thích chỉ số email “provider nhận yêu cầu” không tự chứng minh thư đã tới Inbox; sổ ứng dụng không bao gồm sử dụng ngoài ứng dụng. Không bấm gửi email/search thật để làm đầy bảng.

### Câu hỏi xin góp ý

1. Quy trình báo cáo → xử lý → ẩn tin → khôi phục có rõ trách nhiệm và trạng thái không?
2. Lý do, người thực hiện và nhật ký có đủ để kiểm tra lại thao tác không?
3. Các đơn vị usage và phần giữ chỗ/chờ đối soát có dễ hiểu không?

**Chứng cứ cần ghi:** tin/báo cáo synthetic đã chọn, hành động thực tế, trạng thái trước/sau, thông báo, nhật ký nhìn thấy và nhận xét. Việc không có nút đọc CV riêng trong UI không tự chứng minh toàn bộ ACL backend; dùng bằng chứng kiểm thử quyền đã có khi giải thích thêm.

## 4. Phiếu ghi nhanh để điền sau khi thao tác

Người đánh giá: ____________________  Vai trò: ____________________

Ngày/giờ thực tế: ____________________  Bản được xem/commit: ____________________

Hình thức: trực tiếp thao tác / quan sát chủ dự án thao tác qua màn hình / hình thức khác: ____________________

| Mục | Kết quả thực tế: đạt/chưa đạt/chưa thử | Lỗi hoặc nhận xét |
| --- | --- | --- |
| Chọn tiêu chí còn thiếu và chủ đề học phù hợp | | |
| Ngôn ngữ/trình độ và thông báo đúng chế độ Fake | | |
| Vẫn xem được kết quả đối chiếu khi search rỗng | | |
| Báo cáo tin synthetic và admin xử lý đúng báo cáo | | |
| Ẩn tin, kiểm public và khôi phục thành tin đã đóng | | |
| Nhật ký có người xử lý, lý do và thời điểm | | |
| Phân biệt test_credit/credit/USD và email accepted/Inbox | | |

Mức trợ giúp cần dùng: ____________________

Góp ý ưu tiên: ____________________

Nhận xét về bằng chứng nguồn học thật đã trình bày, nếu có: ____________________

Người đánh giá xác nhận những mục mình thực sự đã thử/xem theo hình thức ghi ở trên. Phiếu này không tự đóng M6 hoặc nghiệm thu chất lượng đại diện. Các mục chưa thử giữ nguyên trạng thái đó.
