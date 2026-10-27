# Các bước còn lại để chốt giai đoạn D và chuyển sang E

Ngày đối chiếu: 26/10/2026, Asia/Saigon (checklist lập 25/10). Đây là hướng dẫn công việc và bảng đối chiếu bằng chứng, **không phải biên bản M5 đã đạt**. Thực hiện từng công việc theo thứ tự; chốt kết quả của việc trước rồi mới bắt đầu việc sau.

Mục tiêu M5 là có sản phẩm local đủ chức năng, provider đã kiểm chứng trong hạn mức, không còn lỗi quyền/snapshot chặn sản phẩm, cùng phương pháp và tập đánh giá cuối đã chốt. **Chạy đánh giá chính thức là P20 của giai đoạn E**, không yêu cầu có kết quả đánh giá cuối trước khi được chuyển E.

Nguồn yêu cầu: [kế hoạch P16–P21](CareerGapAI-danh-gia-va-ke-hoach-2026-10-16.md), [điều kiện M5 trong roadmap](../03-roadmap.md), [hợp đồng D mục 5](../phase-05-improvement-and-administration.md). Đã đối chiếu [report D](../report/stage-d-2026-10-23.md) và các manifest provider mới nhất. Rà hết 187 nhãn tuning là một phương án hỗ trợ, **không phải con số bắt buộc để đóng M5**.

## 1. Đối chiếu phần đã xong và phần còn thiếu

**Đã thực hiện trong lượt lập checklist này.** Anh không cần lặp lại các thao tác đã có bằng chứng.

| Điều kiện | Bằng chứng hiện có | Trạng thái đúng phạm vi |
|---|---|---|
| Viết CV có duyệt | DeepSeek thật 2 mẫu synthetic Việt/Anh; owner accept, rà PDF, lưu private version Việt3/Anh2; nguồn/public selection giữ nguyên | Đã kiểm chứng chức năng; chưa chất lượng đại diện |
| Search thật | Tavily2 requests,6links; owner rà hai nhóm và đối soát2credits | Đã kiểm chứng đợt thử; allowance còn0 |
| Reset email thật | Một accepted/Inbox/reset/login/relogin tới owner; Fake single-use regression riêng | Đã kiểm chứng trong phạm vi recipient thử; unknown đầu giữ nguyên, allowance0 |
| Admin, quyền và ba luồng sản phẩm | Report D: Candidate tìm việc, Recruiter tìm ứng viên, Candidate cải thiện CV; quản trị/report/audit/usage, application snapshot, mobile | Có bằng chứng local; chưa dùng thử giảng viên |
| Kiểm thử cần thiết | Report D ghi133backend/9E2E/3frontend/8readiness; các lượt provider bổ sung ghi riêng | Kết quả lịch sử có bằng chứng; không nhận là vừa chạy lại |
| Phiên bản nguồn | Bước5 có 112/114 file candidate giữ hash, 2 sửa quyền; source/DLL mới ghim riêng, bước6 kiểm lại khớp | Working tree còn dirty; không gán CI/provider/fullsuite cũ cho DLL mới |
| Pilot tuning trên đầu vào hợp lệ | 7 tài liệu/164 nhãn Codex và 4 nhãn cặp local; một sửa importance có bản v2; writing/search thật đã owner rà | Bước 4 đã xong trong phạm vi pilot; chưa chất lượng đại diện hoặc human gold |
| Corpus và quyền provider | Phạm vi owner chốt 12 tài liệu/9 nhóm/246 nhãn, local-only; bộ 14 followup giữ riêng; đã có 4 nhãn cặp tuning | Native CV Việt/quyền provider/coverage đại diện vẫn chưa đủ cho kế hoạch cuối |
| Method và final split | Candidate D, chưa final freeze; final cũ phải kiểm lịch sử sử dụng theo từng phương pháp | Chưa hoàn thành |
| Giao thức, dự toán E và biên bản M5 | Có công cụ/protocol dự thảo; chưa có một bộ đã chốt cho E | Chưa hoàn thành |

