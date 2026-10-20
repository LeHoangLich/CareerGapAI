# CareerGap AI – Hệ Thống Tuyển Dụng Và Quản Lý Ứng Viên Tích Hợp AI

> Tài liệu đặc tả ý tưởng, tầm nhìn và phạm vi chức năng của đồ án chuyên ngành CareerGap AI. Nội dung dựa trên các yêu cầu đã thống nhất trong quá trình làm rõ dự án. Những quy tắc được đánh dấu **đề xuất** hoặc **chưa chốt** cần được phân biệt với yêu cầu đã xác nhận.

## Ý tưởng dự án

**Quyết định phát triển ngày 15/10/2026:** dừng deploy, ưu tiên hoàn thiện sản phẩm và kiểm thử trên localhost. Phạm vi chức năng giữ nguyên; cloud là bước sau khi sản phẩm hoàn thiện local và người dùng yêu cầu mở lại deploy. Không lấy việc thiếu hosting/domain làm điều kiện chặn phát triển local.

CareerGap AI là nền tảng web hỗ trợ tuyển dụng, tìm việc và quản lý ứng viên đến bước sàng lọc. Hệ thống phục vụ cả ứng viên và nhà tuyển dụng thuộc nhiều ngành, ưu tiên ngành Công nghệ thông tin (CNTT). Dữ liệu và hoạt động đánh giá ban đầu tập trung vào CNTT; các chức năng AI không bị giới hạn quyền sử dụng theo ngành.

Hệ thống tích hợp mô hình ngôn ngữ lớn có sẵn để đọc CV và mô tả công việc (JD), tìm các hồ sơ hoặc vị trí tuyển dụng phù hợp, đánh giá mức độ phù hợp trên thang điểm 100 và giải thích kết quả dựa trên nội dung hồ sơ. AI chỉ phân tích, gợi ý và xếp hạng; nhà tuyển dụng quyết định kết quả sàng lọc.

Điểm trọng tâm của dự án là khoảng cách giữa thông tin năng lực được thể hiện trong CV và yêu cầu của một vị trí cụ thể. Ứng viên có thể hiểu các điểm đáp ứng, nhận biết phần còn thiếu trong hồ sơ, tìm tài liệu học tham khảo và cải thiện CV bằng thông tin thực tế của mình.

### Bài toán cần giải quyết

- **Đối với ứng viên:** tìm vị trí phù hợp với CV; hiểu lý do phù hợp hoặc chưa phù hợp; viết và trình bày CV; xác định kỹ năng cần bổ sung so với JD.
- **Đối với nhà tuyển dụng:** tìm ứng viên trong kho CV công khai theo JD; sàng lọc hồ sơ đã ứng tuyển; xem bằng chứng phục vụ đánh giá; quản lý trạng thái và ghi chú sàng lọc.
- **Đối với người thực hiện:** tự xây dựng một hệ thống có luồng nghiệp vụ hoàn chỉnh, triển khai được để trải nghiệm và có thực nghiệm đánh giá chất lượng các chức năng AI.

### Bối cảnh và giới hạn nguồn lực

| Nội dung | Phạm vi đã xác nhận |
| --- | --- |
| Loại dự án | Đồ án chuyên ngành, có triển khai thực tế để demo và đánh giá thử |
| Nhân sự và thời gian | 1 người tự thực hiện; thời gian mục tiêu 3 tháng, cần rà khối lượng theo số giờ thực tế |
| Người dùng mục tiêu | Mọi đối tượng tìm việc và tuyển dụng; ưu tiên ngành CNTT |
| Mức độ ưu tiên | Ứng viên và nhà tuyển dụng đều là trọng tâm |
| Frontend | React, TypeScript, Vite |
| Backend | .NET 10, kiến trúc Monolith |
| Cơ sở dữ liệu | PostgreSQL 16 trở lên |
| AI | Tích hợp LLM Cloud của DeepSeek; không yêu cầu tự huấn luyện mô hình nền tảng |
| Dữ liệu ban đầu | Một lượng nhỏ CV/JD mẫu, chủ yếu thuộc ngành CNTT |
| Ngôn ngữ đầu vào | CV và JD tiếng Việt, tiếng Anh |
| Định dạng CV | PDF có văn bản và DOCX; OCR dành cho bản scan/ảnh là phần mở rộng |
| Triển khai dự kiến | Vercel, Render, Neon; đây là phương án của người thực hiện, không phải yêu cầu bắt buộc của trường |
| Ngân sách vận hành | Mục tiêu không vượt khoảng 4 USD/tháng trong giai đoạn phát triển và demo |
| Người hỗ trợ đánh giá | Giảng viên hỗ trợ đánh giá thử |

