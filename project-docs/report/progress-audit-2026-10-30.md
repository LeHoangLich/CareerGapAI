# Kiểm tra tiến độ dự án — 30/10/2026

**Bổ sung 31/10 — A12/A13:** A12 đã sửa và tái kiểm mới: backend160/160, frontend15/15, E2E22/22, lint/typecheck/build đạt; logs/TRX/result ghim SHA-256. A13 hoàn tất rà năm tiêu chí và đối chiếu adapter/UI, chưa phát hiện lỗi chức năng trong luồng đã rà nên giữ code; giới hạn nguồn tiếng Anh từ truy vấn Việt vẫn công khai. 0 API/credits/phí mới. Demo/bàn giao chưa cập nhật, M6/P20 vẫn chờ đánh giá ngoài; không đổi tỷ lệ nhóm P. [Biên bản mới](a12-a13-verification-2026-10-31.md). Các nhận định chờ A12/A13 phía dưới là trạng thái lịch sử trước lần bổ sung này.

**30/10 — Chủ dự án Lê Hoàng Lịch đã chấp nhận phạm vi chất lượng pilot `pilot_2cv_4jd_limited`** (2CVAnh×4JD, 6nhóm, 131nhãn final, 8cặp, k=3) để trình bày/bảo vệ với giới hạn công khai. Mục chốt phạm vi bởi owner hoàn thành; không chờ owner chốt lại. Exact CV F1=0.6231884057971014 trên95nhãnCV, partial chưa đo; rerank CV→JD nDCG=0.9169956161990744 so baseline1.0, không cải thiện. Human direct review=0; không representative/production-ready. M6/P20 vẫn chờ đánh giá ngoài đang hoãn; không đổi nghiệm thu M3/M5. Không API/phí, giữ nhãn/metric/ledger/bộ bàn giao. [Biên bản](quality-acceptance-2026-10-30.md). Các ghi nhận owner chưa chốt phạm vi phía dưới là lịch sử.

**30/10 — Rà nhãn theo ủy quyền hoàn thành:** Codex trực tiếp đối chiếu 318 nhãn trường/14 tài liệu/11 nhóm (final131, tuning164, DOCX23); validator offline và kiểm exact spans/hash/split đạt. Bổ sung tọa độ nguồn cho 300 nhãn có trích dẫn, phân loại thêm 3 cấu trúc needsReview; giữ 299present/18absent/1ambiguous. Lê Hoàng Lịch là người ủy quyền, Codex là bên đối chiếu; 318 nhãn rà kỹ thuật được ủy quyền, human trực tiếp vẫn chưa có bằng chứng. Overlay mới giữ nguyên nhãn/metric lịch sử, 0 provider calls; M6 chưa nghiệm thu, đánh giá ngoài vẫn hoãn. [Biên bản](label-review-2026-10-30.md).

Đối chiếu mã nguồn tại HEAD `41b82c6`, source release `d119225`, kế hoạch P01–P21 và bằng chứng mới nhất của release20261030. Đây là kiểm tra tài liệu/artifact, không chạy lại test hoặc gọi provider.

## Cách tính và kết luận

Roadmap không có trọng số giờ công/phần trăm đã được thống nhất. Để có số kiểm tra lại được, lấy **21 đầu việc P01–P21, mỗi đầu việc một đơn vị**. Một đầu việc được tính xong khi có đầu ra kỹ thuật/local tương ứng với phạm vi đã chốt và bằng chứng; không đồng nghĩa toàn bộ mốc M1–M6 đã được nghiệm thu chất lượng. Không chia điểm tùy ý cho phần P20 đã làm: giữ toàn bộ P20 ở trạng thái chưa hoàn tất/đang chờ để tránh tự đóng phần đánh giá.

| Trạng thái nhóm P | Số nhóm | Tỷ lệ trên21 |
| --- | --- | --- |
| Có đầu ra kỹ thuật/local hoàn thành trong phạm vi đã chốt | 20 | 95,24% |
| Đã thực hiện một phần, còn chờ/chưa hoàn tất | 1 — P20 | 4,76% |
| Chưa bắt đầu toàn bộ nhóm | 0 | 0,00% |

**Dùng khoảng95% làm chỉ báo hoàn thành đầu ra kỹ thuật/local; không gọi đây là95% chất lượng hoặc95% nghiệm thu.** Đây không phải tỷ lệ giờ công, không dự báo số giờ còn lại. P20 có kết quả thực nghiệm và công cụ, nhưng đánh giá ngoài còn hoãn và phạm vi/chất lượng chưa được chấp nhận. `0% nhóm chưa bắt đầu` không có nghĩa mọi phần việc bên trong đã xong: dữ liệu/nhãn và phần thẩm định còn thiếu được liệt kê riêng dưới đây.

