# GĐ3/B — Hợp đồng extraction, rà soát và builder local

Ngày 16/10/2026. Implementation working tree sau baseline `b392ef0`; xem [báo cáo B](report/stage-b-2026-10-16.md) và [hiện trạng](current-status.md). Không có migration mới: vẫn 15 migrations, các schema mới nằm trong JSON revision đã version hóa. Không nâng dependency.

## Ba schema CV được giữ riêng

| Schema | Ý nghĩa | Consumer |
|---|---|---|
| v1 | Skills AI/Fake và xác nhận kỹ năng cũ | API/worker/confirmation/diagnostic cũ vẫn đọc được; không backfill |
| v2 | Builder `user_entered`, nhóm field có cấu trúc, mọi evidence `user_statement` | Form/PDF; projection self scope mới đọc được; public diagnostic không coi là quote file |
| v3 `cv-fields-v3` | Các field CV được trích xuất, gắn nguồn theo từng path | Worker → revision nguyên bản → Candidate rà soát/sửa → child `user_confirmed`; chuyển đổi có chủ đích sang bản nháp builder v2 |

`fields` gồm `{path,value}`; paths: `displayName`, `email`, `phone`, `location`, `headline`, `summary`; `skills[0..19]`, `certifications[0..19]`; `experience[0..9].role/organization/period/description`; `projects[0..9].name/context/description`; `education[0..9].program/institution/period/details`. Không có field thì bỏ khỏi danh sách; không điền giá trị suy đoán. Giới hạn 180 fields, độ dài riêng từng nhóm; trường/JSON thừa bị từ chối.

Output provider v3 dùng `{fields:[{path,value,evidenceRef,quote}]}`. Validator kiểm tra path, unique, độ dài, segment ID, quote chính xác trong segment và value là substring chính xác của quote. Đây là kiểm tra **tính có nguồn**, chưa chứng minh field được phân loại đúng ngữ nghĩa. Revision lưu source segments, start/end offset trong segment, quote, parser/method/job và hash. Output JSON hợp lệ gốc của full job nằm ở `ai_jobs.output_json`, giữ trước sửa; revision có dữ liệu đã validate riêng. Output không hợp lệ không trở thành revision thành công/điểm 0.

Khi sửa/bổ sung, cùng path + cùng value giữ evidence cũ; giá trị khác hoặc path mới mang `user_statement`, không segment/offset/quote. Bỏ field loại nó khỏi child. Cho rà soát lại child v3 bằng revision mới; optimistic revision check và khóa version trong transaction ngắn ngăn ghi đè đồng thời. File, v1/v2 và revision cha không bị sửa.

## API và khôi phục giao diện

| Endpoint | Hợp đồng và quyền |
|---|---|
| POST `/api/ai/jobs` | Thêm optional `contract: "cv-fields-v3"`; không gửi thì giữ `skills-v1`. Contract tham gia input identity/method snapshot; cùng key khác contract trả 409. Candidate sở hữu version; quota/lease/settlement cũ giữ nguyên |
| GET `/api/resume-versions/{version}/revisions` | Owner Candidate; nguồn và dữ liệu chỉ của version này |
| GET `/api/resume-versions/{version}/extraction-status` | Owner Candidate; `{job:null}` hoặc job full mới nhất với id/status/errorCode; JSON luôn có body. Không trả source/contact ở endpoint status |
| POST `/api/resume-versions/{version}/revisions/{revision}/confirm-full` | `{baseRevisionNo,fields:[{path,value}]}`; owner Candidate, chỉ latest v3; 201 hoặc 409 stale/400 invalid |
| GET `/api/resume-versions/{version}/builder-content` | Owner Candidate; latest v3 `user_confirmed` đúng owner/version → draft builder v2. Không lấy AI chưa xác nhận; thiếu dữ liệu để rỗng; chưa có trả 404 |

