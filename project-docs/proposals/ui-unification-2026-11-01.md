# Đề xuất đồng bộ giao diện CareerGapAI

Ngày 01/11/2026 · Dùng skill `ui-ux-pro-max` · Trạng thái: **đề xuất để chủ dự án xem xét**.

## 1. Phương án chọn

Phát triển giao diện theo hướng **workspace tuyển dụng sáng, xanh trầm, dễ đọc, có bằng chứng**. Tiếp tục dùng nhận diện hiện tại; cải thiện thứ bậc thông tin, kích thước chữ, bố cục tác vụ và cách trình bày trạng thái trên toàn hệ thống.

Đồng bộ nghĩa là cùng một tác vụ có cùng tên gọi, màu, vị trí và hành vi ở mọi trang. Bố cục thay đổi theo nhiệm vụ: tìm việc là danh sách; soạn CV là trình biên tập; quản trị là bảng/danh sách nghiệp vụ. Không ép mọi màn hình thành cùng một bộ card.

**Bản minh họa:** [mở HTML](../../output/ui-ux-pro-max-20261101/index.html). Có chuyển vai trò, 13 view điều hướng, nhóm tab, trạng thái loading/rỗng/lỗi/AI tạm dừng và chế độ điện thoại. Dữ liệu minh họa; các nút nghiệp vụ chỉ giải thích phương án, không thực hiện giao dịch thật.

Phạm vi yêu cầu lần này là đề xuất. Release20261031 và source freeze vẫn là Defense Baseline; chưa áp dụng thiết kế này vào ứng dụng, chưa thay thế style guide hiện hành hoặc thay đổi nghiệm thu M6.

## 2. Cơ sở khảo sát và giới hạn

- Đọc `frontend/package.json`: React 19.3.1, TypeScript, Vite, Tailwind 4.3.3, CSS chung; Geist/Noto Sans, Phosphor, React PDF đã có. Phương án dùng lại các nền tảng này.
- Rà 13 view trong [navigation.ts](../../frontend/src/navigation.ts), shell trong [workspace.tsx](../../frontend/src/workspace.tsx), [ui.tsx](../../frontend/src/ui.tsx), [style.css](../../frontend/src/style.css), các module jobs/CV/profile/matching/writing/learning/admin.
- Đối chiếu đặc tả 01, stack 02, roadmap 03, database 04 và quyết định M6 ngày 01/11.
- Đọc [style guide](../frontend-style-guide.md) cùng [báo cáo thực hiện 29/10](../report/frontend-style-guide-2026-10-29.md). Drawer mobile, modal, field validation, badge AI và sub-nav tuyển dụng **đã có**; không ghi lại chúng thành lỗi còn thiếu.
- Xem ảnh lịch sử `output/playwright/multiview-step2/jobs-laptop.png`, `builder-mobile.png`, `output/design-guide-20261029/recruiter-tabs.png`. Đây là hình minh họa lịch sử, không phải lần kiểm thử ứng dụng mới ngày 01/11.
- Prototype là bản kiểm tra ý tưởng có dữ liệu hư cấu. Rà mã nguồn chỉ xác định cấu trúc và rủi ro; chưa đo completion time, chưa phỏng vấn người dùng, chưa chứng nhận WCAG cho ứng dụng thật.

## 3. Kết quả từ skill và quyết định áp dụng

Tra offline, không gửi dữ liệu riêng tư:

| Tra cứu | Kết quả liên quan | Quyết định |
|---|---|---|
| `recruitment job portal --design-system --variance 3 --density 5` | Đúng category `Job Board/Recruitment`; Minimalism & Swiss Style; tìm kiếm/bộ lọc cần rõ | Chọn phong cách gọn và hệ thống khoảng cách. Mẫu funnel chỉ phù hợp giới thiệu, không áp vào workspace tác nghiệp |
| Kết quả typography trong lần tạo system | Trả Inter/Playfair theo mẫu mobile/editorial, không khớp workspace này | Không dùng; tra lại riêng typography một lần |
| `dashboard readable sans serif --domain typography` | Fira Code/Fira Sans cho dashboard dữ liệu; không có kết quả xác thực cho cặp font Việt hiện có | Giữ Geist/Noto Sans theo codebase. Đây là quyết định theo dự án, không gọi là font đã được skill xác thực |
| `forms navigation accessibility --stack react` | Semantic HTML, quản lý focus và form | Dùng nguyên tắc semantic/focus; không áp máy móc gợi ý controlled input cho mọi trường vì hiện có FormData/ValidatedForm hoạt động |
| `error summary validation --domain ux` | Focusable Error Summary | Đề xuất tổng hợp lỗi có link tới field cho form dài, bổ sung lỗi inline |
| `badge chip label wraps --domain ux` | Compact Label Overflow | Nhãn trạng thái ngắn; tên kỹ năng dài có cách xem đầy đủ, tập chip được wrap |

