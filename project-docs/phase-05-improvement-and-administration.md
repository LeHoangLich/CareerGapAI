# GĐ5 — Cải thiện CV, nguồn học và quản trị tại localhost

> 27/10 — Đã hoàn tất phần kỹ thuật E sau sửa: CV2/2, pair8/8 trên method mới; F1 exact0,623 và34/44 trạng thái tham chiếu, rerank CV→JD nDCG0,917 so baseline1,00, không cải thiện. Đây là regression trên tập đã xem, giữ kết quả E đầu. Backend159/159, E2E9/9, FE4/4/lint/typecheck/build; restore current DLL19migrations/43tables/11files/3ledger, Docker+3HTTP và full writing UI→PDF→new private version đạt. Demo đã chuyển sang DB persistent riêng, hai role và proposal Fake chưa duyệt sẵn cho giảng viên. DeepSeekfollowup debit0,04+hold0,01; total spent0,11+hold0,03/remaining1,86 trong2USD; một journal unknown trung gian giữreservation, không hoàn. Tavily mới2credits/2requestsDocker, Git khônggap; UsageAPI tổngchưađổi, giữdebit/capđóng; email0. Chưa xác nhận phản hồi giảng viên, M6/representative quality chưa nghiệmthu. Không push/deploy; userWord giữ hash. Xem báo cáo sau sửa và bộ dùng thử hiện hành. Các trạng thái lỗi kỹ thuật chưa sửa phía dưới là lịch sử. [Báo cáo](report/stage-e-followup-2026-10-27.md), [manifest](evaluation/stage-e-followup-2026-10-27.json), [bộ dùng thử](handoff/stage-e-fixed-20261027/README.md).


> 27/10 — E đã thực thi14jobs/23attempts:CV0/2,pair2/8,writing4/4; P/R/F1 và LLM rerank chưa tính được. Clean directory/9E2E/3FE và restore19migrations/43bảng/18objects đạt; đã chuẩn bị bộ dùng thử, chưa có feedback giảng viên. **E/M6 chưa hoàn thành**, còn lỗi AI/topic và chưa release commit. [Báo cáo hiện hành](report/stage-e-2026-10-27.md). Các ghi nhận E chưa chạy bên dưới là lịch sử.


> 27/10 — bước7/M5 hoànthành phạm vi tích hợp localhost và kếhoạchthựcnghiệmhẹp; [biênbản](report/m5-local-acceptance-2026-10-27.md), [manifest](evaluation/m5-local-acceptance-2026-10-27.json). P16–P19 đủ chức năng đã kiểm;136backend/9E2E/3frontend mới,lint/typecheck/build vàDockerLinuxsmoke đạt. Bằngchứngprovider lịch sử giữnguyên,0calls/phí mới. D kếtthúc,sẵnsàngE/P20;chưađánhgiá cuối/M3representativequality/M6. Các ghi nhận chờM5 dướiđây là lịch sử.

> 27/10: bước6 D→E đã hoàn thành cho thử nghiệm hẹp 2CVAnh×4JD theo ủy quyền mới; [biên bản](report/stage-d-step6-completed-2026-10-27.md) và [manifest](evaluation/stage-d-step6-final-freeze-2026-10-27.json) ghim phương pháp/dữ liệu/quyền/bindings/chi phí, preflight ready. Chưa nghiệm thu M5 hoặc mởE; tiếp theo bước7. Các trạng thái pending trước đây giữ làm lịch sử, không phải tiến độ hiện hành.

Ngày đối chiếu: 23/10/2026, Asia/Saigon. Đây là hợp đồng implementation D/P16–P19; không đồng nghĩa M5 đã được nghiệm thu. Xem [biên bản D](report/stage-d-2026-10-23.md) cho bằng chứng và phần còn chờ. Các quyết định nghiệp vụ nền ở [đặc tả](01-system-specification.md) vẫn áp dụng.