Ngân sách được xem là ràng buộc thiết kế: các chức năng AI và tìm kiếm web phải có giới hạn sử dụng, theo dõi chi phí và xử lý khi hết hạn mức. Khả năng đáp ứng số người dùng cụ thể cần được đo trong quá trình triển khai.

### Thuật ngữ và dữ liệu chính

| Thuật ngữ | Ý nghĩa trong hệ thống |
| --- | --- |
| Profile ứng viên | Hồ sơ trên hệ thống, gồm các trường thông tin ứng viên quản lý và lựa chọn cho nhà tuyển dụng xem |
| CV | Tài liệu ứng viên tải lên hoặc tạo từ mẫu; một ứng viên có thể quản lý nhiều CV |
| CV công khai | CV chính được ứng viên chọn để hiển thị cho ứng viên và nhà tuyển dụng; mỗi ứng viên có tối đa một CV công khai tại một thời điểm |
| CV ứng tuyển | Phiên bản CV được ứng viên gửi cho một tin tuyển dụng cụ thể; không bắt buộc phải là CV công khai |
| JD | Nội dung mô tả công việc và yêu cầu tuyển dụng gắn với một vị trí |
| Điểm phù hợp | Điểm từ 0 đến 100 thể hiện mức phù hợp của một CV với một JD theo tiêu chí đánh giá của hệ thống |
| Kho CV công khai | Tập CV đủ điều kiện hiển thị để nhà tuyển dụng tìm và xếp hạng theo JD |

Quyền công khai profile và quyền công khai CV là hai khái niệm riêng. Quyền xem một CV cũng không đồng nghĩa với quyền sử dụng chức năng tìm kiếm và xếp hạng ứng viên của nhà tuyển dụng.

## Tầm nhìn dự án

CareerGap AI hướng đến một trải nghiệm tuyển dụng trong đó cả hai bên hiểu được cơ sở của kết quả gợi ý. Nhà tuyển dụng có thêm thông tin để sàng lọc, còn ứng viên có hướng cải thiện hồ sơ và kỹ năng theo công việc mong muốn.

### Mục tiêu của bản đồ án

1. Hoàn thành hai luồng chính: ứng viên tìm việc và ứng tuyển; nhà tuyển dụng đăng tin, tìm người và sàng lọc hồ sơ.
2. Đọc CV/JD tiếng Việt và tiếng Anh thành dữ liệu có cấu trúc, cho phép người dùng kiểm tra và sửa thông tin trích xuất.
3. Gợi ý vị trí theo CV và tìm ứng viên theo JD, kèm điểm phù hợp và bằng chứng giải thích.
4. Hỗ trợ tạo CV từ thông tin thật và tìm nguồn học tham khảo cho các kỹ năng còn thiếu so với JD.
5. Triển khai được bản demo trong giới hạn nguồn lực, đồng thời có báo cáo thực nghiệm về chất lượng AI và khả năng sử dụng.

### Nguyên tắc định hướng

- **Con người quyết định:** AI không tự động từ chối hồ sơ, cho qua sàng lọc hoặc thay đổi trạng thái ứng tuyển.
- **Giải thích có cơ sở:** các nhận định phải liên hệ được với nội dung CV, JD hoặc thông tin do người dùng xác nhận. Không tự tạo kinh nghiệm, chứng chỉ hoặc thành tích.
- **Phân biệt thiếu thông tin và thiếu năng lực:** CV không đề cập một kỹ năng chỉ cho thấy hồ sơ chưa thể hiện kỹ năng đó; chưa đủ để kết luận ứng viên không có kỹ năng.
- **Điểm số có giới hạn rõ ràng:** điểm phù hợp gắn với một cặp CV–JD, không phải điểm năng lực tổng thể hay xác suất được tuyển dụng.
- **Quyền hiển thị do ứng viên kiểm soát:** ứng viên chọn trường profile chia sẻ, chọn CV công khai và có thể thay đổi lựa chọn.
- **Đánh giá trong phạm vi dữ liệu:** hệ thống phục vụ nhiều ngành, nhưng kết luận thực nghiệm ban đầu tập trung vào CNTT do giới hạn dữ liệu.
- **Phạm vi phù hợp thời gian:** phần quản lý tuyển dụng dừng ở sàng lọc; các chức năng phụ không làm chậm việc hoàn thành luồng chính và thực nghiệm.