Nút đọc đầy đủ tạo job v3; luồng kỹ năng v1 vẫn có để tương thích. UI full hiển thị tên trường tiếng Việt, quote/segment hoặc nhãn tự xác nhận; không hiển thị JSON cho người dùng. Sau reload, đọc trạng thái job đã được server nhận và polling có giới hạn; không tự enqueue lại. Reload **trước khi request được server nhận** không chứng minh đã có job bền vững.

DeepSeek adapter đã phân nhánh prompt/validator theo contract, vẫn dùng cơ chế ngân sách/retry/lease hiện có. **Chưa thử thật, chưa được phép probe trả phí.** MaxOutputTokens và ngân sách cho full CV phải kiểm chứng riêng khi được phép; không suy từ probe skills 512 token rằng full CV đủ output. Fake v3 chỉ nhận các nhãn tường minh/field paths trong synthetic; không phải bộ đọc CV tự nhiên tổng quát.

## Parser cô lập

`DocumentParser.Parse` gửi byte qua stdin tới cùng assembly .NET ở mode `--parse-document-worker`; không tạo backend/service mới. Child không nhận cấu hình DB/provider/session. Không shell expansion, macro hoặc tải tài nguyên ngoài; stdout là protocol riêng, không log nội dung CV. Parent giới hạn input/output và dừng cả process tree ở deadline 10 giây hoặc cancellation, rồi đợi child thoát. Không coi cancellation giữa trang là hard timeout.

Giữ giới hạn 4 MB, 20 trang PDF, 24.000 ký tự/500 segments, DOCX expansion 20 MB/512 entries và giới hạn XML part. Đọc PDF block/reading order; DOCX body gồm paragraph trong table, thêm header/footer ở vùng riêng. Segment có page/paragraph/region khi xác định được. DOCX không có phép phân trang Word thực tế: body được liệt kê trước các part header/footer, không khẳng định layout/page order như Word. Không OCR; ảnh, text box đặc biệt, file khó và header/footer liên quan section vẫn có giới hạn cần corpus thật.

Có test deadline rất ngắn trên process thật, cancellation, body/header/footer, fixture PDF hai cột vi/en, DOCX, PDF không text và file lỗi; Docker Linux thử parent → parser child. Đây không phải sandbox bảo mật OS/giới hạn RAM cứng hoặc bằng chứng mọi file đối kháng đã an toàn. Chưa đo tải nhiều upload đồng thời.

## JD schema v2

Bộ tách `jd-atomic-requirements-v2` tách newline, dấu phẩy/chấm phẩy thành các phần có offset chính xác; bảo toàn required/optional/unspecified theo ngữ cảnh dòng. Có `minimumYears` khi có số năm/range tường minh, `relation=single/any_of`, `alternatives`, `needsReview`. Dòng có “or/hoặc” giữ một nhóm lựa chọn, không biến thành nhiều yêu cầu bắt buộc. Recruiter xem gợi ý, bỏ/chọn và phân loại; nếu phân tích chưa đúng thì bỏ mục và tự bổ sung yêu cầu đã sửa, provenance vẫn riêng.

Là parser local có giới hạn: chưa grammar logic lồng nhau, không tự kết luận mọi câu là yêu cầu nguyên tử hoàn chỉnh hoặc hiểu mọi dấu phẩy. Metadata chỉ là gợi ý được rà soát, chưa phải rubric/chấm điểm. v1 cũ vẫn đọc và xác nhận được, child dùng schema mới; không sửa JSON lịch sử. [Nhãn JD synthetic](evaluation/jd-atomic-smoke.json) gồm vi/en và lựa chọn/years/unspecified, chưa giảng viên duyệt.

## Builder và projection

Chủ dự án đã chốt **một mẫu một cột `simple-v1` cho GĐ3**, thêm mẫu sau. Form nạp v2 đã lưu hoặc chuyển bản v3 đã xác nhận; không tự điền từ AI chưa duyệt. Draft vẫn cần người dùng kiểm tra và bổ sung các trường bắt buộc trước PDF/save.