## 1. Soạn CV từ dữ kiện đã xác nhận — P16

`suggest_cv` dùng chung hàng đợi PostgreSQL, lease/generation, attempts, user quota và ngân sách tổng với extraction/matching. Không thêm queue trong RAM. Input ghim CV version, revision xác nhận, ngôn ngữ và fingerprint bất biến. Chỉ Candidate chính chủ với file sẵn sàng và quyền hiện tại được tạo/đọc/duyệt; Admin không được đọc đề xuất CV riêng.

Phương pháp thử nghiệm `cv-composition-v1`:

- Nhận trường nghề nghiệp từ v3 `user_confirmed`, v2 builder `user_entered` có xác nhận, hoặc skills v1 đã xác nhận. Không gửi các trường liên hệ riêng để soạn.
- Provider chọn/thứ tự **khóa dữ kiện nguyên vẹn** cho giới thiệu và mô tả kinh nghiệm/dự án. Backend kiểm tra khóa, nhóm, trùng lặp, target và độ dài rồi kết xuất bằng nhãn Việt hoặc Anh. Không nhận prose/claim/điểm/URL do model tự thêm.
- Giữ nguyên nội dung nguồn, số liệu và phủ định. Đây là ghép dữ kiện có kiểm soát, **chưa phải viết lại tự do hoặc dịch chéo ngôn ngữ**. Nội dung nguồn tiếng Anh có thể được trình bày với nhãn Anh; đổi nhãn không biến dữ kiện tiếng Việt thành bản dịch Anh.
- Người dùng xem nguồn, sửa và `accept` hoặc `discard`. Mọi nội dung đưa sang builder được coi là `user_statement`; không biến chữ người dùng sửa thành trích dẫn tài liệu.
- Decision bất biến và idempotent theo nội dung JSON; chấp nhận/bỏ không tự tạo file hay version CV. Builder chỉ lưu sau bước xem trước PDF và thao tác lưu riêng.
- Reload có thể lấy lại đề xuất mới nhất của đúng version. Bản nháp chỉ nhận đề xuất khi khớp revision nguồn đã nạp; sửa bản nháp hoặc đổi version yêu cầu nạp lại nguồn trước khi áp dụng. Không ghép nhầm mô tả sang nhóm của CV khác.

| API | Phạm vi |
|---|---|
| `POST /api/writing/proposals` | Candidate; version, ngôn ngữ `vi/en`, idempotency key |
| `GET /api/writing/proposals?resumeVersionId=…` | Đề xuất mới nhất chính chủ, phục hồi sau reload |
| `GET /api/writing/proposals/{id}` | Kiểm quyền hiện tại; không trả payload liên hệ/raw output |
| `POST /api/writing/proposals/{id}/decision` | Duyệt/bỏ đề xuất đã thành công; chỉnh sửa có giới hạn |

Paid writing yêu cầu thêm `Ai__WritingMaxOutputTokens` (128–2048) cùng cấu hình DeepSeek/ngân sách/pricing có sẵn. Hai attempts tối đa nằm trong dự trù chung. Khi worker mất lease hoặc quyền thay đổi, không phát hành kết quả. Không có nút hủy request provider đang gửi; bỏ đề xuất ở đây áp dụng sau khi kết quả sẵn sàng.