### Định hướng đánh giá kết quả

Các chỉ số dưới đây là định hướng thực nghiệm; ngưỡng đạt cụ thể sẽ được thống nhất với giảng viên sau khi xác định bộ dữ liệu và quy tắc gán nhãn.

| Mục tiêu | Cách đánh giá dự kiến |
| --- | --- |
| Trích xuất CV chính xác | So sánh kỹ năng, kinh nghiệm, dự án, học vấn và các trường thông tin với dữ liệu gán nhãn thủ công; đánh giá riêng theo định dạng và ngôn ngữ |
| Gợi ý sát nhu cầu | Đánh giá mức liên quan của các kết quả đầu tiên cho cả chiều CV tìm JD và JD tìm CV, đối chiếu với nhận xét của giảng viên |
| Điểm phù hợp có cơ sở | Đối chiếu kết quả theo tiêu chí với đánh giá thủ công; kiểm tra sự nhất quán khi phân tích lại cùng dữ liệu |
| Giải thích đúng | Kiểm tra nhận định có được nội dung CV/JD hỗ trợ hay không; thống kê nhận định sai hoặc không có bằng chứng |
| Tài liệu học hữu ích | Kiểm tra đường dẫn, nguồn, mức liên quan với kỹ năng và độ phù hợp với trình độ người học |
| Quản lý tuyển dụng thuận tiện | Cho người đánh giá thực hiện các tác vụ đăng tin, ứng tuyển, xem hồ sơ và cập nhật kết quả sàng lọc |
| Vận hành trong giới hạn | Đo thời gian xử lý, số lần gọi dịch vụ và chi phí thực tế cho các tác vụ AI |

Hướng nghiên cứu đề xuất là so sánh phương pháp đối sánh từ khóa cơ bản với phương pháp sử dụng dữ liệu có cấu trúc và DeepSeek hỗ trợ phân tích. Hai phương pháp cần được đánh giá trên cùng tập dữ liệu. Dữ liệu dùng để điều chỉnh phương pháp và dữ liệu đánh giá cần được tách biệt; các biến thể của cùng một CV phải được giữ chung nhóm để hạn chế đánh giá sai lệch.

## Các tính năng cốt lõi

### 1. Tài khoản và phân quyền

- Có ba vai trò nghiệp vụ: ứng viên, nhà tuyển dụng và quản trị viên.
- Đã xác nhận: đăng ký chọn **một** vai trò Candidate hoặc Recruiter; không có tự đăng ký Admin hoặc tự chuyển/thêm vai trò trong bản đầu. Tài khoản quản trị được cấp qua quy trình có kiểm soát.
- Các chức năng tài khoản cơ bản gồm đăng ký, đăng nhập, đăng xuất, đổi mật khẩu và khôi phục mật khẩu.
- Ứng viên quản lý hồ sơ, CV và các đơn ứng tuyển của mình.
- Nhà tuyển dụng quản lý thông tin tuyển dụng thuộc phạm vi của mình, tìm CV công khai và sàng lọc hồ sơ gửi đến tin tuyển dụng mình phụ trách.
- Nhà tuyển dụng không cần được quản trị viên phê duyệt trước khi truy cập kho CV công khai.
- Quản trị viên quản lý tài khoản, tin tuyển dụng và xử lý báo cáo nội dung; **không có quyền mặc định đọc CV riêng tư**. Không thêm đường đọc hỗ trợ/kiểm duyệt vượt quyền này khi chưa có yêu cầu riêng được xác nhận.
- Quyền truy cập phải được kiểm tra khi đọc dữ liệu hoặc tải tài liệu, không chỉ bằng việc ẩn nút trên giao diện.

### 2. Hồ sơ ứng viên và quyền công khai

Ứng viên quản lý các nhóm thông tin như giới thiệu bản thân, thông tin liên hệ, kỹ năng, kinh nghiệm, dự án, học vấn, chứng chỉ và mong muốn công việc. Các trường cụ thể sẽ được xác định khi thiết kế dữ liệu và giao diện.