Các phạm vi đã biết phải tiếp tục ghi đúng: writing là ghép dữ kiện có kiểm soát, không dịch/viết lại tự do; PDF có nội dung Anh còn tiêu đề mục Việt của simple-v1. Không tự mở thêm template. Search không tuyên bố đọc toàn trang hoặc mọi nguồn đều miễn phí. Email không được mở rộng thành gửi rộng/domain riêng đã xác minh. Unknown email không tự hoàn lại vì đợt sau thành công.

**Điều kiện xong bước 1:** biết rõ mục nào đã có bằng chứng, mục nào thực sự còn thiếu. Bước này hoàn thành; kết quả audit bước2 và tiến độ bước3 được cập nhật tại từng mục dưới đây.

## 2. Chốt phạm vi dữ liệu đánh giá có thể thực hiện

**Kết quả rà lại toàn bộ bước2:** hoàn thành cho phạm vi pilotlocal12tài liệu mà owner đã xác nhận; được chuyển bước3trên164nhãn/7tuningdocs. Phạm vi, quyền local, hash, group/split, coverage và các khoảng thiếu đã rõ. Nhận định trước giữ bước2mở đã gộp các điều kiện chất lượng/finalfreeze của M5 vào intake quá sớm. Đây là sửa phân bổ công việc, không giảm điều kiện M5: CVnativeViệt/quyền đánh giá provider/phạm vi đại diện còn mở; nhãn cặp thuộc bước3 và finalfreeze thuộc bước6. Không đổi transferfalse hoặc coi bộlocal là đủ nghiệm thu DeepSeek. [Phạm vi và audit hiện hành](../evaluation/stage-d-local-pilot-scope-2026-10-25.md).

**Tôi thực hiện:** lập danh sách nhóm dữ liệu, coverage Việt/Anh, PDF/DOCX, quyền local và quyền gửi provider, nhãn trường/nhãn cặp, nguồn đã tiếp xúc trong các lần đánh giá trước. Chỉ kiểm metadata/lịch sử ở tập final khi lập kế hoạch, không lấy output cuối để sửa phương pháp.

**Anh cần cung cấp phần không thể tự suy ra:**

1. Nếu có CV/JD được phép dùng, cho đường dẫn thư mục private và bằng chứng/phạm vi quyền. Tách quyền lưu/phân tích local với quyền gửi DeepSeek. Không gửi key hoặc nội dung liên hệ vào chat.
2. Bổ sung hoặc xác định nguồn CV gốc tiếng Việt có quyền phù hợp; corpus hiện tại chưa có. Nếu chưa có, trả lời rõ chưa có để tôi chuẩn bị phương án tìm/tiếp nhận nguồn; không lấy CV tự tạo làm bằng chứng chất lượng thực tế.
3. Sau khi tôi lập đề xuất từ dữ liệu thực có, chốt phạm vi thử nghiệm có thể bảo vệ và các coverage chưa có. Không tự coi cỡ mẫu hoặc ngưỡng chính xác là đã được giảng viên duyệt.

Mẫu phản hồi đầu vào, chỉ cần trạng thái/đường dẫn:

```text
Nguồn CV/JD bổ sung: <đường dẫn hoặc chưa có>
Nguồn CV gốc tiếng Việt: <đường dẫn hoặc chưa có>
Quyền phân tích local: <căn cứ/phạm vi hoặc chưa rõ>
Quyền gửi DeepSeek: <căn cứ/phạm vi hoặc chưa cho phép>
```

Tôi có thể chuẩn bị nguồn/phương án và xử lý local; không tự cấp sự đồng ý của chủ CV hoặc đổi cờ transferfalse thành true chỉ vì key/budget đã có. Không yêu cầu thêm thành viên hay mặc định cần người thứ hai duyệt.

