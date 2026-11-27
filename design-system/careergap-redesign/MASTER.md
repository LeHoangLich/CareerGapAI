# CareerGap AI — Editorial AI SaaS Design System (`MASTER.md`)

> Phiên bản nâng cấp UX/UI (giữ nguyên mã nguồn Defense Baseline 01/11/2026 đang đóng băng).
> Phương pháp: SkillUI baseline extraction (`design-system/careergap-baseline`) + `ui-ux-pro-max` Editorial AI SaaS.

---

## 1. Triết lý & Định hướng Thẩm mỹ (Editorial AI SaaS)

- **Bản sắc thương hiệu**: Giữ nguyên DNA xanh rừng (**Forest `#245C48`**) trên nền giấy ấm (**Warm Paper `#F7F8F4`**) và bề mặt trắng tinh (**Pure Surface `#FFFFFF`**) để tạo cảm giác tin cậy, chuyên nghiệp, không chói mắt khi đọc CV/JD dài.
- **Tách bạch lớp AI**: Mọi tính năng và bằng chứng do AI sinh ra (so khớp kỹ năng, trích dẫn bằng chứng từ CV, gợi ý viết lại câu) dùng dải màu **AI Slate-Blue (`#2F5586` / `#EEF2F8` / `#C7D6E8`)** để người dùng phân biệt rõ dữ liệu gốc và nhận định AI.
- **Tương phản & Khả năng đọc**: Văn bản chính đạt độ tương phản ≥ 4.5:1 (`#1F2B24` trên `#FFFFFF` và `#F7F8F4`); tránh dùng xám quá nhạt cho body text.
- **Không trang trí thừa**: Không gradient nền lòe loẹt, không backdrop-blur nặng, ưu tiên viền mảnh 1px (`#DDE3DA`), khoảng trắng rõ ràng theo lưới 4/8px, và phân cấp thông tin bằng cỡ chữ + độ đậm + khoảng cách.

---

## 2. Hệ thống Token Màu sắc (Primitives → Semantic)

### Bảng màu Semantic (Light Mode)

| Nhóm | Token | Giá trị Hex | CSS Variable | Mục đích sử dụng |
|---|---|---|---|---|
| **Surfaces** | `bg/canvas` | `#F7F8F4` | `--color-bg` | Nền trang tổng thể (Warm Paper) |
| | `bg/surface` | `#FFFFFF` | `--color-surface` | Thẻ, panel, modal, sidebar, top nav |
| | `bg/surface-subtle` | `#F2F5EE` | `--color-surface-subtle` | Vùng phụ, nền hover nhẹ, input phụ |
| | `bg/ai-tint` | `#EEF2F8` | `--color-ai-bg` | Nền khối AI Evidence / Copilot |
| **Text** | `text/primary` | `#1F2B24` | `--color-text-primary` | Tiêu đề, nội dung chính |
| | `text/secondary` | `#526057` | `--color-text-secondary` | Mô tả phụ, metadata, label |
| | `text/muted` | `#6B776F` | `--color-text-muted` | Placeholder, caption, timestamp |
| | `text/inverse` | `#FFFFFF` | `--color-text-inverse` | Chữ trên nút Primary / AI / Danger |
| **Brand & AI** | `brand/primary` | `#245C48` | `--color-primary` | CTA chính, active nav, link |
| | `brand/primary-hover` | `#194735` | `--color-primary-hover` | Trạng thái hover của Primary |
| | `ai/accent` | `#2F5586` | `--color-ai-accent` | Nút AI, badge AI, điểm nhấn phân tích |
| | `ai/border` | `#C7D6E8` | `--color-ai-border` | Viền thẻ AI Evidence / AI Copilot |
| **Borders** | `border/default` | `#DDE3DA` | `--color-border` | Viền thẻ, ô nhập, đường chia chuẩn |
| | `border/subtle` | `#E9EDE5` | `--color-border-subtle` | Đường kẻ mảnh bên trong thẻ/bảng |
| | `border/strong` | `#C5CFC2` | `--color-border-strong` | Viền focus / thẻ đang chọn |
| **Status** | `status/matched-bg` | `#E8F4EC` | `--color-matched-bg` | Nền badge kỹ năng Khớp (Matched) |
| | `status/matched-text` | `#1F513B` | `--color-matched-text` | Chữ badge Khớp |
| | `status/partial-bg` | `#FBF5E6` | `--color-partial-bg` | Nền badge Khớp một phần (Partial) |
| | `status/partial-text` | `#846224` | `--color-partial-text` | Chữ badge Khớp một phần |
| | `status/gap-bg` | `#FBEFEE` | `--color-gap-bg` | Nền badge Thiếu hụt (Gap) / Danger |
| | `status/gap-text` | `#9E3B33` | `--color-gap-text` | Chữ badge Thiếu hụt / Danger |