Palette xanh dương trả từ generator không thay thế xanh thương hiệu đang dùng. Skill là nguồn gợi ý; nghiệp vụ, mã nguồn và quyết định đã xác nhận có ưu tiên cao hơn.

## 4. Các vấn đề cần giải quyết xuyên suốt

| Ưu tiên | Quan sát có cơ sở | Cải thiện chung |
|---|---|---|
| P1 | `style.css` đang có body/input 13px, label 12px; thông tin matching còn 12px. Ảnh builder mobile có form kéo dài nhiều màn hình | Nâng body/input 16px, control/label 14px, metadata 13px. Chia form theo nhóm với điều hướng mục, giữ dữ liệu khi chuyển mục |
| P1 | Heading cấp trang hiện có biến thể: Trung tâm CV 24–32px, tìm việc và `.card h1` 21px | Một `PageHeader` 28/32px, cùng vị trí breadcrumb, mô tả và action. Title của panel 18–20px |
| P1 | Copy người dùng lẫn thuật ngữ kỹ thuật: `Candidate`, `Recruiter`, `Revision`, `parser`, `cv-fields-v3`, `BASELINE L09`, rubric | Hiển thị tiếng Việt theo tác vụ; giữ nguồn/phương pháp trong disclosure “Chi tiết phân tích”. Nhãn “Mô phỏng” và cảnh báo quan trọng luôn nhìn thấy |
| P1 | CV riêng cùng nhóm hành động phiên bản/trích xuất/công khai; danh sách, dữ liệu và phiên bản có ý nghĩa khác nhau | Bộ chọn nguồn CV + version nhất quán, panel quyền hiển thị riêng, phân biệt dữ liệu đã xác nhận với file PDF đã lưu |
| P1 | Màn recruiter đã có tab nhưng trong tab Tin vẫn render form dài trước danh sách; khối “Tài khoản nhà tuyển dụng” chiếm diện tích đầu màn | Danh sách tin trước; form mở khi tạo/sửa. Một dải ngữ cảnh ngắn thay khối nhắc quyền lặp lại |
| P2 | Badge AI riêng đã có; badge trạng thái tin, đơn và điều khiển admin vẫn có cách đặt tên/nhấn mạnh khác nhau | Một từ điển trạng thái với chữ + icon + semantic tone, dùng chung cho badge/filter/timeline |
| P2 | Màu đã có token nhưng `workspace.tsx` vẫn có hex trực tiếp; CSS có các nhóm bổ sung/override theo đợt | Chuyển giá trị cùng vai trò sang token hiện hữu; tách cấu trúc layer base/components/pages, giảm override cạnh tranh |
| P2 | Lịch sử matching đã có nút GET latest nhưng nằm cùng dãy nút tạo phân tích mới | “Xem kết quả đã lưu” cạnh lịch sử, “Phân tích mới” riêng và có tiền điều kiện rõ; giữ nguyên API/quyền/idempotency |

P1/P2 ở đây là ưu tiên thiết kế, không phải kết luận lỗi bảo mật hay điều kiện mở lại M6.

## 5. Quy chuẩn chung cần chốt trước khi sửa từng trang

### Màu và trạng thái

Tiếp tục sử dụng token semantic hiện có trong `style.css`. Bảng dưới là giá trị đề xuất; success/warning/error bổ sung vai trò thay vì dùng màu thương hiệu cho mọi ý nghĩa.