**Điều kiện xong:** có danh sách dữ liệu/phạm vi đánh giá được chốt, quyền phù hợp với phương pháp định dùng, coverage và phần thiếu được ghi rõ. Số lượng lấy theo phương án được chốt, không tự đặt một mức tối thiểu rồi gọi đó là đủ đại diện.

## 3. Hoàn thiện đúng các nhãn sẽ dùng

**Phạm vi thực hiện sau audit bước2:** bộ12đã xác nhận, chỉ7tài liệu tuning/4nhóm/164nhãn; giữ5tài liệu final riêng. 23nhãn DOCX từng rà thuộc nguồn followup khác, không ghép vào bộ12để tăng n. Không gọi provider.

**Kết quả đã thực hiện:**164nhãn được Codex rà nguồn:161giữ nguyên/2sửa/1giữ mơhồ;7mục JDchưa chốt atomicstructure được loại khỏi mẫu số tương ứng. Đã chuẩn bị4nhãn cặp canonical2CVgroups×2JDgroups, với bằng chứng và giới hạn; humanreview vẫn pending. Validator local/hash/sourceanchors đạt, provider gate chặn đúng7docs. [Biên bản audit2và thực hiện3](../report/stage-d-step2-step3-2026-10-25.md). Hoàn thành3trong phạm vi pilotlocal, chưa nghiệm thu chất lượng đại diện/M5.

**Tôi thực hiện:**

1. Rà field/value/nhóm/quote/locator theo nguồn cho164nhãn thuộc7tuningdocs đã chọn. Con số187trước đây chỉ áp dụng cho bộ14followup, không dùng thay phạm vi12này.
2. Lưu nhãn đã rà thành phiên bản mới có lineage, giữ corpus/nhãn cũ. Giữ23DOCXlabels ở nguồn bổ sung riêng; không cộng bản mới vào số nhãn hay số CV độc lập.
3. Chuẩn bị nhãn JD required/optional/unspecified, số năm và lựa chọn; chuẩn bị nhãn cặp CV–JD, relevance và bằng chứng cho hai chiều matching. Đã có 4 nhãn cặp tuning; trạng thái chưa có nhãn cặp ở lượt intake ban đầu là lịch sử.
4. Giữ absent/ambiguous/unreadable/unknown khác nhau. Không biến mục chưa rà thành absent hoặc cặp chưa nhãn thành không phù hợp.
5. Chạy validator/hash/group/split và kiểm anchors; ghi chính xác người/agent nào đã rà. Kiểm chuỗi tự động không thay được rà ngữ cảnh.

**Phần của anh:** nếu chính anh đối chiếu một packet, tôi mới ghi owner reviewed cho phần thực sự đã đọc. Anh có thể giao Codex rà và sửa; khi đó kết quả vẫn ghi Codex source review. Protocol sẽ chỉ rõ nhãn nào được dùng làm chuẩn, nhãn nào còn dự thảo và chưa được tính vào kết luận chất lượng.

**Điều kiện xong:** nhãn cho phạm vi đã chọn có nguồn/phiên bản/trạng thái review rõ, nhãn mơ hồ được xử lý hoặc loại khỏi denominator thích hợp, có nhãn cặp cần thiết cho phép đo dự kiến; không giả có human/expert review.

## 4. Pilot tuning và xử lý lỗi thực tế

**Hoàn thành ngày 26/10 trong phạm vi pilot đã chốt:** audit 3 đạt; parser 3 PDF khôi phục 118/118 giá trị có nhãn, JD 103 source span khớp/92 needsReview trên 4 tài liệu; writing 4 controls giữ facts/nhóm, chặn 10 plan sai; baseline 2 CV × 2 JD/top-k=1 chạy hai chiều; topic dry-run 15/47 hỗ trợ, 32 ngoài danh mục. Đã sửa một nhãn importance bằng bản v2, kiểm lại diagnostics và validator/gate quyền. Tái kiểm hash 2 workflow writing thật và bằng chứng Tavily 2 truy vấn/6 URL đã owner rà, cùng source/DLL; không gọi thêm. [Biên bản/đối chiếu đủ 8 điều kiện](../report/stage-d-step3-step4-2026-10-25.md). Nhận định nháp giữ 4 mở vì chờ provider quality corpus thực đã gộp gate đại diện/final của M5 vào pilot; điều chỉnh minh bạch, không giảm M5 hoặc mở transfer. **Bước 4 xong; tiếp theo 5. M5/E chưa đóng/mở.**

