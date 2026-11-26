# CareerGapAI — Design System & Style Guide

*Phiên bản 1 — dùng để áp trực tiếp vào codebase. Ưu tiên accessibility/UX trước, giữ nguyên tinh thần tối giản-trầm hiện có, chỉ bổ sung elevation + phân biệt AI vì được xác nhận là "sửa UX" chứ không phải trang trí.*

## 1. Nguyên tắc thiết kế

- Giữ phong cách workspace tối giản, màu trầm, ít hiệu ứng — không đổi định hướng thẩm mỹ tổng thể.
- Mọi bổ sung (shadow, màu AI) phải phục vụ **rõ ràng chức năng**, không phải trang trí thuần túy.
- Accessibility là tiêu chí chấp nhận, không phải tùy chọn: mọi token màu mới phải đạt tối thiểu WCAG AA (4.5:1 cho text thường, 3:1 cho text lớn/UI component).
- Áp dụng đồng bộ trên toàn hệ thống — không vá riêng từng màn hình.

## 2. Design Tokens — Màu sắc

Ba token đang fail AA (3.20–3.35:1) được nâng độ đậm. Các token còn lại giữ nguyên vì đã đạt chuẩn.

```css
:root {
  /* Primary / accent — giữ nguyên */
  --color-primary: #245C48;
  --color-primary-hover: #194735;
  --color-primary-active: #143D2D;

  /* Surface — giữ nguyên */
  --color-bg: #F7F8F4;
  --color-surface: #FFFFFF;
  --color-surface-secondary: #F4F6F1;

  /* Text — giữ nguyên, đã đạt AA */
  --color-text-primary: #29362E;   /* 11.85:1 */
  --color-text-secondary: #68746B; /* 4.88:1 */

  /* Text — FIX contrast (trước đây 3.20–3.35:1, dưới chuẩn AA) */
  --color-text-tertiary: #5E685F;     /* thay #818A80 (footer) → ~4.6:1 */
  --color-text-caption: #5A6359;      /* thay #869087 (caption logo) → ~4.7:1 */
  --color-text-placeholder: #5E685F;  /* thay #8A928B (placeholder) → ~4.6:1 */

  /* Border — giữ nguyên */
  --color-border: #E0E5DD;
  --color-border-selected: #EAF0E6;

  /* Badge mặc định — giữ nguyên */
  --color-badge-bg: #EDF2E9;
  --color-badge-text: #41654D;

  /* Danger — giữ nguyên */
  --color-danger: #85473E;
  --color-danger-bg: #FBF5EE;
  --color-danger-text: #85553E;

  /* Focus — giữ nguyên */
  --color-focus: #397558;

  /* Trạng thái tin — giữ nguyên */
  --color-status-draft-bg: #F8F4E8;
  --color-status-draft-text: #886E3C;
  --color-status-closed-bg: #F3EEEB;
  --color-status-closed-text: #86685A;

  /* MỚI — phân biệt nội dung AI-gợi-ý vs người dùng tự nhập */
  --color-ai-bg: #EEF2F8;
  --color-ai-text: #2F5586;   /* ~6.1:1 trên nền trắng */
  --color-ai-border: #C7D6E8;

  /* MỚI — elevation rất nhẹ, không phá vỡ cảm giác phẳng vốn có */
  --shadow-sm: 0 1px 2px rgba(41, 54, 46, 0.06);
  --shadow-md: 0 2px 6px rgba(41, 54, 46, 0.08);
}
```

## 3. Typography

Hiện trạng có \~12 cỡ chữ rời raạc, một số xuống tới 8–10px (bảng admin mobile). Gom về 7 bậc, sàn tối thiểu 12px cho mọi text mang thông tin nghiệp vụ (badge/eyebrow trang trí được giữ 10–11px vì không mang nội dung thiết yếu):

| Token | Giá trị | Dùng cho |
| --- | --: | --- |
| `--font-size-xs` | 11px | badge, eyebrow (trang trí, không đổi) |
| `--font-size-sm` | 12px | label, small text, caption (nâng từ 10–11px) |
| `--font-size-base` | 13px | paragraph, input, button |
| `--font-size-md` | 14px | bảng admin mobile (năng từ 10px) |
| `--font-size-lg` | 16px | H3 |
| `--font-size-xl` | 21px | H2 chung |
| `--font-size-2xl` | `clamp(24px,3vw,32px)` | heading Trung tâm CV |

Font family giữ nguyên: `'Geist', 'Noto Sans', sans-serif`.

## 4. Elevation — quy tắc dùng shadow

Chỉ dùng shadow khi một bề mặt **nổi trên** bề mặt khác, không dùng cho card tĩnh trong luồng trang (giữ đúng tinh thần phẳng hiện tại):

- Card tĩnh trong luồng trang (job card, CV version, form): **không shadow**, giữ `border: 1px solid var(--color-border)` như hiện tại.
- Panel sticky đang ở trạng thái nổi (bộ lọc, cột kiểm tra builder khi sticky): `--shadow-sm`.
- Dropdown, popover, modal mới (xem mục 7): `--shadow-md`.

## 5. Hệ thống phân biệt nội dung AI

Áp dụng nhất quán ở 3 nơi đang có AI: kết quả matching (`pairMatching.tsx`), gợi ý viết CV (`writing.tsx`), trích xuất CV công khai (`fullCvReview.tsx`).