| Vai trò | Màu | Nền | Quy tắc |
|---|---|---|---|
| Primary | `#245C48` | trắng | CTA chính, item đang chọn |
| Canvas/surface | `#F7F8F4` / `#FFFFFF` | — | Nền workspace và bề mặt nội dung |
| Text | `#29362E` | canvas | Tiêu đề, nội dung chính |
| Text phụ | `#67736A` | trắng/canvas/`#F4F6F1` | Metadata quan trọng; không giảm opacity cả khối |
| Border trang trí | `#E0E5DD` | trắng | Chia vùng; không làm chỉ báo trạng thái duy nhất |
| Control border | `#7A877D` | trắng | Đề xuất cho viền cần giúp nhận diện input/control; kiểm lại từng nền |
| AI đề xuất | `#2F5586` | `#EEF2F8` | Kèm chữ “AI đề xuất · cần rà soát” |
| Thành công/đã xác nhận | `#41654D` | `#EDF2E9` | Sau khi API/hành động áp dụng xác nhận thành công |
| Cần rà/chờ | `#7A4F00` | `#FFF5DB` | Có chữ nêu hành động tiếp theo |
| Lỗi/nguy hiểm | `#9B312B` | `#FFF1F0` | Lỗi tác vụ và thao tác phá hủy phân biệt bằng copy |
| Trung tính | `#5A6374` | `#F1F4F8` | Chưa bắt đầu/đã đóng/chưa đánh giá, có nhãn cụ thể |

Tính từ mã hex: primary/trắng 7,779:1; text/canvas 11,850:1; secondary/canvas 4,647:1; secondary/nền phụ 4,555:1; AI 6,761:1; success 5,786:1; warning 6,564:1; error 6,657:1; neutral 5,483:1. Đây là phép tính màu phẳng, không thay kiểm tra rendered UI, opacity, hover hay focus thực tế.

### Chữ, khoảng cách, kích thước

- Font chung: Geist; Noto Sans fallback tiếng Việt. Dùng font đã đóng gói local, kiểm glyph và weight khi triển khai; PDF giữ hệ font xuất tài liệu riêng đang có.
- Type scale: metadata 13/20; label/control 14/22; body/input 16/24; panel 20/28; page title 28/36 mobile và 32/40 desktop. Business status không dùng 11px.
- Spacing tokens: 4, 8, 12, 16, 24, 32, 48px. Panel padding 24px desktop / 16px mobile; khoảng cách section 24–32px.
- Bo góc: control 8px, surface 12px; badge 6px. Shadow chỉ cho overlay và bề mặt thực sự nổi.
- Icon: Phosphor regular, 20px chung, 16px trong metadata; hit area nút icon 44×44 CSS px.
- Control cao 44px; textarea tối thiểu 120px. Form đọc dài tối đa 720px. Một primary cho một nhóm quyết định; tác vụ độc lập ở nhóm khác vẫn có primary riêng.
- Motion đề xuất: phản hồi button 120ms, chuyển panel 180ms; không thêm GSAP cho các thao tác này; reduced motion trả về chuyển trạng thái trực tiếp.

### Shell và responsive

- Sidebar 240px, topbar khoảng 64px, content tối đa 1280px và dùng cùng lề; giữ hash routes hiện tại.
- Tất cả view có PageHeader: breadcrumb → H1 → 1 câu mô tả → action chính. Tab con dùng cùng chiều cao và active treatment.
- Desktop ≥1280px: list/detail khi cần; 768–1279px: hai cột chỉ khi đủ chiều rộng tối thiểu; <768px: một cột. Sidebar hiện drawer dưới 1024px được giữ.
- Mobile topbar gọn; tên trang có thể xuống dòng; thao tác account nằm trong menu. Nhóm tab CV dài cần select có label hoặc menu “Các mục”, tránh tab cuối bị che mà không có gợi ý.
- Sticky chỉ khi panel vừa viewport; luôn chừa scroll-padding, khoảng trống cho thanh tác vụ và safe area. Không ghim nút làm che lỗi hoặc bàn phím ảo.
- Tìm kiếm, bộ lọc và phân trang dùng chung cấu trúc. Chuyển detail có nút quay về danh sách và giữ bộ lọc/scroll/state.

## 6. Ma trận đề xuất cho toàn bộ 13 view