**Probe thật bổ sung 23/10:** hai mẫu synthetic với dữ kiện gốc Việt/Anh chạy DeepSeek thành công, 2 attempts/0 retry, kiểm whole facts/group, cùng key và reload. Cost bảo thủ 0,00054900 USD; debit 0,01 USD, còn 1,97/2 USD toàn giai đoạn. **Chủ dự án đã rà hai mẫu, xác nhận giữ đúng dữ kiện và không cần sửa; chưa ghi accept hoặc tạo CV version trong ứng dụng từ output.** Source runtime/assembly không đổi; search/email vẫn Fake. [Report mục 6](report/stage-d-2026-10-23.md#6-probe-writing-that-va-ban-giao-ra-soat), [manifest probe](evaluation/stage-d-writing-provider-2026-10-23.json). Không suy từ hai mẫu thành chất lượng thực tế hoặc khả năng dịch/viết lại tự do.

**Cập nhật workflow 25/10:** cả hai proposal thật synthetic đã được owner accept, rà PDF và lưu private version mới (Việt version3, Anh version2, cùng revision1). Kiểm source/public selection, hash file, text và Poppler visual đạt; mẫuAnh chỉ chuẩn hóa phone trống thành null. PDF có nội dung Anh nhưng tiêu đề mục simple-v1 vẫn Việt; chưa có template được địa phương hóa hoàn toàn sang Anh. Đây là kiểm chứng chức năng trên hai mẫu, chưa chất lượng đại diện. [Report mục13](report/stage-d-2026-10-23.md#13-mau-anh-da-luu-va-dau-vao-ra-nhan).

## 2. Nguồn học theo khoảng thiếu — P17

Chỉ Candidate đọc được pair `self` đã thành công, với tiêu chí `partial/no_evidence`, mới yêu cầu nguồn học. Không suy ra ứng viên thiếu năng lực chỉ vì CV chưa có bằng chứng. Recruiter/application context không dùng endpoint này để gửi dữ liệu ứng viên đi tìm kiếm.

`GET /api/learning/topics` trả tối đa sáu chủ đề phù hợp từ danh mục cố định 21 chủ đề. `POST /api/learning/resources` nhận pair/criterion, topic key, trình độ `beginner/intermediate`, ngôn ngữ `vi/en`, idempotency key. Chỉ tên chủ đề trong danh mục, trình độ và ngôn ngữ được gửi ra ngoài; không gửi tên, email, toàn CV/JD hoặc nguyên văn tiêu chí. Danh mục chưa bao phủ mọi ngành; chủ đề ngoài danh mục được báo chưa hỗ trợ.

Mặc định `Search:Provider=Fake`: trả rỗng và UI ghi rõ chưa search web thật. **Cập nhật tuần tự: chủ dự án đã chọn Tavily, key backend được xác thực và cấp tối đa 2 credits miễn phí/0 USD cho một đợt thử.** Probe thật dùng hết hai requests, trả sáu links và debit 2 theo từng phản hồi; chủ dự án rà hai nhóm đạt và Overview ghi đúng 2 lúc 17:45, đóng đối soát theo khai báo dashboard (API snapshots 0 giữ lịch sử). Không có allowance để gọi thêm. [Report mục 8](report/stage-d-2026-10-23.md#8-ra-nguon-doi-soat-hoan-thanh-va-chuan-bi-email), [manifest search](evaluation/stage-d-search-provider-2026-10-23.json). Hành vi adapter:

- Fixed HTTPS endpoint, tắt redirect; một query `basic`, `auto_parameters=false`, tối đa ba URL; không answer/raw content/images. Không retry tự động.
- Chỉ dùng URL do công cụ trả, lọc HTTPS/public-host format, userinfo/port/IP/localhost; không có endpoint fetch URL tùy ý. Backend không truy cập toàn trang.
- Trả title, host nguồn, URL, ngôn ngữ truy vấn và lý do theo chủ đề. Không tuyên bố đã xác minh ngôn ngữ/truy cập nội dung toàn trang.
- Transaction ngắn giữ chỗ một credit trước HTTP, khóa admission; actual usage được ghi sau phản hồi. Timeout/lỗi/usage chưa rõ giữ dự trù và không gửi lại cùng key. Request dang dở sau restart cũng trả `outcome_unknown`, không tự giải phóng credit.
- Quota tổng/tài khoản, kỳ bắt đầu/kết thúc và thời điểm đối soát phải được cấu hình. Kỳ chồng lấn khác biên bị từ chối. Credit tách hoàn toàn USD AI.
- Mặc định **không lưu kết quả search**. Chỉ khi đã rà điều khoản và bật cờ xác nhận mới cache metadata tối đa 60 phút; không lưu snippet/raw page. Cache hit gắn key mới bằng record riêng, actual/reserved = 0. Hết TTL không trả link; worker xóa metadata khi app chạy, quét lại sau restart. Không bảo đảm xóa vật lý trong lúc máy tắt.
- Kiểm quyền trước admission và trước trả links, kể cả cache. Hết credit/không có kết quả không làm mất match result.

Sổ credit trong DB không kiểm soát request ngoài ứng dụng hoặc DB khác. Trước probe thật cần đối soát toàn tài khoản/mọi môi trường, dành một phần hạn mức chung cho probe và lưu manifest; không cấp lại cùng credit khi tạo DB mới. Đợt hiện tại đã dùng hết cap 2 credits, được chủ dự án đối chiếu Overview; không hoàn/cấp lại chỉ vì API snapshots cũ chưa hiện mức dùng.

**Cửa sổ probe và bằng chứng riêng:** Usage API không trả thời điểm/múi giờ reset; docs chỉ nêu ngày đầu tháng. Provider UTC cycle giữ null trong manifest. Đợt 23–24/10 dùng một cửa sổ admission UTC 24 giờ, nằm trọn trong tháng hiện tại, không giả là chu kỳ provider hoặc cấp lại credit; ledger 2 credits có scope mọi môi trường và không reset. Runtime dùng cùng bounds đã ghi cho journal; sau này cửa sổ chồng lấn khác bounds bị `search_period_conflict`, cần xử lý/đối soát có kiểm soát, không xóa journal/đổi DB để lách guard.

Product cache vẫn tắt, `LinksJson=null`. Ngoại lệ bằng chứng thử riêng: chỉ title/URL/source/ngôn ngữ truy vấn/lý do được giữ private cho chủ dự án rà, tối đa bảy ngày, dọn thủ công sau rà; không phục vụ lại từ API/cache, không giữ snippet/raw page. Đã đọc [Terms hiện hành](https://www.tavily.com/terms); không suy quyền lưu toàn trang hoặc xác nhận tính đúng/hợp pháp của mọi nguồn bên thứ ba. Không có scheduler dọn bằng chứng được triển khai.

Nguồn kỹ thuật đã đọc: [Tavily Search API](https://docs.tavily.com/documentation/api-reference/endpoint/search), [API credits](https://docs.tavily.com/documentation/api-credits), [Terms](https://www.tavily.com/terms). Việc đọc docs/terms không phải xác nhận tài khoản, quyền cache hoặc cho phép chi phí của dự án.

## 3. Quản trị, báo cáo và email — P18

**Cập nhật mới nhất 24/10:** đã chốt kiểm chứng email local tới recipient owner. Sau sửa key và cấp thêm một lượt, Resend accepted lúc 02:07 Asia/Saigon; owner xác nhận Inbox/reset/login, thử link cũ có lỗi và relogin bằng mật khẩu hiện tại thành công. Fake regression HTTP1/1 kiểm single-use được ghi riêng, không xác định nguyên nhân lỗi UI thật. Usage sau send **monthly1/3.000, daily0/100**, khớp một thư được accepted/nhận; daily0 phù hợp reset ngày UTC. Giữ DB/cap tổng2 gồm accepted1/unknown1, allowance0/hold gửi thêm, không hoàn lượt cũ hoặc claim hóa đơn chính xác. [Report mục11](report/stage-d-2026-10-23.md#11-chot-email-local-va-chuyen-cong-viec7), [manifest](evaluation/stage-d-email-provider-2026-10-24.json). Chưa nghiệm thu M5 hoặc email gửi rộng ngoài owner.

Các `/api/admin/*` yêu cầu role Admin từ backend hiện tại. Danh sách phân trang 20 mục; UI chọn bản ghi có tên/email thay vì nhập UUID. Không bổ sung quyền đọc CV private, writing input hoặc file theo ID.

- Tài khoản: liệt kê email/role/trạng thái; tạm khóa/mở lại `active ↔ suspended`. Không khóa bản thân hoặc tài khoản Admin; không đổi account đang xóa/đã xóa. Thay security/concurrency stamp trong transaction để cookie cũ không sống lại sau mở khóa.
- Tin: ẩn `published/closed → archived`; khôi phục thành `closed`. Khôi phục không tự mở lại nhận hồ sơ. Khóa cùng row tin và kiểm `RowVersion`; không sửa version hay snapshot. Tin đã ẩn không nằm trong public search hoặc nhận đơn mới; recruiter vẫn xử lý hồ sơ đúng ngữ cảnh đã nộp.
- Báo cáo: Candidate/Recruiter báo tin công khai hoặc CV đang được phép thấy. Tối đa mười báo cáo/người/ngày; lý do có giới hạn. Admin giải quyết/bỏ qua với row version và lý do; quyết định báo cáo không tự khóa tài khoản/ẩn tin.
- Audit: actor, action, target, reason, UTC cùng transaction thay đổi. API trả DTO, không trả entity/CV. Không có API sửa/xóa audit thông thường.
- Usage: ngân sách/giữ chỗ AI và đơn vị của từng sổ; lỗi AI theo code; search credit; trạng thái email. Không cộng `test_credit`, USD và credit search thành một tổng tiền. Sổ local không thay hóa đơn/ledger mọi môi trường.

Email mặc định Fake và hộp thư giả chỉ ở local Development/Testing với provider Fake. UI không hiện hộp thư giả khi chọn email thật. Resend đã được chủ dự án chọn và kiểm chứng tới recipient owner: một accepted có Inbox/reset/login/relogin, một unknown đầu giữ nguyên, không còn allowance gửi thêm. [Plan/giới hạn](evaluation/stage-d-email-probe-plan.json), [manifest](evaluation/stage-d-email-provider-2026-10-24.json), [report hiện hành](report/stage-d-2026-10-23.md#11-chot-email-local-va-chuyen-cong-viec7). Hành vi adapter:

- Chỉ cho gửi reset tới một recipient chủ dự án kiểm soát trong đợt kiểm chứng local; cần key backend, sender đã xác minh, kỳ/hạn mức được duyệt và thời điểm đối soát.
- Fixed HTTPS endpoint, không redirect, timeout 15 giây; journal giữ chỗ trước HTTP và idempotency key riêng. Không retry tự động; lỗi/timeout vẫn giữ lượt dự trù. Không lưu email recipient, reset token hoặc link trong journal.
- `accepted` chỉ nghĩa provider nhận yêu cầu, **không chứng minh email tới hộp thư**. Forgot-password giữ phản hồi chung khi account không tồn tại hoặc lỗi provider/cấu hình, không lộ tài khoản qua mã lỗi.
- Đã kiểm chứng recipient owner nhận thư/reset/login/negative replay UI/relogin và đối soát Usage; single-use HTTP được kiểm bằng regression Fake riêng. Screenshot lỗi thật không capture nguyên nhân HTTP, không mở rộng kết luận sang mọi recipient/domain hoặc production.

Phương án thử localhost tối thiểu theo [giới hạn `resend.dev`](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain): sender `onboarding@resend.dev` chỉ gửi tới email gắn với chính tài khoản Resend của chủ dự án, sau khi xác nhận người nhận đó được kiểm soát. Đây là sender thử được provider cho phép, không phải bằng chứng domain riêng của dự án đã xác minh DNS hoặc được gửi tới địa chỉ khác. `SenderVerified` trong probe phải gắn với cơ sở cho phép sender/phạm vi cụ thể, không tự đặt true khi chưa xác nhận. Email gửi tới địa chỉ khác cần domain hợp lệ đã được provider xác minh. Không triển khai DNS/hosting trong bước này.

Real-email probe dùng **Development**, không dùng Testing vì code Testing ép token reset hết hạn sau 5 giây. Link recovery là HTTP loopback trên máy đang chạy app; không cần deploy. Không in/gửi token, link reset, password hoặc recipient thật vào tài liệu public. Tổng hai attempts: một unknown cũ được giữ, một accepted có Inbox owner xác nhận; không công nhận email delivery chỉ từ phản hồi 204 hoặc journal `accepted`.

Nguồn: [Resend Send Email](https://resend.com/docs/api-reference/emails/send-email), [hạn chế sender thử](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain). Không gán free quota hoặc domain xác minh từ docs thành hiện trạng dự án.

## 4. Schema, retention và UX

Migrations 17–19 thêm `writing_inputs/results/decisions`, `learning_requests`, `content_reports`, `admin_audits`, `email_deliverys` (tên bảng thực do convention hiện hành). Không sửa CV/JD/application snapshot hoặc dữ liệu cũ. Writing input/result/decision có trigger bất biến và kiểm context owner/revision. JSON builder đọc lại từ PostgreSQL dùng so sánh giá trị JSON, tránh sai do `jsonb` đổi thứ tự thuộc tính/khoảng trắng; không đổi scorer L09 hay rubric C.

Purge tài khoản xóa writing input/result/decision, xóa nguồn raw job theo cơ chế cũ; scrub pair reference/criterion/link search và lý do report/audit liên quan, giữ các dòng usage/reservation phục vụ đối soát. Không hoàn lại credit chưa rõ kết quả. Migration đã kiểm fresh và nâng cấp có legacy job; downgrade chỉ kiểm với bảng tính năng mới rỗng. Không dùng Down để xử lý DB đã có dữ liệu D; cần backup và phương án tiến có kiểm soát.

P19 có điều hướng theo vai trò, liên kết/chọn hồ sơ thay nhập ID, nhãn trường profile thay JSON, loading/lỗi/quota và màn hình quản trị. Builder vẫn một mẫu `simple-v1`; không thêm UI framework/package. Các message lịch sử kỹ thuật còn có trong phần diagnostic, không đòi nhập ID để làm tác vụ chính.

## 5. Điều kiện còn lại để chốt M5

**Chuẩn bị local followup26/10:** [report](report/stage-d-step6-followup-2026-10-26.md) ghi candidate2CVAnh×4JD/131labels/8pairs, thêm một nguồnCC0local riêng và giữ corpus12cũ.106draftfacts/8pair+4writingcontractcontrols đạt đúngDLL; sourceguardlimits giữ nguyênkhôngtunemethod. Protocolv3/manifestfollowup/dựtoán14jobs0,20321280USD có sẵn; chưachi/call/transfer. [Ownersteps](tasks/stage-d-step6-owner-actions-2026-10-26.md) bắt đầu bằng actualpool/coverage, rồi quyềnfile, rồi đốisoát/hạnmức; khôngbắt4×4 hoặc humanreview. **Bước6/M5/Echưa đạt/mở**,0providercall/phí/runtime/DBmới. Các số1CV/4cặpbên dưới là trước intake followup.
**Scope hiện hành bước6:** owner chốt4×4là mục tiêu có điều kiện/không minimum, actualpool độc lập đủ nhãn/quyền, ưu tiênCNTT/coverageViệt-Anh riêng; giữk3/trầnđềxuất0,50USD không chi/call/transfer. Rà82nhãn nguồn finalcandidate (78giữ/4sửa/11exclusions), tạo4cặp local/36judgments/14excluded; validator5docs/82fields/4pairsđạt, gateprovider chặn5. Không đổi method/source từ final; nguồn được đọc riêng sau lock để annotate, không mở modeloutput. Scopepolicytrue, còn finalpool/independence/quyền/confirmedbindings và search/funds trướccall;6/M5/Echưa đạt/mở. Không bắt đủ4×4 hoặc humanreview giả. Các trạng thái chờ scope/pairlabels0ở dưới là lịch sử.
**Scope bước6:** owner yêu cầu điều chỉnh đề xuất4×4; chưa có thông số thay thế được chốt. [Bảng lựa chọn](evaluation/stage-d-scope-options-2026-10-26.md) và snapshot/receipt giữ lịch sử; không tự đổi n/k/coverage/quyền hoặc nhận0,50USD là phương án đã duyệt.

**Bước6 tiếp tục ngày26/10:** [biên bản](report/stage-d-step6-2026-10-26.md) và [manifest chuẩn bị](evaluation/stage-d-step6-preparation-2026-10-26.json) ghim source/DLL5,10component/23files/3config hash kế hoạch, protocol/dự toán/provider evidence và audit exposure. Preflight/final evaluator riêng kiểm nguồn/version/revision/fingerprint, hash/parse cùng bytes;61/61tests công cụ đạt. Bộ thật valid/pending và prepared final envelope bị chặn trước input; không đánh giá E. **Chuẩn bị local độc lập xong, bước6 tổng thể còn mở** vì scope4CV×4JD/k3 mới đề xuất, final nguồn/nhãn/quyền/độc lập chưa đủ, searchallowance0/funds chưa đối soát mới. TrầnAIđề xuất0,50USD nằm trong2USD phát triển hiện có, không allocation/debit;0providercall/phí mới. Không đổi runtime/DB/schema; chưa bước7/M5/finalfreeze/E. Các trạng thái6chưa bắt đầu dưới đây là lịch sử.

**Bước 5 chốt ngày 26/10:** [biên bản](report/stage-d-step4-step5-2026-10-26.md) ghi audit lại bước 4 đạt, rà ba luồng/quyền/snapshot/job/quota và sửa hai race trả profile cũ sau thu hồi chia sẻ/khóa tài khoản. Ba regression mới có bằng chứng đỏ trước sửa, hồi quy cuối 28/28 PostgreSQL đạt; source/DLL mới và danh sách giới hạn được ghim riêng. Bước 5 xong trong phạm vi rà, bước 6 chưa bắt đầu; M5/phương pháp/final split/giao thức/dự toán và chất lượng đại diện vẫn chưa đóng. Không probe mới, không đổi schema/dependency hoặc môi trường ứng dụng đang chạy.

**Chốt bước 4 ngày 26/10:** [biên bản](report/stage-d-step3-step4-2026-10-25.md) đối chiếu đủ 8 điều kiện pilot trong phạm vi local đã chốt. Parser/JD/baseline, writing controls và topic dry-run có raw/hash/giới hạn; sửa một nhãn importance bằng v2 và kiểm lại diagnostics, phân loại các mục unresolved/unsupported. Tái kiểm writing thật 2 workflow và Tavily 2 truy vấn/6 URL đã owner rà trên cùng source/DLL; không gọi thêm. Nhận định nháp giữ 4 mở vì chờ provider quality corpus thực đã gộp gate đại diện/final vào pilot, được sửa minh bạch. **Bước 4 xong; bước 5 sẵn sàng, chưa bắt đầu; M5/E chưa đạt/mở.** Corpus transferfalse/search-email allowance 0 giữ nguyên. Kết quả deterministic không thay bằng chứng chất lượng model hoặc đánh giá cuối.

**Hướng dẫn tuần tự hiện hành 25/10:** [checklist D→E](tasks/stage-d-to-e-checklist-2026-10-25.md) đối chiếu phần đã đạt và6công việc còn lại sau bước1. Chốt đầu vào/phạm vi trước, rồi nhãn cần thiết, pilot tuning, lỗi/test, freeze và biên bản M5. Rà187nhãn không phải gate riêng do kế hoạch quy định; kết quả đánh giá chính thức thuộc E. Không bắt hoàn thành toàn bộ thực nghiệm cuối trước khi được chuyển E, nhưng phải có bộ đầu vào/cấu hình/kế hoạch thực thi được.

**Audit tiếp theo:** [kiểm2và thực hiện3](report/stage-d-step2-step3-2026-10-25.md) xác nhận2đủ cho pilot12local, không chờ hoàn tất gatequality/final củaM5để rà nguồn tuning. Đã rà164fieldlabels/7docs và4pairreferences Codex, validator/hash đạt, transferfalse/humanpending. Bước1–3xong trong phạm vi local; các công việc pilot cần thiết, lỗi/test, finalfreeze/giao thức/dự toán và biên bản M5vẫn chưa chốt. Không chuyển Codexreview thành representativequality hoặc humanreview.

**Bổ sung 23/10 sau xác nhận chủ dự án:** đầu vào đối soát đã hoàn thành (21 requests/17.316 tokens khớp B + C); guard dùng baseline được chủ dự án đối chiếu rồi kiểm số dư mới, không bỏ guard hoặc cấp lại ngân sách. Probe writing vi/en nhỏ đã chạy như mục 1; ledger hiện còn **1,97 USD**. Hóa đơn chính xác/quy đổi CNY chưa xác minh. [Report tuần tự](report/stage-d-2026-10-23.md#6-probe-writing-that-va-ban-giao-ra-soat).

1. Search đã được chủ dự án chốt, chạy hai requests, rà hai nhóm đạt và đối soát Overview đúng 2 credits; cap đã hết, không gọi thêm. Email local tới owner đã kiểm chứng và đối soát Usage, vẫn giữ unknown đầu/allowance0; không tự gửi lại hoặc mở rộng sender/recipient.
2. Đã đối soát và hoàn thành workflow writing thật 2/2 synthetic: owner rà dữ kiện/accept/PDF/lưu private, Việt version3 và Anh version2/revision1. Kiểm hash/text/render/nguồn/public selection đạt; mẫuAnh chuẩn hóa phone trống thành null và PDF vẫn dùng tiêu đề mục Việt của simple-v1. Mọi probe tiếp cần đối soát ledger/số dư mới; key không thiếu, trần 2 USD toàn giai đoạn không cấp lại.
3. Rà chất lượng viết/nguồn học trên dữ liệu hợp lệ; hiện synthetic và nhãn Codex không chứng minh chất lượng thực tế. Ngày25/10 owner giao Codex rà23nhãn nguồn của một CVAnh tuning:21giữ nguyên,2sửa locator Git/C,0mơhồ; nhãn/manifest subset mới được kiểm offline, corpus gốc giữ nguyên. Đây là rà bằng Codex, human review vẫn pending; M3 vẫn thiếu đầu vào đại diện có quyền/người rà. [Report mục14](report/stage-d-2026-10-23.md#14-codex-ra-23-nhan-nguon-theo-uy-quyen).
4. Chốt bản method và danh sách nhóm final sau khi đầu vào hợp lệ. Manifest D là **candidate**, chưa phải final freeze/acceptance. Không mở/tune trên holdout cuối để hoàn tất chức năng.

Chưa deploy, commit, push hoặc xác nhận CI của working tree D. Các kiểm chứng Docker là local build, không là bản phát hành.

Chuẩn bị cấu hình: chép [template readiness](evaluation/stage-d-provider-readiness.template.json) vào `.local/`, chỉ ghi trạng thái và hạn mức; giữ key/sender/recipient thật ở backend. Từ repository root chạy `python scripts/check-stage-d-readiness.py .local/<manifest>.json` bằng Python đã cấu hình; exit 2 nghĩa còn đầu vào chờ, không gửi request nào. Self-tests: `python -m unittest scripts/test_stage_d_readiness.py`. Manifest đạt chỉ xác nhận dữ liệu khai báo đủ để chuẩn bị probe, không chứng minh provider thật hoặc mở thêm ngân sách.