Sau upload thành công, nếu manual revision thất bại, browser giữ operation IDs + content snapshot trong `sessionStorage` theo user ID. Reload có nút hoàn tất lên đúng version đó, endpoint idempotent trả revision đã có khi response lần trước bị mất. Không upload PDF lần nữa khi đã biết version. State tạm xóa sau thành công/bỏ nháp/đăng xuất hoặc kết thúc phiên tab; UI thông báo việc giữ tạm. Bỏ nháp không xóa file đã tải riêng tư. Không bảo đảm phục hồi draft khi đóng phiên tab/xóa browser storage; khi reload giữa upload trước khi biết IDs, best-effort hash recovery có thể cần người dùng xem lại. Nếu có nhiều ứng viên file cùng hash không rõ operation, không chọn tùy tiện.

L12 consumer dùng projection tối thiểu on-demand từ latest eligible revision đúng version/owner: v1 confirmed; v2 user-entered đã owner xác nhận; v3 confirmed. Không thêm taxonomy/projection table khi chưa có nhu cầu query/index đã đo. Public scope chỉ lấy skill có `document_span`, loại toàn bộ user statements kể cả builder v2; self scope cho phép tự khai báo. Alias diagnostic v1 chỉ `reactjs/react.js → react`, `nodejs → node.js`; không gộp SQL/PostgreSQL hoặc Java/JavaScript. Diagnostic method v2, không biến thành score/ranking.

Thuật toán và config L09 giữ nguyên trên **cùng input text**. Parser mới thêm header/footer có thể làm API nhận text khác dù file gốc giống nhau; đó là thay đổi parser/input, không phải cache/score cũ được bảo đảm giữ nguyên. Không cache baseline hoặc công khai CV private/snapshot ứng tuyển.

## Bổ sung local ngày 19/10/2026

- Full CV real yêu cầu `Ai__FullCvMaxOutputTokens` riêng (128–2048 theo guard), không dùng ngầm 512 skills. `MethodParameters` mới snapshot completion policy/parser; old records vẫn đọc được, không backfill.
- Worker yêu cầu finish reason stop; length báo ai_output_truncated không retry cùng limit và không tạo revision, usage biết được vẫn settle. Model/limit thực tế chưa kiểm chứng.
- Owner export `GET /api/ai/jobs/{id}/evaluation-capture` lấy raw job trước sửa, metadata/usage biết được; terminal jobs mới có provenance, không xuất private cho user/role khác, old metadata thiếu trả 409.
- Có [evaluator captures offline](evaluation/cv-capture-evaluation.md), [runbook full v3](evaluation/deepseek-probe-runbook.md) và [checklist M3](phase-03-acceptance.md). Replay không gọi provider hoặc tự nghiệm thu chất lượng.

## Đánh giá trước sửa

```powershell
# Root; Python có reportlab cho generator, không thêm dependency backend.
python scripts/generate-cv-extraction-fixtures.py
dotnet build backend/CareerGap.Api
./scripts/evaluate-cv-extraction.ps1
# Sau E2E tạo PDF, Python có pdfplumber:
python scripts/check-generated-cv-pdfs.py .local/stage-b-20261016/evidence/builder-long.pdf .local/stage-b-20261016/evidence/builder-short.pdf
```

Generator tạo bốn biến thể vi/en × PDF/DOCX của **một** CV tổng hợp, nhãn đủ các nhóm field và hash từng file, thêm no-text/broken. Evaluator offline luôn Fake/synthetic/smoke, lưu parser recovered-value counts riêng TP/FP/FN/precision/recall/F1, raw trước sửa và assembly/input hashes; không dùng confirmed revision làm ground truth. Các con số chỉ kiểm tra contract/code, không chứng minh chất lượng AI trên CV thực tế.