Ứng viên có thể lựa chọn những thông tin profile cho nhà tuyển dụng xem và chọn một CV chính để công khai. Chỉ nhà tuyển dụng được xem các trường profile mà ứng viên khác đã cho phép hiển thị; ứng viên vẫn có quyền xem và chỉnh sửa hồ sơ của chính mình.

| Tài nguyên hoặc chức năng | Ứng viên khác | Nhà tuyển dụng |
| --- | --- | --- |
| Trường profile được chủ hồ sơ cho phép hiển thị | Không được xem | Được xem |
| Trường profile không được chia sẻ | Không được xem | Không được xem qua kho hồ sơ công khai |
| CV được chọn công khai | Chỉ được xem bản dẫn xuất đã ẩn thông tin liên hệ | Được xem, bao gồm thông tin liên hệ trong CV |
| CV riêng tư, chưa gửi ứng tuyển | Không được xem | Không được xem |
| CV đã gửi ứng tuyển | Không được xem | Chỉ bên tuyển dụng phụ trách tin nhận hồ sơ được xem |
| JD công khai | Được xem | Được xem |
| Tìm và xếp hạng ứng viên theo JD bằng AI | Không sử dụng | Được sử dụng |

Các quy tắc bổ sung được đề xuất:

- CV mới tải lên ở trạng thái riêng tư cho đến khi ứng viên chủ động công khai. Gửi ứng tuyển chỉ cấp quyền xem cho bên tuyển dụng nhận đơn, không tự công khai CV.
- Khi ẩn hoặc thay CV công khai, CV cũ không còn xuất hiện trong kho tìm kiếm và các kết quả gợi ý mới; quyền mở lại kết quả cũ cũng phải được kiểm tra theo trạng thái hiện tại.
- Việc ẩn CV công khai không tự động thu hồi phiên bản đã chủ động gửi ứng tuyển. Recruiter phụ trách tin vẫn đọc được snapshot của đơn đã rút/bị loại tối đa 90 ngày từ khi đơn kết thúc, trừ khi quyền bị thu hồi sớm do yêu cầu xóa tài khoản.
- Đã xác nhận: khách chưa đăng nhập chỉ xem JD công khai; mọi đường đọc CV yêu cầu đăng nhập và đúng ngữ cảnh/quyền.

**Đã chốt:** ứng viên xem CV công khai của người khác chỉ nhận bản đã ẩn liên hệ; recruiter vẫn nhận bản có liên hệ theo quyền. Việc ẩn trường profile hoặc phủ hình lên file không đủ. Thiết kế bản dẫn xuất phải loại email, số điện thoại và liên kết/thành phần chứa liên hệ khỏi nội dung được trả về, kể cả text có thể copy, link, metadata hoặc QR chứa liên hệ. Ưu tiên dựng bản hiển thị từ dữ liệu cho phép thay vì tuyên bố đã làm sạch mọi file tùy ý. Khi chưa có bản dẫn xuất được xác minh, báo bản xem này chưa sẵn sàng; tuyệt đối không fallback file gốc cho ứng viên khác. File gốc/snapshot ứng tuyển không bị sửa để thực hiện việc ẩn liên hệ.

### 3. Hồ sơ công ty và tin tuyển dụng

- Nhà tuyển dụng tạo và cập nhật hồ sơ giới thiệu công ty.
- Đã xác nhận: mỗi hồ sơ công ty có một recruiter owner; bản đầu không có mời nhiều recruiter cùng công ty hoặc chuyển owner. Số lượng công ty một recruiter được tạo chưa bị giới hạn thêm bởi quyết định này.
- Tạo, chỉnh sửa, công khai và đóng tin tuyển dụng.
- Tin tuyển dụng thể hiện mô tả công việc, yêu cầu, kỹ năng, kinh nghiệm, địa điểm, hình thức làm việc, quyền lợi, thông tin lương nếu có và hạn nhận hồ sơ.
- Phân biệt yêu cầu bắt buộc và yêu cầu ưu tiên để phục vụ tìm kiếm và đánh giá CV–JD.
- Ứng viên và nhà tuyển dụng được xem JD đã công khai.
- Đề xuất các trạng thái tối thiểu của tin: bản nháp, đang tuyển và đã đóng/hết hạn. Tin nháp không xuất hiện trong kết quả tìm kiếm; tin đã đóng không nhận đơn mới hoặc được gợi ý như một vị trí còn tuyển.

### 4. Quản lý, đọc và tạo CV

