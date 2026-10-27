# Bước 6 — Bổ sung nguồn và hoàn thiện phần chuẩn bị local

Ngày26/10/2026, Asia/Saigon. Theo yêu cầu làm trước mọi việc có thể xử lý trực tiếp, đã bổ sung một nguồn CV thật mới, chuẩn bị bộ candidate2CV×4JD, nhãn, dữ kiện dự thảo, phương án dùng dữ liệu theo method và dự toán theo pool thực. **Chưa final freeze/bước6hoàn thành/M5/E**: quyền provider, xác nhận bộ đầu vào ứng dụng và hạn mức thực thi còn chờ. Không coi yêu cầu tiếp tục là mở quyền corpus hoặc chi API.

## 1. Nguồn đã bổ sung và nguồn chưa đủ căn cứ

Tiếp nhận một CVAnh do tác giả công bố ở [veekas/react-resume](https://github.com/veekas/react-resume), revision `cb99bb346cf78a71d852c211637c2643e60666b7`; lưu README, [giấy phépCC0](https://github.com/veekas/react-resume/blob/cb99bb346cf78a71d852c211637c2643e60666b7/LICENSE) và PDF nguồn bằng hash. Đây là căn cứ công bố của upstream đã được Codex rà cho local evaluation, không phải audit pháp lý độc lập hoặc xác minh lịch sử làm việc của tác giả. Quyền gửi provider vẫnfalse.

Đã nhìn PDF gốc1trang, tạo và nhìn bản dẫn xuất nghề nghiệp1trang; bản mới bỏ tên/liên hệ và các mục ngoài nghề nghiệp, bỏ glyph liên kết và dàn lại một cột. Không tự suy đã ẩn danh hoàn toàn hoặc được quyền gửi cloud. Original/derivative là **cùng một nhóm**, không hai người/CV độc lập. Bản dẫn xuất có text chọn/copy, geometry trong trang, không replacement character; không đổi template sản phẩm simple-v1 hoặc CV của người dùng.

Lượt tìm nguồn có giới hạn chưa tiếp nhận được CV gốcViệt có căn cứ phù hợp. Không tải corpus CV từ nguồn tuyển dụng chỉ vì được search index. Có repository song ngữ nhưng [tuyên bố chỉ cho dùng template cá nhân](https://github.com/HThanh-how/NLHH_CV); chưa xác nhận quyền dùng CV thật để đánh giá nên không nhập. Có nguồn CC0 chỉ áp dụng template và [loại trừ dữ liệu cá nhân](https://github.com/nathanlesage/cv/blob/master/README.md), cũng không dùng để bù mẫu. Chỉ dùng nguồn sơ cấp để quyết định intake; không chạy code/LaTeX/npm từ repository tải về hoặc nhắn tác giả.

Nguồn mới lưu riêng ở `.local/stage-d-20261023/step6-followup/`; **bộ12 owner đã chốt và bộ14 followup cũ giữ nguyên**. Không nhập nguồn mới vào corpus12 hoặc tự coi owner đã duyệt pool mới.

## 2. Bộ candidate và nhãn đã chuẩn bị

| Thành phần | Kết quả |
|---|---|
| Canonical candidate | 2CVAnh CNTT,4JD gồm2Anh CNTT và2Việt ngoàiCNTT;6docs/6groups |
| Field references | 131nhãn =82đã rà +49CVmới; CVmới42present/7absent, humanreview0 |
| Pair references | 8cặp đầy đủ2×4,72criterion judgments;28excluded theo cùng quy tắc của4cặp trước |
| Quyền | 6/6candidate transferfalse; metadata inventory gồm12cũ+1canonical intake mới=13docs, không cộng PDFvariant |
| Local validator | 6docs/131fields/8pairs đạt; provider gate từ chối đúng6docs |
| Dữ kiện để duyệt | 64facts CVcũ và42facts CVmới;4JDdrafts với36reference requirements một phần |

49nhãn mới được Codex đối chiếu PDF/text, không có model output được dùng làm chuẩn. Các period giữ literal, không tự kéo “Present” tới26/10/2026 để tính năm. Một số vai trò cùng employer được kiểm từ heading chung. Field ngoài reference vẫnunknown; absent ở derivative không tự trở thành absence tự nhiên của toàn CV.

Đã bổ sung22facts nghề nghiệp có nguồn vào **bản nháp đầu vào matching/writing của CVcũ**, không cộng chúng vào gold extraction82nhãn ban đầu. Việc này tránh dùng reference “có bằng chứng” từ toàn nguồn trong khi bản nháp chỉ chứa những field từng được chọn để gán nhãn. Mọi positive source judgment đều có quote nằm trong ít nhất một fact được đề xuất. Source judgment và yêu cầu runtime được ghi riêng: kinh nghiệm số năm do backend hoãn đánh giá đầy đủ, nên source hỗ trợ không tự thành runtime `met`.

Các bản nháp chưa có application version/revision/fingerprint thực; chưa ghi `owner_confirmed=true`, chưa tạo AiMethod/AIjob hoặc confirmed revision giả. Bản JDdraft là reference một phần, chưa tuyên bố đã chọn/xác nhận đầy đủ yêu cầu của JD trong app. Đã có file private đọc được cho owner xem, không yêu cầu owner gán nhãn lại131mục.

## 3. Kiểm tra offline đúng DLL và giới hạn

Console riêng tham chiếu DLL bước5 có hash `a21e45f59c8ff7982660cd233a3830a2d9064cbf1b0e5d02a32703a42aa29319`; không ProjectReference, không rebuild API, không packages mới, NuGet sources rỗng, môi trường allowlist không key/dotenv/DB/provider. Không tạo apphost hoặc nối PostgreSQL.

Đã kiểm106field dự thảo theo shape/limit của CVschema3;8pair payload draft controls và4writing draft controls (2CV×vi/en) đều qua validator. Inputadmission của pair tối đa13.425/16.000units; writing tối đa9.799/16.000units, theo đúng UTF8bytes+prompt+1.024framing của backend, không giả token thực đã đo. Extraction input phải được kiểm lại với parser capture thật khi chuẩn bị chạy; bản text rà nguồn không tự thành parser output hiện hành.

Có3positive source references không đi qua literal-overlap guard của facts hiện có. Giữ source reference nguyên trạng, ghi chi tiết giới hạn; control dùng `no_evidence` chỉ để kiểm cấu trúc, **không đổi gold để làm method đạt, không tính đây là model prediction hoặc quality result**. Không sửa thuật toán/prompt theo candidate. Đối chiếu2chiều có thể dùng2CVthay cho1CVsuybiến trước đây; vớik3,poolCV2thì chiềuJD→CVchọn toàn pool, không đo selectionloss ở chiều đó. Chưa chạy ranking chính thức.

Đã lập6conditional gap-topic rows từ catalog21cố định, rà8topic candidates:6hỗ trợ/2marketing không đủ ngữ cảnh bị loại khỏi kế hoạch query. Đề xuất tối đa hai topicCNTT phù hợp (Docker/Git)×vi/en, **chỉ khi output pair thật sau này có gap tương ứng và được cấp allowance**. Chưa có actual pairJobId/gapID, chưa search/URL mới. Không dùng ngôn ngữ query làm bằng chứng ngôn ngữ toàn trang hoặc dựng kết quả web.

## 4. Dữ liệu được dùng cho phép đo nào

Đã lập `method-data-routing-and-exposure.json`:

- Parser CVcũ/JD cũ giữ scope lịch sử đã xem output; không đổi thành một bộ end-to-end pristine mới.
- CVmới được tiếp nhận sau lock method/tools; chỉ có source/PDF/reference review. Chưa có parser ứng dụng hoặc model output trên nguồn này.
- Baseline/pair/writing giữ lịch sử chưa có output final của các method đó; có thể đề xuất thử hẹp trên pool này khi đủ quyền/bindings, với upstream exposure được công bố. Không tự bật global independence verified hoặc quality accepted.
- CoverageCVViệt=0;2JDViệtngoàiCNTT/2JDAnhCNTT có nhiễu ngành-ngôn ngữ. Không kết luận matchingCNTTtiếngViệt hoặc semanticCVtiếngViệt đã được đánh giá.

Đã chuẩn bị datasetbaseline trong envelope`prepared_not_final_frozen`, binding trống và không đặt datasetarrays ở root. Không dựa vào tên split để chặn thực thi: runnerbaseline hiện chấp nhận splitlabel bất kỳ. Negative control trên DLL thật xác nhận envelope bị từ chối`evaluation_dataset_invalid` trước scorer; không sửa runtime/method. Không chạy evaluator final hoặc đổi wrapper để vượt gate. Final pool có thể nhỏ hơn4×4đúng chính sách đã chốt; vẫn phải ghi đúng claim/phạm vi/nguồn/nhãn/quyền.

## 5. Dự toán theo pool thực và phần còn chờ

[Dự toán followup](../evaluation/stage-d-evaluation-budget-followup-2026-10-26.json):2extraction+8pair+4writing=**14jobs**, tối đa28attempts, dự trù0,20321280USD trên giả định giá/parameters đã ghim ngày26/10. Demo mặc định0calls; nếu thực sự cần8jobsdemo mới sau này thì tổng tối đa0,31933440USD. Trầnđềxuất0,50USD và trần phát triển2USD không đổi; ledger vẫnspent0,03/remainingguard1,97, không kiểm số dư live/allocate/debit mới. Giá cần đối chiếu trước call theo [tài liệu provider](https://api-docs.deepseek.com/quick_start/pricing/).

Search tối đa4creditsfree/0USD chỉ đề xuất, allowance0; email0lượt mới. Không tự cấp quyền chi hoặc dùng lại trần đã hết vì môi trường/DB/kỳ thay đổi.

[Manifest followup](../evaluation/stage-d-step6-followup-2026-10-26.json) tách khỏi manifest trước. Preflight bộ mới valid/pending/errors0/exit2:114runtime/27test/23method files/2DLLkhớp,13metadata docs/6selected candidates/8labelled pairs; owner policytrue nhưng sáu readinessgates final/data/independence/rights/references/search/funds cònfalse. Đã kiểm local/source/contract để chuẩn bị, chưa phải final freeze đủ điều kiện M5.

Phần cần owner xác nhận/thao tác được chuyển thành [hướng dẫn tuần tự](../tasks/stage-d-step6-owner-actions-2026-10-26.md). Trước hết cần chốt việc sử dụng pool và các coverage còn thiếu; sau đó quyền cho từng tài liệu; cuối cùng đối soát tiền/credits và quyền thực thi. Các thao tác import/bind/test còn lại do Codex xử lý sau khi đúng đầu vào/phạm vi, không yêu cầu owner sửaJSON/chạytool.

**Tác động:**0DeepSeek/Tavily/Resendcalls,0APIcost dự án mới; có truy cập web/GitHub công khai để tìm và nhận source.114runtime/27backend-test sources/2DLL/corpus12/55frozenentries/ledger/fileWordowner giữ nguyên; tools/test61cũ cùnghash, không chạy lạifullsuite. Dockerengine hiện không kết nối được ở lượt kiểm duy nhất; không tự báo DBbindings đã có, không thay API/Vite/appDB hoặc khởi động worker. Khôngdeploy/commit/push; chưa bước7/M5/E.