---

## 3. Typography (Hỗ trợ đầy đủ Tiếng Việt)

- **Font chính**: `Inter` (hoặc `Noto Sans` / `Geist` trong code) — đảm bảo hiển thị dấu Tiếng Việt sắc nét ở mọi trọng lượng (`Regular`, `Medium`, `Semi Bold`, `Bold`).
- **Thang chữ (Type Scale)**:
  - `Display/32`: 32px / Line-height 40px / Bold / Tracking `-0.02em` (Hero Heading)
  - `H1/24`: 24px / Line-height 32px / Semi Bold / Tracking `-0.015em` (Tiêu đề màn hình)
  - `H2/20`: 20px / Line-height 28px / Semi Bold / Tracking `-0.01em` (Tiêu đề khối / Panel)
  - `H3/16`: 16px / Line-height 24px / Semi Bold (Tiêu đề thẻ / Job Title)
  - `Body/14`: 14px / Line-height 22px / Regular (Nội dung chính, mô tả JD, trích dẫn CV)
  - `Body/14 Medium`: 14px / Line-height 20px / Medium (Nav item, nút, tên ứng viên)
  - `Label/12 SemiBold`: 12px / Line-height 16px / Semi Bold (Badge, tag kỹ năng, label input)
  - `Caption/12`: 12px / Line-height 18px / Regular (Metadata, thời gian, chú thích)

---

## 4. Spacing, Radii & Elevation

- **Spacing Scale (4/8px grid)**: `4`, `8`, `12`, `16`, `20`, `24`, `32`, `48` px.
- **Border Radius**: `6px` (button, input), `8px` (inner box, evidence quote), `12px` (card, panel, drawer), `999px` (pill badge, skill chip).
- **Elevation**:
  - `Shadow/SM`: `0 1px 2px rgba(31, 43, 36, 0.06)` (thẻ mặc định)
  - `Shadow/MD`: `0 4px 12px rgba(31, 43, 36, 0.08)` (drawer, dropdown, thẻ đang chọn)

---

## 5. Kiến trúc Điều hướng (Hybrid Shell)

1. **Public Top Navigation (`1440 × 68px`)**: Dùng cho `Home / Landing`, `Job Discovery & Detail`, `Community Career Hub`.
   - Trái: Logo CareerGap AI + 3 mục chính (`Khám phá việc làm`, `Phân tích AI`, `Cộng đồng`).
   - Phải: Nút `Đăng nhập` (Ghost/Secondary) + `Bắt đầu miễn phí` (Primary).
2. **Workspace Left Sidebar (`240px`)**: Dùng cho `AI Gap Analysis`, `CV Builder & AI Writing`, `Recruiter Screening`.
   - Điều hướng phân nhóm rõ theo vai trò (Ứng viên / Nhà tuyển dụng), hiển thị thẻ trạng thái tài khoản & AI quota gọn gàng ở đáy sidebar.

---

## 6. Danh mục 6 Màn hình Trọng tâm (Desktop 1440px)

1. **01 · Home / Landing**: Editorial Hero + thanh tìm kiếm việc làm nhanh + Preview thẻ AI Gap Analysis + 3 bước quy trình + lưới 3 việc làm CNTT nổi bật + Footer.
2. **02 · Job Discovery & Detail**: Thanh lọc ngang đa tiêu chí + Master-Detail 2 cột (danh sách `JobCard` bên trái, chi tiết JD + ước tính khớp + CTA `"So khớp với CV của tôi"` bên phải).
3. **03 · AI Gap Analysis & Pair Matching**: Sidebar Ứng viên + bộ chọn cặp CV × JD + dải 4 KPI (Điểm khớp 78%, Khớp 8/11, Một phần 2, Thiếu hụt 1) + danh sách `AiEvidenceCard` trích dẫn nguyên văn từ CV + Lộ trình học tuần bên phải.
4. **04 · CV Builder & AI Writing Assistant**: Sidebar Ứng viên + cấu trúc 3 cột (Mục lục CV | Khung soạn thảo & xem trước ATS | Ngăn kéo AI Writing Copilot so sánh Trước/Sau có căn cứ fact gốc).
5. **05 · Recruiter Screening Workspace**: Sidebar Nhà tuyển dụng + dải KPI tin tuyển dụng + bảng ứng viên có Match Score & kỹ năng thiếu hụt + Quick-Review Drawer xem bằng chứng AI và duyệt/loại nhanh.
6. **06 · Community Career Hub**: Public Top Nav + bố cục 3 cột (Bộ lọc chủ đề | Luồng bài viết chuyên môn CNTT | Nút đăng bài & Kỹ năng đang được quan tâm).