#### 4.1. Tải lên và trích xuất CV

- Nhận file PDF có lớp văn bản và DOCX; OCR đối với PDF scan hoặc ảnh nằm ngoài phạm vi chính.
- Hỗ trợ CV tiếng Việt và tiếng Anh.
- Trích xuất thông tin liên hệ, kỹ năng, kinh nghiệm, dự án, học vấn và chứng chỉ khi có trong tài liệu.
- Hiển thị dữ liệu trích xuất để ứng viên kiểm tra, sửa và xác nhận.
- Liên kết các thông tin phân tích với nội dung nguồn khi có thể để phục vụ giải thích và đánh giá độ đúng.
- Nếu không đọc được file hoặc văn bản không đủ để phân tích, thông báo rõ và cho phép tải lại hoặc nhập bổ sung; không tạo dữ liệu giả để hoàn thành kết quả.

#### 4.2. Quản lý nhiều CV

- Tạo, đặt tên, xem và quản lý nhiều CV theo nhu cầu ứng tuyển. Chỉnh sửa nội dung CV được tạo từ mẫu; với CV tải lên, thay file hoặc sửa dữ liệu trích xuất, không yêu cầu trình biên tập trực tiếp file PDF/DOCX.
- Chọn tối đa một CV chính để công khai; các CV còn lại không tự động được công khai.
- Chọn CV cụ thể khi phân tích một JD hoặc gửi đơn ứng tuyển.
- Khi nội dung CV thay đổi, các kết quả phân tích cũ phải gắn với phiên bản đã dùng hoặc được đánh dấu cần cập nhật.

#### 4.3. Tạo CV với AI hỗ trợ nội dung

- Tạo CV từ thông tin người dùng cung cấp và lựa chọn trong một số mẫu phổ biến.
- AI hỗ trợ viết phần giới thiệu, diễn đạt kinh nghiệm, mô tả dự án và kỹ năng bằng tiếng Việt hoặc tiếng Anh.
- AI chỉ sử dụng thông tin thật đã được người dùng cung cấp; nếu thiếu dữ liệu cần yêu cầu bổ sung hoặc để trống.
- Người dùng xem trước, sửa và chấp nhận nội dung trước khi lưu hoặc xuất bản.
- Xác nhận chủ dự án ngày 16/10/2026 trong giai đoạn B: chốt **một mẫu một cột `simple-v1` cho GĐ3**, hỗ trợ xuất PDF; thêm mẫu sau. Đề xuất 2–3 mẫu trước đó không còn là điều kiện chặn GĐ3. AI hỗ trợ nội dung ở giai đoạn sau; thiết kế mẫu do người thực hiện kiểm soát.

### 5. Tìm việc, lưu việc và ứng tuyển

- Tìm kiếm và lọc tin tuyển dụng theo từ khóa, lĩnh vực, kỹ năng, địa điểm, kinh nghiệm và hình thức làm việc; các bộ lọc chỉ áp dụng khi tin có dữ liệu tương ứng.
- Xem chi tiết JD và thông tin công ty.
- Lưu tin tuyển dụng quan tâm.
- Chọn CV để ứng tuyển vào tin đang nhận hồ sơ, kể cả CV đang để riêng tư.
- Xem danh sách đơn đã gửi và trạng thái sàng lọc; nhận thông báo trong hệ thống khi trạng thái thay đổi.
- Đã xác nhận hỗ trợ rút đơn và nộp lại sau khi rút hoặc bị loại, trừ khi ứng viên bị recruiter chặn. Vẫn ngăn gửi trùng khi có đơn còn hiệu lực cho cùng một tin; chặn nộp lại là quyền do recruiter chủ động thực hiện, AI không tự chặn.
- Mỗi lần nộp lại tạo đơn mới với snapshot và lịch sử riêng; không mở lại hoặc sửa đơn cũ. Cùng idempotency key vẫn trả đúng lần nộp trước; lần nộp mới dùng key mới. **Chặn chỉ áp dụng cho tin hiện tại**, do recruiter phụ trách tin đặt/bỏ chặn; không mở rộng sang tin khác hoặc toàn hệ thống.
- Recruiter phụ trách tin vẫn được đọc snapshot của đơn đã rút/bị loại trong **90 ngày từ thời điểm đơn kết thúc**, trừ khi chủ tài khoản yêu cầu xóa và quyền này bị thu hồi sớm hơn. Ứng viên nhìn thấy rõ rút đơn không xóa/thu hồi ngay hồ sơ đã gửi. Yêu cầu xóa tài khoản thu hồi truy cập ngay, có **7 ngày để hủy yêu cầu**, sau đó xóa/ẩn danh dữ liệu chính; backup hết hạn trong tối đa **14 ngày tiếp theo**. Chi tiết và các trường hợp dùng chung file theo chính sách liên kết cuối tài liệu.
- Đề xuất lưu một phiên bản CV tại thời điểm ứng tuyển để việc sửa CV sau đó không làm thay đổi hồ sơ nhà tuyển dụng đã nhận.