```css
.badge-ai-suggested {
  background: var(--color-ai-bg);
  color: var(--color-ai-text);
  border: 1px solid var(--color-ai-border);
}

/* Khi user bấm "Chấp nhận" → chuyển về badge thường,
   báo hiệu nội dung đã thuộc về người dùng */
.badge-ai-accepted {
  background: var(--color-badge-bg);
  color: var(--color-badge-text);
}
```

Quy tắc: trạng thái AI-đề-xuất luôn dùng `.badge-ai-suggested`; ngay khi hành động "chấp nhận" được xác nhận (ghi vào draft/profile), đổi class sang `.badge-ai-accepted` trong cùng một lần cập nhật state — không giữ 2 trạng thái cùng lúc.

## 6. Component cần cập nhật

| Component | Thay đổi |
| --- | --- |
| Modal/Dialog (mới) | Chưa tồn tại trong code hiện tại. Thêm 1 component dùng `--shadow-md`, cho 3 luồng: xóa tài khoản, rút đơn ứng tuyển, báo cáo nội dung. |
| Sticky panel (bộ lọc, cột builder) | Thêm `--shadow-sm` khi đang ở trạng thái sticky (hiện chỉ có logic bật/tắt sticky, chưa có shadow phân biệt). |
| Badge | Thêm biến thể `.badge-ai-suggested` / `.badge-ai-accepted` như trên. |
| Input/select/textarea | Không đổi kích thước/token, chỉ áp `--color-text-placeholder` mới. |

## 7. Checklist Accessibility — ưu tiên cao nhất, làm trước

> **Trạng thái kiểm chứng:** Cả 3 phase của Style Guide v1 đã được triển khai và kiểm chứng ngày **29/10/2026** trên bộ màn hình Defense Baseline ([Báo cáo kiểm chứng 29/10](report/frontend-style-guide-2026-10-29.md)). Trên nhánh mở rộng **`feat/post-freeze-p1-p3-20261121`**, cần đối chiếu và đồng bộ lại cho các màn hình mới (`#home`, `#community`, `#analysis-history`, `#notifications`, `#admin`, `quickMatch.tsx`, `weeklyRoadmap.tsx`) — các đầu việc chi tiết được theo dõi tại [taskboard_v2.md](../taskboard_v2.md).

- [x] *(29/10)* Áp 3 token contrast mới (`--color-text-tertiary/caption/placeholder`)
- [x] *(29/10)* Sửa bug 2 menu cùng `aria-current="location"` ở `#jobs` (chỉ "Khám phá việc làm" được active, "Cách CareerGap hoạt động" chuyển thành hành động mở nội dung, không phải mục điều hướng)
- [x] *(29/10)* Thêm `aria-expanded` cho nút mở lịch sử đơn ứng tuyển
- [x] *(29/10)* Thêm `aria-describedby` + `aria-invalid` nối lỗi inline với field tương ứng *(lưu ý nhánh mở rộng còn nhiều `<p role="alert">`/`<div role="alert">` thiếu `.error` — theo dõi `SYNC-05` trong `taskboard_v2.md`)*
- [x] *(29/10)* Đồng bộ ngôn ngữ heading PDF theo lựa chọn Việt/English của người dùng *(lưu ý `resumeBuilder.tsx` và `writing.tsx` đang lặp selector — theo dõi `SYNC-11`)*
- [x] *(29/10)* Thêm H1 riêng cho các màn chưa có *(nhánh mở rộng có sự không đồng nhất `<h1>` giữa `workspace.tsx` và các view mới — theo dõi `SYNC-01`)*
- [x] *(29/10)* Mở rộng `prefers-reduced-motion: reduce` để bao gồm transition tab Trung tâm CV (hiện chỉ áp cho onboarding/CTA)

## 8. Thay đổi cấu trúc — làm sau accessibility

- **`#recruiter-jobs`**: tách thành sub-nav 3 mục (Công ty / Tin & JD / Ứng viên), theo đúng mẫu đã có ở Trung tâm CV (`#private-resumes`, `#cv-builder`, `#profile`). Đây là màn hình dài nhất hệ thống, gộp nhiều luồng trong 1 view.
- **Sidebar mobile**: đổi từ cơ chế đẩy nội dung xuống sang overlay drawer (che nội dung, đóng khi tap ngoài vùng menu).

## 9. Lộ trình triển khai

**Phase 1 — Accessibility (rẻ, nhanh, làm trước):** token contrast mới → fix bug menu kép → aria-expanded/aria-describedby → đồng bộ ngôn ngữ PDF → H1 thiếu → reduced-motion.

**Phase 2 — UX rõ ràng hơn (đã thống nhất tính là UX, không phải thẩm mỹ):** shadow token (`--shadow-sm`/`--shadow-md`) áp cho sticky panel + modal mới → hệ thống badge AI (`badge-ai-suggested` / `badge-ai-accepted`) áp vào 3 màn có AI.

**Phase 3 — Cấu trúc:** tách sub-nav `#recruiter-jobs` → đổi sidebar mobile sang overlay drawer.

---

*Tài liệu này dựa trên bản khảo sát code thực tế do người dùng cung cấp (ngày khảo sát trước đó). Các giá trị hex/tỷ lệ contrast mới là đề xuất, cần kiểm tra lại trên trình duyệt thật trước khi merge.*