**Tôi thực hiện:** chuẩn bị một pilot nhỏ theo phạm vi đã chốt, dùng tuning, lưu output trước sửa, kiểm writing không thêm/ghép nhầm dữ kiện và nguồn học phù hợp chủ đề/URL thực. Nếu có lỗi, sửa và kiểm lại trước khi freeze. Các kết quả không được nâng thành đánh giá chính thức trên final.

**Anh thực hiện:** rà các nội dung sử dụng thực tế được giao trong packet, nhất là wording CV trước khi accept/lưu; báo lỗi cụ thể. Không làm lại hai workflow synthetic đã hoàn thành chỉ để tăng số lượt.

Ngân sách hiện ghi DeepSeek đã dùng0,03/2USD, còn1,97USD theo ledger, không cấp lại theo giai đoạn/môi trường. Trước call mới phải kiểm quyền dữ liệu, baseline/ledger hiện hành và dự trù token/retry. Đây không phải xác nhận số dư live hoặc hóa đơn chính xác. Tavily và Resend đã hết allowance của đợt thử. Nếu pilot thực sự cần query search/email mới, phải lập mục đích và allowance bổ sung cụ thể trước; không mặc định cần gọi lại các provider đã kiểm chứng.

**Điều kiện xong:** pilot cần thiết có kết quả và giới hạn trung thực, lỗi tác động chức năng/phương pháp đã được xử lý hoặc xác định rõ; không còn lỗi bịa dữ kiện, sai nhóm hoặc nguồn giả trong phạm vi chốt.

## 5. Chốt lỗi chặn và bằng chứng kiểm thử cùng bản nguồn

**Hoàn thành ngày 26/10:** kiểm lại đủ 8 điều kiện bước 4 rồi rà ba luồng/quyền/snapshot và job/quota/đường lỗi. Đã tái hiện và sửa hai đường trả profile cũ khi quyền thay đổi giữa request: baseline recruiter và batch gợi ý; 3 case mới thất bại trước sửa và đạt trong hồi quy cuối **28/28 PostgreSQL**. Hai file runtime thay đổi có chủ đích; 112/114 file candidate còn lại giữ hash, DLL/source/test/TRX mới được ghim riêng. Đã lập danh sách giới hạn/mức ảnh hưởng/chủ trì/thời điểm; không phát hiện lỗi chặn chức năng còn lại trong phạm vi rà. [Biên bản bước 4–5](../report/stage-d-step4-step5-2026-10-26.md). Bằng chứng full suite/provider trước đó giữ đúng phạm vi lịch sử, không gán cho DLL mới. **Bước 5 xong; tiếp theo 6, chưa freeze/M5/E.**

**Tôi thực hiện:**

1. Đối chiếu ba luồng sản phẩm cùng quyền/snapshot, job/idempotency/quota và đường lỗi với bản nguồn chuẩn bị freeze.
2. Sửa lỗi có bằng chứng làm lộ dữ liệu, mất/ghi đè application snapshot, tự đổi trạng thái sàng lọc hoặc chặn luồng cốt lõi.
3. Chạy test liên quan đến phần đã đổi; nếu nguồn không đổi và bằng chứng/hash còn phù hợp thì dùng đúng kết quả lịch sử, không chạy lại mọi test/provider cho một sửa tài liệu.
4. Lập danh sách lỗi/giới hạn còn lại, mức ảnh hưởng, người phụ trách và thời điểm dự kiến xử lý. Không tự coi giới hạn đã biết là tính năng đã hoàn thiện hơn thực tế.