### 6. Quản lý ứng viên đến bước sàng lọc

- Xem danh sách ứng viên đã nộp hồ sơ theo từng tin tuyển dụng.
- Tìm kiếm, lọc, xem CV và kết quả AI phân tích độ phù hợp với JD của tin đó.
- Thêm ghi chú nội bộ và đưa ứng viên vào danh sách phù hợp để tiếp tục xem xét.
- Nhà tuyển dụng chủ động cập nhật kết quả sàng lọc; AI không tự đổi trạng thái.
- Nhà tuyển dụng phụ trách tin có thể chặn/bỏ chặn ứng viên nộp vào chính tin đó; việc chặn không tự sửa/xóa hồ sơ hoặc lịch sử đã nhận, không ngăn nộp tin khác và không do AI tự quyết định.
- Ghi chú nội bộ không hiển thị cho ứng viên hoặc nhà tuyển dụng ngoài phạm vi phụ trách.

**Ma trận đã được người dùng xác nhận ngày 16/10/2026** (recruiter owner của tin):

| Trạng thái hiện tại | Chuyển tiếp recruiter được phép |
|---|---|
| Đã nộp (`submitted`) | Đang xem xét (`reviewing`), Qua sàng lọc (`shortlisted`), Không phù hợp (`rejected`) |
| Đang xem xét (`reviewing`) | Qua sàng lọc (`shortlisted`), Không phù hợp (`rejected`) |
| Qua sàng lọc (`shortlisted`) | Đang xem xét (`reviewing`), Không phù hợp (`rejected`) |

Candidate có thể rút mọi đơn còn hiệu lực (`submitted`, `reviewing`, `shortlisted`) sang `withdrawn`. `rejected` và `withdrawn` là trạng thái kết thúc của đơn cũ; không mở lại đơn. Có thể nộp lại bằng application/snapshot/key mới nếu không có đơn còn hiệu lực và không bị chặn ở tin đó. “Qua sàng lọc” không có nghĩa là đã được tuyển. Lịch phỏng vấn, quản lý offer, hợp đồng và nhân sự sau tuyển dụng không nằm trong phạm vi bản đồ án.

### 7. AI đối sánh CV–JD và gợi ý hai chiều

#### 7.1. Nhà tuyển dụng tìm ứng viên theo JD

- Chọn JD của vị trí cần tuyển để tìm trong toàn bộ kho CV đang công khai, không chỉ trong danh sách đã ứng tuyển.
- Trả về danh sách được xếp hạng theo mức phù hợp, kèm điểm và giải thích cho từng CV.
- Cho phép xem CV, thông tin liên hệ được công khai và những trường profile chủ hồ sơ cho phép hiển thị.
- Hỗ trợ phân tích, xếp hạng riêng tập hồ sơ đã ứng tuyển vào một tin. CV riêng tư đã được gửi cho tin đó chỉ được xử lý trong phạm vi đơn ứng tuyển, không được đưa vào kho CV công khai.

#### 7.2. Ứng viên tìm vị trí theo CV

- Dùng CV do ứng viên chọn để gợi ý các vị trí đang tuyển, có JD công khai.
- Hiển thị điểm phù hợp, điểm đáp ứng, phần còn thiếu trong hồ sơ và các điều kiện công việc liên quan.
- Cho phép chủ động chọn một JD để phân tích chi tiết, kể cả khi JD đó không nằm trong nhóm gợi ý đầu tiên.
- Không yêu cầu ứng viên công khai CV để sử dụng chức năng gợi ý việc làm cho chính mình.

#### 7.3. Điểm phù hợp và bằng chứng

