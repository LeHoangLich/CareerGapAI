# Phạm vi pilot local được chủ dự án xác nhận

**Audit hiện hành:** bước2đủ điều kiện để chuyển bước3local trên7tuningdocs/164nhãn. Các nhận định giữ bước2mở ở phần lịch sử bên dưới được điều chỉnh minh bạch: phạm vi/quyền/coverage đã được owner xác nhận; nhãn thuộc bước3, finalfreeze thuộc bước6. Thiếu CVnativeViệt/quyền provider/phạm vi đại diện vẫn chặn phần nghiệm thu M5tương ứng, không chặn rà nguồn tuning local. Biên nhận audit private `step2/step2-full-audit-and-local-exit.json`; không đổi consent/split hay giảm điều kiện M5.

Ngày ghi nhận: 25/10/2026. Bổ sung bước2 của [checklist D→E](../tasks/stage-d-to-e-checklist-2026-10-25.md). Đây là xác nhận phạm vi sử dụng và kiểm tính toàn vẹn, không phải nghiệm thu chất lượng M3/M5 hoặc audit pháp lý độc lập.

## Đầu vào đã xác nhận

Chủ dự án chỉ định bộ private `.local/m3-inputs-20261021/` theo [report đầu vào M3](../report/m3-inputs-2026-10-21.md): dùng cho **local evaluation**, căn cứ theo license/công bố nguồn đã lưu. Cả12tài liệu giữ **`provider_transfer_allowed=false`**. Quyền probe trên hai CV synthetic không mở quyền gửi corpus bổ sung này sang DeepSeek. Không hỏi lại key, ngân sách hoặc quyền local vừa được xác nhận.

| Thành phần | Phạm vi đã kiểm |
|---|---|
| Corpus chính |12tài liệu/9nhóm/246nhãn trường|
| CV |4file tiếngAnh/3nguồn; không có CV nativeViệt|
| JD |8tài liệu/6nhóm,5Việt+3Anh|
| Tuning |7tài liệu/4nhóm/164nhãn|
| Final theo manifest cũ |5tài liệu/5nhóm/82nhãn; phải kiểm exposure theo method trước freeze hiện hành|
| Nhãn matching/relevance cặp |0|
| Review nhãn |Codex dự thảo/self_reviewed; human review pending|
| Quyền gửi provider |0/12được phép|

Nhãn246gồm234present,11absent,1ambiguous. Nhãn vắng có cả dữ kiện chủ động loại khỏi bản dẫn xuất; không suy ra tất cả đều là trường vắng tự nhiên trên CV gốc. Ba CV dẫn xuất dàn lại không được gọi là layout gốc. Một nhóm có nhiều file không làm tăng số người/CV độc lập.

## Kiểm tra vừa chạy

Từ repository root, dùng bundled Python với `scripts/validate-evaluation-corpus.py .local/m3-inputs-20261021/corpus.json --verify-files`: **valid,12tài liệu/9nhóm/246nhãn,0lỗi**. Thêm `--for-provider`: từ chối đúng12tài liệu với `provider_transfer_not_allowed`; không gửi request nào.

Metadata audit kiểm12/12source hashes,55/55hash entries trong frozen manifest (51đường dẫn duy nhất), cùng các refs license/quyền/nhãn. Đây là kiểm bằng chứng đã lưu, không phải xác nhận pháp lý mới. Không mở nội dung tài liệu/output mô hình final, không sửa source/nhãn/split/quyền, không nhập CV mới.

## Phân biệt với bộ followup14tài liệu

Bộ `.local/m3-followup-20261021/corpus-v2-ready.json` có thêm DOCX/PDF tiếngAnh cùng một nguồn `cv-d-origin`,46nhãn: tổng14tài liệu/10nhóm/292nhãn. Shared12giữ nguyên hash/split. Hai format không phải hai người độc lập.

Số **187nhãn tuning chưa rà** được nêu trước đây thuộc bộ14, không phải bộ12vừa được chủ dự án chỉ định. Với đúng bộ12, tuning có **164nhãn**;23nhãn DOCX đã rà nằm trong phần bổ sung riêng, không cộng/trừ vào246nhãn của bộ12. Các evidence/review cũ vẫn được giữ riêng; xác nhận hiện tại không tự mở rộng sang nguồn khác hoặc gửi provider.

## Phạm vi có thể chuẩn bị tiếp

Trong bước2 có thể hoàn thành kiểm kê, rà hồ sơ quyền/nhóm/lịch sử exposure, chuẩn bị phương án tiếp nhận và giao thức đánh giá. Sau khi phạm vi tương ứng được chốt mới thực hiện bước nhãn/pilot: rà nguồn tuning, parser/baseline tất định local, chuẩn bị nhãn JD/cặp và các diagnostics có giới hạn. Chưa có nhãn cặp thì chưa báo chất lượng matching.

Không dùng corpus12cho DeepSeek hoặc provider khác. Không đổi cờ false, không dùng dữ liệu dẫn xuất để lách phạm vi quyền, không lấy kết quả synthetic làm chất lượng của corpus này. Không chạy formal final để tinh chỉnh phương pháp.

## Khoảng thiếu còn mở cho nghiệm thu M5

1. Nguồn CV gốc tiếngViệt có phạm vi quyền phù hợp; hiện chủ dự án xác nhận chưa có. Đã chuẩn bị hướng dẫn/template tiếp nhận private ở `.local/stage-d-20261023/step2/`, trạng thái chưa nhận CV/consent, mặc định không gửi provider. Không hỏi lại rằng chủ dự án đã có CVViệt hay chưa.
2. Nếu đánh giá DeepSeek trên tài liệu nghề nghiệp thực thuộc phạm vi nghiệm thu, cần một bộ dữ liệu có quyền gửi provider phù hợp. Bộ12hiện tại không được dùng cho mục đích đó. Không đề nghị tự bật transferfalse thành true.
3. Chốt coverage/giới hạn và nhóm final độc lập cho từng method; dữ liệu đã dùng tinh chỉnh chỉ giữ tuning/lịch sử. Không tự đặt cỡ mẫu/ngưỡng đã được giảng viên duyệt.

Đầu vào chủ dự án đã cung cấp được ghi nhận đầy đủ; **phạm vi pilot local12đã rõ, phạm vi đánh giá đại diện/final chưa chốt**. Audit sau đó xác nhận phạm vi local này đã đáp ứng điều kiện intake bước2để chuyển3; các khoảng thiếu tiếp tục được giữ trong checklist M5, không bị xóa hoặc coi đã đạt. Không cần thêm allowance/API call để kiểm tra và rà nguồn local.

Biên nhận private: `step2/owner-local-pilot12-scope-receipt.json`; phương án/coverage: `step2/local-pilot12-scope-and-gaps.json`; hai kết quả validator tại `evidence/pilot12-*-20261025.json` trong `.local/stage-d-20261023/`.