| View hiện có | Tên hiển thị đề xuất | Cấu trúc và hành động chính | Điều phải bảo toàn |
|---|---|---|---|
| `#jobs` | Khám phá việc làm | Search bar → filter chips → danh sách → chi tiết theo lựa chọn. Tab “Tất cả việc làm / Theo CV / Đã lưu”; “Đã lưu” dùng dữ liệu đã có | Khách chỉ xem JD. Tìm thông thường vẫn dùng khi AI tắt. Baseline từ khóa không đổi thành % phù hợp |
| `#access` | Đăng nhập | Form 440–480px, bốn mode đăng nhập/đăng ký/quên/đặt lại cùng template. Vai trò ghi Ứng viên/Nhà tuyển dụng; helper mật khẩu ngay field | Giữ phân biệt lỗi invalid/expired, password policy, network/server và đường xin link mới; không lộ tài khoản tồn tại |
| `#private-resumes` | CV của tôi | Danh sách CV + version → panel tài liệu được chọn → trạng thái đọc/xác nhận. Upload là primary; nhóm Công khai có xác nhận riêng | Tải lên và tạo version luôn private. Public selection tối đa một. Không đổi nhầm version đang dùng |
| `#cv-builder` | Soạn & cải thiện CV | Stepper trình bày 3 giai đoạn: Nội dung → Rà đề xuất → PDF & Lưu. Mục lục section; desktop editor/preview, mobile đổi panel có nhãn | Đổi panel giữ draft, revision binding và recovery. Chấp nhận đề xuất, áp dụng draft, tạo PDF, lưu version là các bước riêng |
| `#profile` | Hồ sơ & chia sẻ | Nhóm Cá nhân / Kinh nghiệm / Mong muốn / Quyền chia sẻ. Checklist chia sẻ có giải thích đối tượng xem | Chia sẻ profile khác công khai CV; không gộp toggle quyền |
| `#applications` candidate | Đơn ứng tuyển | Danh sách theo trạng thái → detail + CV đã nộp + timeline; tab Thông báo dùng cùng page header | Snapshot lúc nộp; “Qua sàng lọc” không phải “Đã tuyển”; rút đơn không xóa ngay snapshot |
| `#recruiter-company` | Công ty | Danh sách công ty → form tạo/sửa theo lựa chọn, nhãn field chung | Company ownership và rowVersion hiện có |
| `#recruiter-jobs` | Tin & JD | Danh sách tin đứng trước; “Tạo tin” mở form theo nhóm; chi tiết tin có yêu cầu cần rà soát | Không tự publish khi lưu; phiên bản JD và trạng thái draft/published/closed/archived giữ nguyên |
| `#recruiter-applicants` | Ứng viên theo tin | Chọn tin rõ ở đầu → danh sách ứng viên → detail hồ sơ → ghi chú, history, action sàng lọc. Mobile list/detail tuần tự | Tách context ứng tuyển khỏi kho CV công khai; AI không quyết định sàng lọc |
| `#shared-profile` | Hồ sơ được chia sẻ | Dùng chung CandidateSummary và chi tiết field được cấp quyền; lookup hiện có vào nhóm “Mở hồ sơ theo mã” | Không tự tạo global search người dùng nếu chưa có API; thiếu quyền/không có hồ sơ không tiết lộ dữ liệu riêng |
| `#public-resumes` | Kho CV công khai | Search/filter → resume cards có summary → detail phù hợp vai trò; nguồn JD đặt trong recruiter context | Candidate chỉ nhận bản đã ẩn liên hệ; recruiter theo quyền. Chưa có bản an toàn thì trạng thái unavailable, không trả file gốc |
| `#account` | Tài khoản & bảo mật | Thông tin tài khoản → đổi mật khẩu → vùng nguy hiểm. Chờ xóa có page riêng nêu hạn chót và nút hủy | Thu hồi quyền ngay, thời hạn hủy 7 ngày và backup tối đa 14 ngày tiếp theo. Không lẫn lịch sử AI với xóa tài khoản |
| `#administration` | Quản trị | Cùng TabNav cho Tài khoản/Tin/Báo cáo/Nhật ký/Hạn mức & lỗi. Lý do xử lý nhập ở đúng item/dialog | Không có đường đọc CV riêng; USD/credits/email là đơn vị riêng, hold/unknown không thành 0 |

`#applications` của recruiter chỉ hiển thị thông báo theo quyền hiện tại; prototype đổi nhãn theo vai trò. Các tab/action đề xuất là tổ chức lại UI; chỉ sử dụng chức năng đã có, không thêm tuyển dụng sau sàng lọc.

## 7. Những vùng lồng nhau phải thống nhất cùng trang chính

### Đọc CV và rà dữ liệu