**Anh thực hiện:** bật Docker khi kiểm tra cần PostgreSQL; chỉ làm các lượt dùng thử có thay đổi cần xác nhận. Bằng chứng owner đã có được giữ lại. Không cần thao tác lại toàn bộ app hoặc cấp key mới.

**Điều kiện xong:** không còn lỗi quyền/snapshot hoặc thiếu chức năng cốt lõi chặn M5; test cần thiết có bằng chứng đúng bản nguồn, lỗi không chặn được ghi rõ.

## 6. Freeze phương pháp, giao thức, tập cuối và dự toán E

**Hoàn thành 27/10 theo ủy quyền mới:** sáu điều kiện đạt trong phạm vi đăng ký 2CVAnh×4JD/k3, 131fieldlabels/8pairs; source/license/quyền mới, confirmed references actorCodex, bindings, protocolv4 và allocation có trần đã ghim. Preflight `valid=true/ready=true/errors0/pending0`; 8pair+4writing roundtrip và20negative controls đạt, không generation/search mới. [Biên bản](../report/stage-d-step6-completed-2026-10-27.md), [manifest](../evaluation/stage-d-step6-final-freeze-2026-10-27.json). Giữ coverage/exposure/human0/quality limits; không tự đóngM3/M5 hoặc mởE. **Tiếp theo bước7**, không còn yêu cầu owner lặp quyền/hạn mức cho6. Các ghi nhận6pending phía dưới là lịch sử.

**Quyết định mới nhất 27/10 — việc 2 đã được trả lời: cả sáu tài liệu chỉ local.** Không tài liệu nào được phép gửi DeepSeek; chưa cấp quyền gọi API hoặc chi tiền. [Biên bản quyết định](../report/stage-d-step6-rights-decision-2026-10-27.md). Đây không còn là câu hỏi chờ xác nhận: không hỏi lại nếu owner chưa thay đổi quyết định. Gate quyền provider chưa đạt, chưa sang việc 3; bước 6/M5/E chưa hoàn thành. Các lời nhắc xin quyền từng file phía dưới giữ làm lịch sử, không phải yêu cầu hiện tại. Bộ hiện có vẫn chỉ dùng chuẩn bị local; không tự đổi phương pháp hoặc hạ điều kiện nghiệm thu.