- Điểm nằm trong khoảng 0–100, gắn với phiên bản CV, phiên bản JD và quy tắc đánh giá đã sử dụng.
- Cùng một cặp CV–JD và cùng quy tắc phải có cơ sở tính điểm nhất quán ở cả hai phía; cách diễn đạt giải thích có thể khác theo vai trò.
- Các nhóm tiêu chí đề xuất gồm kỹ năng chuyên môn, kinh nghiệm liên quan, dự án và học vấn/chứng chỉ khi JD yêu cầu. Không mặc định mọi JD đều cần đủ các nhóm này.
- Phân biệt tiêu chí bắt buộc với tiêu chí ưu tiên; hiển thị rõ yêu cầu bắt buộc chưa có bằng chứng dù điểm tổng cao.
- Mỗi tiêu chí cần thể hiện mức đáp ứng và bằng chứng từ CV/JD. Không suy ra năng lực nghề nghiệp từ các thông tin không liên quan đến yêu cầu công việc.
- Đề xuất để LLM hỗ trợ nhận diện và đối chiếu bằng chứng, còn điểm tổng được tính theo quy tắc và trọng số có thể kiểm tra. Trọng số và ngưỡng phân loại chưa chốt, cần được đánh giá thực nghiệm.
- Địa điểm, hình thức làm việc và mong muốn lương nên được hiển thị rõ như điều kiện phù hợp, tránh để một điểm tổng che khuất khác biệt quan trọng.
- Khi dữ liệu không đủ hoặc phân tích lỗi, trả về trạng thái chưa thể đánh giá thay vì tự gán điểm 0.

#### 7.4. Diễn đạt khoảng cách kỹ năng theo vai trò

| Tình huống: JD yêu cầu Docker nhưng CV không đề cập | Nội dung hiển thị |
| --- | --- |
| Nhà tuyển dụng | “Chưa tìm thấy bằng chứng về Docker trong CV.” |
| Ứng viên | “Còn thiếu kỹ năng Docker”, kèm giải thích dựa trên nội dung CV và gợi ý nguồn học tham khảo |

Đối với ứng viên, phần giải thích cần làm rõ kết luận dựa trên thông tin CV. Nội dung bổ sung đề xuất: **“CV hiện chưa thể hiện kỹ năng Docker mà JD yêu cầu. Nếu bạn đã có kỹ năng này, hãy bổ sung kinh nghiệm hoặc dự án minh chứng; nếu chưa, tham khảo các nguồn học được đề xuất.”** Cách này giữ nhãn kỹ năng còn thiếu theo yêu cầu, đồng thời phân biệt việc hồ sơ thiếu bằng chứng với việc ứng viên chưa có kỹ năng.

### 8. AI tìm và đề xuất tài liệu học tập

- Từ các kỹ năng còn thiếu so với JD, hỗ trợ tìm nguồn học tham khảo qua công cụ tìm kiếm web.
- Backend kết nối dịch vụ tìm kiếm và cung cấp kết quả cho AI chọn lọc, giải thích; nhà cung cấp và hạn mức cụ thể sẽ được quyết định ở tài liệu công nghệ.
- Tài liệu đề xuất cần có tên, nguồn, đường dẫn, ngôn ngữ, kỹ năng liên quan và lý do phù hợp. Trình độ, chi phí hoặc thời lượng chỉ hiển thị khi có thông tin từ nguồn.
- Ưu tiên nguồn chính thức và nội dung có thể kiểm tra; không tự tạo URL hoặc tuyên bố đã đọc đầy đủ khi chỉ có trích đoạn tìm kiếm.
- Truy vấn tìm kiếm tập trung vào kỹ năng, trình độ và ngôn ngữ học; không cần gửi tên, email, số điện thoại hay toàn bộ CV ra dịch vụ tìm kiếm.
- Nếu không có kết quả phù hợp hoặc hết hạn mức, thông báo rõ; kết quả chấm CV–JD vẫn có thể xem độc lập.
- Đề xuất tìm tài liệu khi người dùng yêu cầu, giới hạn số truy vấn mỗi lượt và chỉ lưu/tái sử dụng kết quả trong phạm vi nhà cung cấp cho phép.

Chức năng này cung cấp tài liệu tham khảo, chưa bao gồm khóa học nội bộ, cấp chứng chỉ hoặc đánh giá người dùng đã thành thạo kỹ năng sau khi đọc tài liệu.

### 9. Quản trị và theo dõi vận hành