## Chi tiết từng đầu việc

Mỗi dòng chiếm1/21, khoảng4,76% tổng đơn vị. Các báo cáo lịch sử được dùng theo thời điểm/phạm vi, không gán kết quả provider cũ cho DLL30/10.

| Mã | Công việc | Trạng thái hiện tại và giới hạn |
| --- | --- | --- |
| P01 | Đồng bộ hiện trạng, rà nền/checklist M1/M2 | Xong đầu ra kỹ thuật: baseline/checklist, dựng nguồn sạch, quyền/file/job; không tự công nhận M1/M2 đã được nghiệm thu toàn bộ |
| P02 | Chuẩn bị corpus/protocol | Xong chuẩn bị và freeze cho thử nghiệm hẹp đã chốt2CVAnh×4JD; không đại diện Việt–Anh/nhiều ngành |
| P03 | Provider, key backend, hạn mức và probe | Xong kiểm chứng có kiểm soát; tích hợp thật có evidence lịch sử riêng |
| P04 | Full CV có provenance | Xong schema/worker/quote/child revision; nguồn tự khai được phân biệt |
| P05 | Parser và file lỗi/khó | Xong phạm vi PDF text/DOCX, cô lập/timeout và các ca đã thử; không bao gồm OCR/mọi layout |
| P06 | Worker → revision → rà soát | Xong luồng có trạng thái, xác nhận và provider probe; độ đúng đại diện vẫn chưa nghiệm thu |
| P07 | JD cấu trúc và rà soát | Xong parser/yêu cầu/phân loại/source và nhãn trong phạm vi thử; không tuyên bố NLP exhaustive |
| P08 | Builder/PDF/version/phục hồi/ứng tuyển | Xong simple-v1, preview/lưu/mở/recovery và recruiter tải đúng snapshot |
| P09 | Đầu vào matching và đo extraction | Xong consumer theo scope và phép đo trước sửa trên mẫu hẹp; thiếu nativeCVViệt/human review, không đóngM3quality |
| P10 | Rubric và JD snapshot | Xong rubric thử nghiệm và quyết định dùng JD lúc nộp cho application; chưa thẩm định chuẩn chất lượng |
| P11 | Job/usage/quota | Xong job bền vững, snapshot, lease/retry/settlement/unknown và quota |
| P12 | Chấm cặp/điểm/giải thích | Xong contract/validate/scoring/UI/đọc lại; điểm0–100 không là năng lực/xác suất tuyển |
| P13 | Gợi ý hai chiều/cache/quyền | Xong phạm vi local giới hạntop3, quyền và cache; không đổiL09 để ép kết quả |
| P14 | Phân tích tập đơn | Xong ngữ cảnh application và snapshot/owner; AI không tự đổi sàng lọc |
| P15 | So sánh sơ bộ/hiệu năng | Xong phép đo sơ bộ và báo giới hạn; không có kết luận LLM vượtbaseline |
| P16 | Viết CV từ dữ kiện/duyệt | Xong pipeline đề xuất/duyệt/bản nháp/PDF/version; giữ giới hạn ngôn ngữ pha trộn, không nhận là dịch thuật hoàn chỉnh |
| P17 | Nguồn học thật | Xong adapter/flow và các probeTavily trong phạm vi được cấp; chất lượng nguồnViệt chưa được công nhận |
| P18 | Admin/report/usage/email | Xong local và reset email thật tới owner; không coi đó là verified domain/production gửi tùy ý |
| P19 | UX và ba luồng | Xong điều hướng, trạng thái/lỗi/mobile cơ bản và tích hợp; thêm nút xem lại kết quả trên bản30/10 |
| P20 | Đánh giá cuối và dùng thử người đánh giá | **Đang chờ/chưa hoàn tất**: đã có freeze, thực nghiệm thật, metric/chi phí và diễn tập; chưa phản hồi ngoài/nhận xét chấp nhận phạm vi/chất lượng. Phản hồi giảng viên hoãn theo owner |
| P21 | Ổn định/restore/báo cáo/demo/bàn giao | Xong phạm vi kỹ thuật/local đã chốt; sourceZIP, clean build, backup/restore, mode rollback và video mới có evidence. M6 vẫn chưa nghiệm thu |

## Tỷ lệ theo giai đoạn A–E