**27/10 — owner đã chọn pool cho chuẩn bị thử nghiệm hẹp:** việc1 trong hướng dẫn owner hoàn thành với2CVAnh×4JD, giới hạn đã nêu; chưa quyềnprovider/chiAPI. [Biên nhận](../report/stage-d-step6-owner-selection-2026-10-27.md) và manifest27giữ đúng6ID/hash, quyềnfalse và readinesspending. Tiếp theo chỉ [owner việc2](stage-d-step6-owner-actions-2026-10-26.md#2-xác-nhận-quyền-gửi-đúng-các-tài-liệu-được-chọn), chưa sangviệc3. Xác nhận lựa chọn không đổi nhãnCodex thànhhumangold hoặc chốt final/M5/E; không hỏi lại việc1.

**Followup local hiện hành26/10:** [report mới](../report/stage-d-step6-followup-2026-10-26.md) ghi thêm một nguồnCVAnhCC0riêng, candidate2CV×4JD/131fieldlabels/8pairs,106facts và8pair+4writingdraftcontrols đạt đúngDLL5. Corpus12cũ/runtime/ledger giữ nguyên; source quyềnproviderfalse, chưaCVViệt. Protocolv3/manifestfollowup/dựtoán14jobs0,20321280USD đã chuẩn bị; chưachi/call/transfer. [Hướng dẫn owner theo thứ tự](stage-d-step6-owner-actions-2026-10-26.md): actualpool/coverage trước, quyền từngfile sau, rồi đốisoát/APIallowance. Bước6tổng thể chưa xong/bước7-M5-Echưa mở; con số1CV/82labels/4pairs ở các ghi nhận dưới đây là lịch sử trước intake mới. Không cần owner gánnhãn hoặc sửaJSON/chạytool.

**Đã tiếp tục ngày 26/10:** tái kiểm bước5 đạt4/4/42artifacts và source114/test27/DLL/TRX khớp; chuẩn bị [manifest](../evaluation/stage-d-step6-preparation-2026-10-26.json), [protocol](../evaluation/stage-d-final-protocol-2026-10-26.md), [dự toán](../evaluation/stage-d-evaluation-budget-2026-10-26.json), audit exposure theo method và intake final private. Ghim3config hash bằng serializer của DLL hiện tại; công cụ preflight/final-ranking đạt cùng corpus/capture tests **61/61**. Preflight bộ thật valid/pending, prepared envelope bị chặn trước mở input final; chưa chạy E.

**Bước6 tổng thể chưa xong:** final đăng ký chỉ1CVAnh/4JD/82fieldlabels, pairlabels0; parser/JD đã có output lịch sử21/10;12/12transferfalse. Phạm vi đề xuất4CVgroups×4JDgroups/k3 và trầnAI0,50USD trong2USD đã cấp chờ owner chốt; đây không là dữ liệu đã có/mức tối thiểu đại diện. Nguồn final độc lập/references/bindings/quyền provider và đối soát ngân sách trước call còn chờ; search4freecredits chỉ đề xuất, allowance hiện0, email0lượt mới. [Biên bản và điều kiện còn thiếu](../report/stage-d-step6-2026-10-26.md). Phần chuẩn bị local độc lập hoàn tất; không mở quyền/call hoặc tự ghi finalfreeze/M5/bước7/E. Các ghi nhận6chưa bắt đầu ở mục trước là lịch sử.

**Quyết định owner sau điều chỉnh:**4×4 là mục tiêu có điều kiện, không là minimum; pool theo nhóm độc lập thực có đủ nhãn/quyền, ưu tiên CNTT, báo coverage Việt/Anh riêng, giữk3/0,50USD chỉ là dự toán/chưa chi/call/transfer. Đã cập nhật protocolv2/receipt; không chờ đủ4×4 mới được chốt nếu pool nhỏ hơn hợp lệ. Đã rà nguồn82fieldlabels final-candidate (78giữ/4sửa/11structure exclusions), bổ sung4cặp local/36judgments/14excluded; localvalidator5docs/82fields/4pairsđạt, provider gate chặn5. Chưa ghim pool/quyền/confirmed bindings/final độc lập cho toàn bộ E; M5vẫnmở. Nhận định pairlabels0/chờ owner ở đoạn trước là trước cập nhật này; original corpus giữ nguyên.

**Tôi chuẩn bị một manifest hiện hành mới**, giữ candidate và report lịch sử:

1. Ghim source/assembly/config không chứa secret, parser/schema/prompt/rubric/trọng số, writing/learning/template, model alias và giới hạn biết được về model version.
2. Ghim document/group/hash/split, nhãn/quyền/coverage và danh sách dữ liệu demo riêng.
3. Kiểm lịch sử tiếp xúc theo từng phương pháp. Các output JD final đã được xem ngày21/10 phải giữ đúng phạm vi lịch sử; không gọi toàn bộ final cũ là holdout mới chưa chạm. Nhóm đã dùng để tinh chỉnh phương pháp không được thay cho final độc lập: phải có nhóm final mới phù hợp cho phương pháp đó. Kết quả tái sử dụng chỉ giữ làm bằng chứng bổ trợ/lịch sử và nêu giới hạn, không dùng thay điều kiện final độc lập để đóng M5. Không tự xóa lịch sử hay chuyển cùng nhóm qua hai split.
4. Chốt phép đo: extraction trước sửa, coverage theo ngôn ngữ/định dạng; retrieval và ranking hai chiều; score/bằng chứng; writing/nguồn học; latency/usage. Chốt relevance, top-k theo pool, normalization và cách xử lý thiếu nhãn. Không tự đặt ngưỡng đã được giảng viên duyệt.
5. Dự toán số requests/input-output/retry cho E và demo, tách USD AI/search credits/email, giữ trong phần ngân sách chung còn lại. Chỉ xin allowance mới nếu kế hoạch thực sự vượt phạm vi đã cấp.
6. Tham chiếu bằng chứng DeepSeek/Tavily/Resend mới nhất trong manifest mới; các cờ false ở candidate trước probe là lịch sử, không sửa để giả đã biết từ đầu.

**Anh thực hiện:** xem và chốt phạm vi đánh giá/giới hạn/dự toán do tôi chuẩn bị; bổ sung góp ý giảng viên nếu có. Không cần có một giảng viên hoặc người thứ hai ký mới được tự ghi biên bản phát triển local, nhưng không được ghi nhận xét chưa có.

**Điều kiện xong:** có một bộ cấu hình/dữ liệu/nhãn/quyền và kế hoạch chi phí cố định, có thể chạy đánh giá E; final không bị dùng để sửa phương pháp sau freeze. Nếu sửa phương pháp tiếp, tạo bản mới và xử lý lại ảnh hưởng tới final trước đánh giá.

## 7. Lập biên bản M5 và chuyển sang E

**Hoànthành27/10 theo ủyquyền owner:**7/7điều kiện M5 đạt phạmvi chức nănglocalhost vàfreeze thửnghiệmhẹp;[biênbản](../report/m5-local-acceptance-2026-10-27.md),[manifest](../evaluation/m5-local-acceptance-2026-10-27.json). Bộkiểm mới136backend/9E2E/3frontend+lint/typecheck/build/DockerLinuxsmoke đạt,source/DLL/dataset/ledgerkhôngđổi. D hoànthành, côngviệc tiếp theo [E/P20](stage-e-entry-2026-10-27.md);chưa chạyE/nghiệmthuM3quality/M6. Khôngcònviệcownerlặp xácnhận chobước7;Codexđãkýquyếtđịnhlocaltheoủyquyền,khônggiảnhậnxétgiảngviên. Các trạngthái7pending trướcđây làlịchsử.

**Tôi thực hiện:** lập biên bản đối chiếu từng điều kiện M5 với artifacts/hash/kết quả, ghi phần đã đạt và giới hạn còn lại; cập nhật current-status, roadmap và hướng dẫn chạy. Không gán kết quả kiểm thử/CI cũ cho source khác.

**Anh thực hiện:** đọc phạm vi nghiệm thu và các giới hạn được trình bày cụ thể; chỉ cần phản hồi điểm chưa đúng hoặc chưa chấp nhận. Không yêu cầu làm lại các bước đã xong.

Chỉ ghi **D/M5 hoàn thành và chuyển E** khi đủ các điều kiện đã chốt: chức năng/provider cần thiết có bằng chứng, không lỗi chặn, dữ liệu/phương pháp/final split/giao thức/dự toán thực thi được, và hồ sơ nghiệm thu phù hợp cùng phiên bản. Không lấy hết thời gian, synthetic pass hoặc số nhãn lớn làm điều kiện thay thế.

Ở E mới chạy đánh giá chính thức, tổng hợp kết quả, hồi quy/ổn định cuối, backup/restore bản bàn giao, thử README ở môi trường sạch và hoàn thiện báo cáo/slide/demo. Deploy tiếp tục ngoài phạm vi hiện tại.

**Trạng thái sau đối chiếu 26/10:** bước1–5 hoàn thành trong phạm vi đã chốt; bước6 đang thực hiện, đã hoàn tất phần công cụ/giao thức/ghim cấu hình và dự toán local. Bộ final thực thi được còn chờ nguồn/nhãn/quyền/độc lập/phạm vi và đối soát hạn mức trước call. Chưa chuyển bước7, chưa nghiệm thu M5 hoặc mở E; không dùng việc chuyển bước để mở quyền corpus.