- Quản lý tài khoản và vai trò; xử lý tài khoản hoặc nội dung bị báo cáo.
- Quản lý tin tuyển dụng để hỗ trợ gỡ hoặc ẩn tin không phù hợp. Cơ chế kiểm duyệt trước khi công khai tin chưa chốt.
- Theo dõi lỗi trích xuất, lỗi phân tích và lỗi tìm kiếm tài liệu để hỗ trợ xử lý.
- Theo dõi số lượt gọi AI, lượng token và mức sử dụng dịch vụ tìm kiếm để kiểm soát ngân sách tổng. Đề xuất bổ sung hạn mức theo tài khoản để phân bổ lượt dùng.
- Khi hết hạn mức AI, vẫn cho phép đăng tin, tìm kiếm thông thường, xem hồ sơ theo quyền, ứng tuyển và sàng lọc thủ công.
- Các file CV phải được lưu bền vững và truy cập theo quyền; không coi việc biết đường dẫn file là đủ quyền đọc.

### 10. Phạm vi mở rộng và các quyết định còn lại

Các chức năng sau không thuộc phạm vi chính trong 3 tháng:

- OCR cho CV scan/ảnh.
- Ứng dụng di động.
- Quản lý phỏng vấn, offer, hợp đồng và nhân sự sau tuyển dụng.
- Chat thời gian thực, thanh toán và gói dịch vụ thương mại.
- AI tự động ra quyết định tuyển dụng hoặc tự chỉnh sửa hồ sơ mà người dùng chưa duyệt.
- Huấn luyện mô hình nền tảng riêng hoặc xây hệ thống quản lý học tập đầy đủ.

Các điểm cần xác nhận hoặc hoàn thiện ở bước thiết kế tiếp theo:

| Quyết định | Trạng thái hiện tại |
| --- | --- |
| Thông tin liên hệ khi ứng viên xem CV công khai của người khác | Đã chốt: chỉ bản đã ẩn liên hệ; chưa có bản an toàn thì không trả file gốc |
| Quyền xem của khách chưa đăng nhập | Đã chốt: chỉ JD công khai; CV yêu cầu đăng nhập và kiểm tra quyền |
| Admin đọc CV riêng | Đã chốt: không có quyền mặc định |
| Ngôn ngữ giao diện | Đã chốt: tiếng Việt; xử lý CV/JD Việt–Anh; chưa đồng nghĩa UI song ngữ |
| Vai trò khi đăng ký | Đã chốt: chọn một vai trò Candidate hoặc Recruiter; Admin cấp riêng có kiểm soát |
| Owner công ty | Đã chốt: một recruiter owner trên mỗi công ty |
| Rút/nộp lại và đọc đơn đã rút | Đã chốt: recruiter vẫn đọc hồ sơ sau rút; được nộp lại sau rút/bị loại nếu không bị recruiter chặn |
| Phạm vi recruiter chặn ứng viên | Đã chốt: chỉ tin tuyển dụng hiện tại; recruiter phụ trách tin đặt/bỏ chặn |
| Mẫu CV | Đề xuất 2–3 mẫu cố định, xuất PDF; chưa chọn thiết kế cụ thể |
| Quy tắc tính điểm | Chưa chốt trọng số, mức đáp ứng và ngưỡng phân loại; cần góp ý của giảng viên và thực nghiệm |
| Quy mô thực nghiệm | Chưa chốt số CV/JD, số cặp đánh giá và ngưỡng đạt |
| Hạn mức AI và tìm kiếm web | Cần đo chi phí thực tế để đáp ứng ngân sách khoảng 4 USD/tháng |
| Lưu giữ hồ sơ đã gửi | Đã chốt: 90 ngày sau rút/bị loại; yêu cầu xóa tài khoản thu hồi truy cập ngay, có 7 ngày hủy yêu cầu rồi xóa/ẩn danh dữ liệu chính; backup hết hạn tối đa 14 ngày sau xóa dữ liệu chính |

Các lựa chọn công nghệ chi tiết, kế hoạch thực hiện và thiết kế dữ liệu sẽ được phát triển trong các tài liệu tương ứng, dựa trên phạm vi nghiệp vụ của đặc tả này.

Quyết định trong lượt xác nhận ngày 15/10/2026 chỉ cập nhật yêu cầu; chưa phải các API GĐ2 đã triển khai. Theo dõi [checklist dịch vụ](phase-01-services-checklist.md) và [chính sách lưu/xóa đã chốt](phase-01-data-retention.md).
