# Đánh giá extraction trước sửa bằng captures — v1

Ngày 19/10/2026. Công cụ offline, không đọc key/khởi tạo API/worker/DB hoặc gửi provider. Chấm output Fake/DeepSeek **đã lưu**; tên provider trong capture là khai báo xuất xứ, không chứng minh call thật. Tất cả dataset/capture/nhãn/quote/output ở `.local` bị Git ignore. Không lấy revision confirmed/builder làm ground truth.

## Nhãn độc lập và chuẩn bị dataset

Dùng manifest/JSONL từ [giao thức corpus](stage-a-corpus-protocol.md). Path theo [contract v3](../phase-03-extraction-contract.md): `skills[0]`, `experience[0].role`…; template `skills[0].name` trước đây đã sửa, không âm thầm đổi nhãn cũ.

- `present`: dữ kiện tường minh, value/quote và locator trang/đoạn từ bản gốc. `absent`: đã xem và xác nhận không có. Không tự tạo absent cho field chưa gán nhãn.
- `unreadable`/`ambiguous` không phải negative. `pending` chưa vào precision/recall. Tự rà rồi đặt `self_reviewed`, không gọi là hai người đồng thuận/giảng viên duyệt.
- V1 so **exact path + exact value**, phân biệt hoa/thường, không fuzzy/alias/normalize hoặc tự ghép item. Đảo thứ tự skill/item có thể bị tính sai; ghi giới hạn này và version protocol nếu đổi, không chỉnh trên final.
- Bản dịch/format cùng nguồn chung group, khóa split trước tuning. Bốn bản một CV không là bốn người. Quyền local và quyền provider riêng; offline không tự cấp quyền gửi.

```powershell
# Root, chỉ nguồn private được chỉ định, không quét kho CV:
python scripts/prepare-cv-capture-dataset.py .local/evaluation/corpus/v1/manifest.json --split tuning --output .local/evaluation/corpus/v1/cv-dataset.json
```

Script dùng corpus validator để kiểm tra file hash/quyền/group/split/label references; chọn CV PDF/DOCX đúng split. Chưa chấm JD bằng lệnh này. Input/output private, output mới không ghi đè. Coverage thấp phải ghi rõ, không bổ sung gold bằng output model.

## Xuất, ghép và chạy replay

Chính chủ lấy GET `/api/ai/jobs/{jobId}/evaluation-capture` bằng phiên đăng nhập và lưu response private. Không gửi cookie/key vào script hoặc commit response. Terminal full jobs mới có metadata được xuất; raw job không bị ảnh hưởng bởi child confirmed. Failed jobs giữ error/raw null; job cũ thiếu metadata bị chặn.

Mapping private, ID khớp dataset:

```json
[{"documentId":"cv-001","captureFile":"capture-cv-001.json"}]
```

```powershell
python scripts/assemble-cv-captures.py .local/evaluation/corpus/v1/cv-dataset.json .local/evaluation/corpus/v1/mapping.json --output .local/evaluation/corpus/v1/captures.json
dotnet build backend/CareerGap.Api
./scripts/evaluate-cv-captures.ps1 -Dataset .local/evaluation/corpus/v1/cv-dataset.json -Captures .local/evaluation/corpus/v1/captures.json -Output .local/evaluation/corpus/v1/report-001.json
```

Assembler khóa dataset SHA, ID/source hash và cùng method/config/prompt/params/parser. Method khác chạy report riêng trên cùng dataset. Không tự lấy latest revision. Evaluator kiểm tra lại rights declarations/hash/giới hạn/metadata, reparse byte gốc và dùng **cùng FullCvExtraction validator** như worker. Parser khác version bị chặn; cần source tương ứng để tái lập, không chấm bằng segment IDs mới. Công cụ không tự xác minh giấy phép hoặc ngữ nghĩa gold.

## Diễn giải report

- Parser recovered-value count tách riêng, không chứng minh mọi cột/thứ tự/quan hệ đúng.
- TP đúng path/value; sai value là FP và FN; output ở annotated absent là FP; thiếu present là FN. Pending/unreadable/ambiguous/chưa có nhãn được loại denominator, đếm riêng; không coi là đúng/sai.
- Empty output hợp lệ với gold present có recall/F1=0. Lỗi parser/JSON/evidence/truncation/thiếu capture có **metrics null**. Tổng hợp chỉ tính documents scored và luôn kèm scored/failed/missing count để không che lỗi bằng điểm đẹp.
- `sourceDisagreements`: prediction đúng value/path và hợp lệ với một segment nhưng segment đó không chứa quote nhãn. Validator thấy quote không chứng minh field được phân loại đúng ngữ nghĩa.
- Có breakdown ngôn ngữ/format/nhóm field, group/coverage/lỗi. Report chỉ path/kết quả/hash, không in value/quote/contact/raw vào tổng hợp/console.
- Lưu dataset/capture/raw/assembly hashes, Git/source state và method/prompt/params. Usage/cost/elapsed là từ captures; **replay không là lời gọi mới hoặc đo mới latency/độ ổn định/chi phí**. Elapsed export gồm queue/retry; unknown null, không đổi USD/test_credit.
- `independentQualityAcceptance=false` có chủ đích: report không tự nghiệm thu corpus/nhãn/chất lượng dù metric cao. Không có ngưỡng giảng viên đã duyệt. Non-synthetic có quyền và output thật là đầu vào để đánh giá thực tế sau.

## Smoke chạy ngay

Dùng fixtures/raw synthetic đã sinh, không tạo PDF mới trong replay:

```powershell
./scripts/evaluate-cv-extraction.ps1
python scripts/prepare-cv-capture-smoke.py --output-directory .local/evaluation/replay-example
./scripts/evaluate-cv-captures.ps1 -Dataset .local/evaluation/replay-example/dataset.json -Captures .local/evaluation/replay-example/captures.json -Output .local/evaluation/replay-example/report.json
# Thư mục mới khác, tiêm lỗi có chủ đích:
python scripts/prepare-cv-capture-smoke.py --output-directory .local/evaluation/replay-fault-example --with-faults
```

Bốn file valid vi/en × PDF/DOCX vẫn một nguồn synthetic; hai PDF lỗi là hai group lỗi riêng. Fault smoke bỏ một field, đặt finish reason length và phá JSON; **đây là lỗi test tiêm, không phải kết quả DeepSeek**. Synthetic không thay corpus đại diện có quyền/nhãn.