Bố trí file/version, trạng thái tác vụ, dữ liệu gợi ý và nút xác nhận theo cùng thứ tự. Mỗi field hiện nguồn trích dẫn hoặc “Bạn tự khai”; field chưa biết để trống/unknown. Chuyển `revision`, schema và parser vào “Chi tiết xử lý”; không bỏ khả năng đối chiếu nguồn. Luồng legacy/trích xuất thử tách khỏi CTA “Đọc CV” chính, có nhãn rõ; mapping endpoint cụ thể phải được xác nhận khi triển khai.

### Đối sánh CV–JD hai chiều

Một `MatchResult` dùng cho ứng viên tìm việc, recruiter tìm CV công khai và phân tích hồ sơ đã nộp. Header luôn ghi CV/version + JD/version + context. Điểm từ khóa giữ thang 0–1 và tên “Điểm từ khóa”; điểm đối chiếu giữ /100, mô tả thử nghiệm và không gọi là xác suất trúng tuyển.

Nội dung gồm tóm tắt → tiêu chí có bằng chứng/một phần/chưa có bằng chứng/chưa đánh giá → trích dẫn nguồn. `not_assessed` không xếp vào nhóm thiếu kỹ năng. Failed/timeout/permission-revoked giữ chưa có điểm; không tô đỏ như bị loại hoặc gán 0. Kết quả lưu được xem lại qua GET hiện có; không ngầm tạo job khi đổi tab hoặc mở detail.

### Viết CV và tài liệu học

Writing: đối chiếu dữ kiện → chỉnh proposal → quyết định → áp dụng draft. Nhãn “Đã chấp nhận đề xuất” và “Đã đưa vào bản nháp” là hai thông tin khác nhau. “Đã lưu phiên bản CV” chỉ xuất hiện sau khi lưu thành công. Không dùng một badge xanh chung cho cả ba.

Learning: nguồn học đặt dưới tiêu chí gap hợp lệ trong self-context, có tiêu đề/nguồn/liên kết và ghi rõ tham khảo. “Chưa có nguồn”, search tạm dừng/hết credits và lỗi riêng biệt. Không tạo lại search khi mở lại kết quả.

## 8. Component và từ điển dùng chung

| Thành phần | Hợp đồng hiển thị/hành vi |
|---|---|
| PageHeader | H1, mô tả, breadcrumb, action group; mọi view dùng cùng spacing/type |
| TabNav | Các route lân cận hoặc local panel; đúng `aria-current`/tab semantics, keyboard; mobile có lựa chọn tương đương |
| Button / IconButton | primary/secondary/quiet/danger; loading giữ độ rộng, không gửi lặp; icon có accessible name |
| Field / FormSection / ErrorSummary | Label và helper ở cùng vị trí; required/invalid rõ; server error không đoán field; focus summary sau submit lỗi |
| StatusBadge | Label + tone + optional icon; quyền, pipeline AI và trạng thái đơn không suy ra từ cùng một boolean |
| SourceContext | Tên CV/JD, version, thời điểm, phạm vi quyền; compact ở list, đầy đủ ở detail |
| DataState | idle/loading/empty/error/forbidden/unavailable/off; cùng vị trí và action khắc phục phù hợp |
| SearchToolbar / Pagination | Số kết quả có trạng thái; tải lỗi không hiển thị “0 kết quả”; giữ filter khi mở/đóng detail |
| ConfirmDialog | Dùng `Dialog` đang có; nêu đối tượng/hệ quả, xác nhận có nội dung, trả focus; hủy không mutation |
| AiEvidencePanel | Dùng `AiBadge` hiện có; tóm tắt, bằng chứng, nội dung chưa xác minh, lịch sử, phương pháp |

Status dictionary đề xuất:

- Hồ sơ: Riêng tư / Đang công khai / Đã gửi trong đơn. Một version có thể đồng thời đang công khai và đã gửi; dùng nhãn độc lập.
- Dữ liệu: Chưa đọc / Đang chờ / Đang đọc / Chờ rà soát / Đã xác nhận / Chưa xử lý được.
- Đơn: Đã nộp / Đang xem xét / Qua sàng lọc / Không phù hợp / Đã rút. Luồng chuyển trạng thái theo đặc tả; không thêm nhãn “Đã tuyển”.
- Tin: Bản nháp / Đang đăng / Đã đóng / Đã ẩn. “Đã hết hạn” là điều kiện nhận hồ sơ, không tùy tiện ghi đè trạng thái lưu.
- Hệ thống: AI tạm dừng / Hết hạn mức / Mất kết nối / Quyền đã thay đổi / Chưa có dữ liệu. Disabled action luôn có lý do đọc được.