| Giai đoạn | Các P | Xong | Chờ/chưa hoàn tất | Chưa khởi động nhóm | % xong trong giai đoạn theo sốP |
| --- | --- | --- | --- | --- | --- |
| A | P01–P03 | 3/3 | 0 | 0 | 100% đầu ra chuẩn bị/local |
| B | P04–P09 | 6/6 | 0 nhóm; M3quality vẫn mở | 0 | 100% kỹ thuật theo phạm vi thử |
| C | P10–P15 | 6/6 | 0 nhóm; thẩm định chất lượng vẫn mở | 0 | 100% chức năng/local/sơ bộ |
| D | P16–P19 | 4/4 | 0 | 0 | 100% chức năng/local đã nghiệm thuM5 |
| E | P20–P21 | 1/2 | P20 | 0 | 50% số nhóm; P20 đã có phần lớn đầu ra kỹ thuật |

Không đọc `E50%` thành còn nửa giờ công giai đoạnE: đây là hệ quả của hai nhóm có kích thước khác nhau. Các điều kiện chất lượng còn mở được gom vào phần đánh giá, không được xóa hoặc đổi thành đạt vì các nhóm kỹ thuật xong.

## Các phần còn chờ hoặc chưa thực hiện bên trong đánh giá

1. **Đang chờ, đã được owner hoãn:** người đánh giá thật xem/thử và ghi nhận xét, nêu rõ phiên bản/actor/hình thức; không thay bằngAI. Không yêu cầu làm ngay cho đến khi owner mở lại.
2. **Chưa được chấp nhận:** phạm vi mẫu, rubric, chất lượng và các giới hạn. F1 khoảng0,623, trạng thái34/44 nhãnCodex, CV→JD nDCG@3 khoảng0,917 so baseline1,000 là số đo chất lượng, không phải phần trăm tiến độ. Kết quả sau sửa thuộc tập đã xem.
3. **Đầu vào/thẩm định chưa có:** CV gốcViệt trong tập cuối, coverageViệtCNTT phù hợp và nhãn benchmark do người thật rà (hiện0). Tập cuối hiện2CVAnh×4JD; nhãnCodex và thao tác owner trênUI không biến thành nhãn human benchmark. Chưa có mức n/cỡ mẫu đại diện được duyệt nên không gán% hoàn tất tùy ý cho phần này.
4. **Chất lượng nguồn học:** đã có search thật/đối soát4credits, nhưng ngôn ngữ và độ hữu ích nguồnViệt chưa được công nhận; không ghi là search thật hoàn toàn chưa làm.
5. **Chỉ làm nếu phạm vi/góp ý yêu cầu:** bổ sung dữ liệu/nhãn, tập kiểm tra độc lập mới hoặc sửa có ảnh hưởng rồi kiểm lại. Không mặc định phải mở rộng corpus, dịch thuật hoặc gọi lại provider để hoàn thành phạm vi thử hẹp; không dùng tập đã tune làm holdout mới.

Các mục1–5 không có trọng số giờ công/tiêu chí số lượng được chốt, vì vậy không chia tiếp4,76% thành số giả chính xác. Không coi việc hoãn là hoàn thành hoặc loại P20 khỏi mẫu số.

## Chưa làm nhưng ngoài phạm vi hoàn thành localhost

Deploy/public hosting, domain/HTTPS/private storage trêncloud và kiểm chứngproduction; OCR, mẫuCV bổ sung, dịch toàn bộ nội dung tự động; scheduler/offsite/production retention proof. Đây là phần hoãn/ngoài phạm vi hoặc mở rộng chưa được yêu cầu, không tự đưa vào mẫu sốP01–P21 hoặc tự triển khai.

## Bằng chứng chính

- [Kế hoạchP01–P21](../tasks/CareerGapAI-danh-gia-va-ke-hoach-2026-10-16.md).
- [A: checklist/chuẩn bị](stage-a-2026-10-16.md), [B: provider và luồng thật](stage-b-provider-2026-10-21.md), [C: P10–P15](stage-c-2026-10-22.md), [M5 local](m5-local-acceptance-2026-10-27.md).
- [Phạm vi2CV×4JD đã chốt](stage-d-step6-completed-2026-10-27.md), [chất lượng sau sửa](stage-e-followup-2026-10-27.md), [owner vàTavily](stage-e-external-evidence-2026-10-28.md).
- [Ma trậnM6](../handoff/stage-e-20261029/nghiem-thu-M6.md), [bản30/10](handoff-sync-2026-10-30.md), [receipt bản giao](../handoff/stage-e-20261030/verification.json).

Receipt bản30/10 xác nhận backend160/160, frontend11/11 và E2E21/21 (tích hợp+mock), cleanZIPbuild đạt, backup/restore43bảng/7objects và mode rollback/fault đạt. Không dùng số test pass làm tỷ lệ chất lượngAI. CIremote của commit30/10 chưa chạy vì khôngpush; kiểm tra local có bằng chứng riêng, không gán CIlịch sử cho commit này. Không sửa gói bàn giao hoặc điều kiện nghiệm thu trong lần kiểm tra tiến độ này.
