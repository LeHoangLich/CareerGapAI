# Đồng bộ ghi chú bảo vệ — 31/10/2026

Auditor/compiler: **Codex (Authorized by Project Owner)**. Approved by: **Lê Hoàng Lịch (Project Owner)**, căn cứ chỉ thị giao biên tập; không giả định owner đã đọc từng dòng đầu ra. Status: **verified**; human_authorized: true.

## Đầu ra

- [Cẩm nang thuyết trình](../handoff/stage-e-20261030/thuyet-trinh.md) thay ghi chú ngắn bằng bốn phần: bài toán/kiến trúc/pilot; liêm chính và rà nhãn; phản biện B01–B14 cùng A13 và C01–C08; demo Fake/Synthetic và xử lý sự cố.
- [README](../handoff/stage-e-20261030/README.md), [M6](../handoff/stage-e-20261030/nghiem-thu-M6.md), [current-status](../current-status.md), [roadmap](../03-roadmap.md) đồng bộ mốc30–31/10 và đường dẫn cẩm nang.
- Lưu bản trước sửa tại `.local/defense-sync-20261031/before/`; biên nhận/hash trong [manifest đồng bộ](../evaluation/defense-docs-sync-2026-10-31.json).

## Số liệu và cách diễn đạt đã đối chiếu

Pilot2CVAnh×4JD/6nhóm/131nhãnfinal/8cặp/k3; 131=95CV+36JD. Exact P=0.7962962962962963, R=0.5119047619047619, F1=0.6231884057971014; TP43/FP11/FN41; trạng thái34/44; rerank CV→JD0.9169956161990744 so baseline1.0 là regression. Partial chưa đo. Các số0,623/0,917 trong lời nói được đánh dấu là làm tròn, không thay metric.

Rà318nhãn/14tài liệu/11nhóm =131final+164tuning+23DOCX;299present/18absent/1ambiguous;300nhãn được chuẩn hóa tọa độ, thêm3cấu trúc needsReview. Không coi300là300lỗi hoặc3là tổng mọi needsReview. Ba validator đạt không chứng nhận chất lượng ngữ nghĩa100%; human direct review=0, không expert review độc lập.

Ma trận35=13+14+8 giữ số lượng lịch sử. A12 source mới backend160/unit15/E2E22, Vite có cảnh báo bundle không chặn. A13 đã rà5tiêu chí và code; ba trangHTTP200/playerOK trong kiểm tra trước, chưa phát toàn bộ video, chưa xác nhận tiếng Việt. Không suy “truy cập công khai” thành giấy phép Open Access, không chạy search thêm.

## Những ranh giới được làm rõ

1. Owner đã chấp nhận pilot, không cần chốt lại; M6/P20 vẫn chờ đánh giá ngoài đang hoãn.
2. Không mô tả Codex rà theoủyquyền là audit chuyên gia độc lập. Script kiểm riêng không thay người đánh giá độc lập.
3. Candidate khác nhận CV công khai đã ẩn liên hệ; recruiter có quyền xem bản gốc/snapshot có liên hệ theo đặc tả. Kịch bản không hứa mọi recruiter đều nhận CVẩnliênhệ.
4. A12 chưa đóng gói vào runtime5190/ZIP30; test release30 là160/11/21, source31 là160/15/22. DemoA12 được ghi có điều kiện sau khi đồng bộ runtime; hiện trình bày bằng bằng chứng test. Token hết hạn kiểm ở backend, E2E dùng link đã dùng cùng mã lỗi.
5. Nội dung Markdown cập nhật tại chỗ theo chỉ thị; ZIP/PPTX/video và verification release cũ giữ lịch sử. Không dùng hash release cũ chứng thực Markdown mới; manifest bổ sung ghim tài liệu mới.

## Kiểm chứng và bảo toàn

Kiểm liên kết nội bộ của các tài liệu bàn giao được sửa, đủ14mụcB/8mụcC/bốn phần, đối chiếu số liệu với báo cáo và manifest gốc; ghi kết quả kiểm tại `.local/defense-sync-20261031/verification.json`. Hash nguồn ứng dụng, ledger, ZIP/PPTX và manifest đầu vào trước/sau không đổi. Không gọi API/web/provider, không phát sinh phí, không chạy lại test ứng dụng vì chỉ sửa Markdown. Không sửa nhãn/corpus/metric/retention, không tạo phản hồi giảng viên, không nghiệm thu M6.