## 9. Cách triển khai khi owner mở thay đổi giao diện

1. **Foundation:** chốt token/type/spacing, chuẩn PageHeader/Button/Badge/Field/DataState trong StyleGuide; chọn một catalog nguồn chân lý. Dùng lại `ui.tsx`, không thay toàn bộ component library.
2. **Shell:** cập nhật header/sidebar/tab/responsive trên tất cả 13 view bằng component chung. Giữ hash guards, state trong `PersistentView`, back/scroll và draft đang nhập.
3. **Ba luồng chính:** tìm việc/ứng tuyển → CV/rà dữ liệu/writing/PDF → recruiter công ty/tin/ứng viên. Mỗi lượt áp dụng cùng token/component rồi mới chỉnh bố cục riêng.
4. **Account/admin và trạng thái:** đưa vào cùng mẫu; hoàn thiện mobile, lỗi/quyền/AI off; dữ liệu hạn mức không bị làm đẹp sai ý nghĩa.
5. **Nghiệm thu UI của phiên bản mới:** kiểm tra matrix bên dưới trên source/runtime mới; cập nhật guide và bằng chứng. Không gán kết quả test release31 cho UI đã sửa.

Điểm chạm dự kiến: `style.css`, `ui.tsx`, `workspace.tsx`, `navigation.ts`, `main.tsx`, `jobs.tsx`, `jobDiscoveryWorkspace.tsx`, `publicResumes.tsx`, `resumeBuilder.tsx`, `fullCvReview.tsx`, `matching.tsx`, `pairMatching.tsx`, `writing.tsx`, `learning.tsx`, `administration.tsx`. Backend/schema/scorer là dependency cần rà hợp đồng, không mặc định phải sửa cho redesign này.

## 10. Tiêu chí nghiệm thu đề xuất

- 13 view cùng PageHeader, font, spacing và component variants; admin tabs, auth modes và nested AI panels cũng nằm trong matrix.
- Hành động cùng nghĩa có cùng label/tone/position. Mỗi route chỉ có một H1; active navigation đúng; form state/draft không mất khi đổi panel.
- Kiểm 320/390/768/1024/1440px, zoom 200% và reflow; không che nút/field khi mở bàn phím hoặc sticky. Bảng dữ liệu có container cuộn có tên khi cần.
- Tab/Shift+Tab/Escape, focus return, error summary, screen-reader labels và trạng thái live được kiểm thực tế; chọn text bằng chuột không bị card navigation chiếm click.
- Body text tương phản ≥4,5:1; text lớn ≥3:1. Tính từng cặp màu và trạng thái thật. Mục tiêu thiết kế nút chính 44×44 CSS px; **WCAG 2.2 AA 2.5.8 là 24×24 CSS px hoặc các ngoại lệ**, không nhầm mục tiêu 44px của dự án thành toàn bộ yêu cầu AA.
- State coverage: loading/empty/network error/validation/conflict/forbidden/unavailable/AI off/quota hết/retry/cancel. Không dùng dữ liệu giả để làm đầy màn hình production.
- Các nhánh quyền CV, snapshot ứng tuyển, AI không tự đổi sàng lọc, không call khi mở lịch sử, PDF→private version vẫn đúng theo baseline.
- Không dùng mockup hoặc cặp màu đạt ngưỡng để tuyên bố ứng dụng đã đạt WCAG hoặc UI cải thiện tỷ lệ hoàn thành tác vụ; cần đo sau triển khai.

## 11. Nguồn và bằng chứng cho đề xuất

- [Skill cài local](C:/Users/lichl/.codex/skills/ui-ux-pro-max/SKILL.md) và dataset tra cứu offline.
- [WCAG 2.2: Contrast Minimum](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).
- [WCAG 2.2: Target Size Minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html).
- [WCAG 2.2: Focus Not Obscured Minimum](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html).
- [Style guide thực hiện 29/10](../report/frontend-style-guide-2026-10-29.md), [nghiệm thu M6](../report/m6-internal-acceptance-2026-11-01.md), [source freeze](../evaluation/source-freeze-2026-11-01.json).

Các tiêu chí trên là kế hoạch kiểm tra cho lần triển khai tương lai. Lần công việc này chỉ tạo tài liệu và prototype minh họa; không chạy lại test ứng dụng hoặc phát sinh provider calls.
