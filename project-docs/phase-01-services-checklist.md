# GĐ1 — Checklist tài khoản và dịch vụ

Chủ dự án tự cập nhật bảng này khi có thông tin. Người dùng xác nhận sẽ cung cấp sau; **Chưa cung cấp không có nghĩa Chưa có tài khoản**. Chưa tạo/mua dịch vụ hay cấu hình secret thay người dùng.

Trạng thái có thể điền: `Chưa cung cấp` / `Chưa tạo` / `Đã có tài khoản` / `Đã cấu hình` / `Đã kiểm chứng` / `Không cần` / `Bị chặn`.

## Quyết định hiện tại — dừng deploy

Từ 15/10/2026, ưu tiên hoàn thiện sản phẩm trên localhost. Bảng dưới giữ thông tin đã ghi nhận (bao gồm Neon S04); trạng thái Đã cấu hình không phải chỉ dẫn tiếp tục sử dụng cloud. Không tiếp tục hosting, DNS, migration cloud, cấu hình production hoặc deploy. Không tự xóa dịch vụ/dữ liệu đã có. Các phần này chỉ mở lại sau khi sản phẩm hoàn thiện local và người dùng yêu cầu; thiếu đầu vào cloud không chặn phát triển local.

DeepSeek/search/email thật là kiểm chứng tích hợp riêng, có thể thực hiện từ localhost khi được phép và trong ngân sách. Mặc định phát triển/CI dùng fake; không coi fake là chứng minh provider hoạt động.

## Bảng cập nhật

| ID | Hạng mục | Trạng thái hiện tại | Thông tin an toàn cần điền | Điều kiện kiểm chứng |
|---|---|---|---|---|
| S01 | GitHub/repository/CI | Đã kiểm chứng baseline b392ef0 | Repo: LeHoangLich/CareerGapAI; CI 38836054191 success; xem [hiện trạng](current-status.md) | Frontend/backend/Docker success đúng SHA; thay đổi A chưa commit/push/CI riêng |
| S02 | Hosting backend | Chưa cung cấp | Provider, tên project, gói, region, URL demo, cách deploy từ repo | HTTPS, container/port, cold start, restart và ngân sách phù hợp |
| S03 | Hosting frontend riêng (nếu chọn) | Chưa cung cấp | Provider, URL và khả năng rewrite /api | Login/logout/CSRF/upload qua proxy; nếu dùng S02 chung thì ghi Không cần, không tạo thêm dịch vụ |
| S04 | PostgreSQL demo | Đã cấu hình | Neon; PostgreSQL 16.15; Singapore (`ap-southeast-1`); database `careergapai`; runtime `careergapai_runtime` tách khỏi migration/admin `careergapai_owner`; TLS yêu cầu; runtime connection string được bảo vệ bằng Windows DPAPI tại `.local/secrets/neon-phase01.dpapi.json` | Đã xác minh kết nối, TLS, 3 migrations trong `__EFMigrationsHistory`, runtime đủ DML nhưng không có quyền tạo schema/database. Chưa thử backup/restore cô lập; Neon Free nêu Instant Restore tối đa 6 giờ hoặc 1 GB thay đổi (điều kiện nào đến trước), cần xác nhận phương án backup theo thời hạn lưu trữ dự án |
| S05 | Object Storage private | Chưa cung cấp | Provider, region, endpoint công khai của API storage, tên bucket; private đã bật hay chưa | Upload/read/verify hash, quyền backend, file sau redeploy; không bucket public |
| S06 | DeepSeek | Chưa đặt key; chưa cho phép probe trả phí (chủ dự án xác nhận giai đoạn A) | Tài khoản đã sẵn sàng hay chưa, model định dùng, ngày kiểm tra giá, chu kỳ usage, ngân sách còn lại; chỉ ghi Đã đặt key backend nếu đã làm | Một call thật trong budget, schema/evidence hợp lệ, token/usage/chi phí đối soát và nút ngừng dispatch |
| S07 | Search nguồn học | Chưa cung cấp | Provider, gói/quota/credits, chu kỳ reset, điều khoản cache, trạng thái key backend | Một truy vấn kỹ năng không PII, URL thật, ghi credits, xử lý hết quota |
| S08 | Email qua HTTPS | Chưa cung cấp | Provider, trạng thái sender/domain xác minh, địa chỉ nhận thử do chủ dự án kiểm soát, free quota | Quyền gửi, kết quả gửi thật; fake không tính. Khôi phục mật khẩu hoàn chỉnh thuộc GĐ2 |
| S09 | Domain/DNS (nếu có) | Chưa cung cấp | Tên domain, có quyền chỉnh DNS hay chưa, trạng thái SPF/DKIM/sender; không gửi quyền quản trị | DNS/HTTPS/sender kiểm tra thật. Demo có thể dùng hostname mặc định, không tự mua domain |
| S10 | Data Protection certificate | Chưa cung cấp | Đã chuẩn bị/chưa, nơi mount/secret-store theo tên tham chiếu, cách backup và người quản lý (chủ dự án) | Key ring DB được bảo vệ; cookie/token dùng sau redeploy, có khóa giải mã để restore |
| S11 | Backup/restore và lịch xóa | Chưa cung cấp | Nơi lưu private theo tên tham chiếu, lịch backup, TTL tối đa 14 ngày sau purge, cơ chế chạy khi host sleep | Restore DB+objects+DP cert cô lập; áp lại sổ xóa, không hồi sinh tài khoản đã xóa |
| S12 | Ngân sách chung | Mục tiêu đã chốt, số dư chưa cung cấp | Khoảng 4 USD/tháng cho toàn dự án; mức đã dùng/còn lại và kỳ thực tế; search credit ghi riêng | Chỉ chạy call thật sau dự trù, gồm retry; không nâng gói/pay-as-you-go tự động |

Tài liệu 02 có các provider đề xuất (Vercel/Render/Neon/DeepSeek/Tavily/Resend), chưa phải tài khoản đang có. Nếu chọn provider khác, ghi ở bảng rồi kiểm chứng tương thích; không mặc định gói/giá/quota hiện hành từ tài liệu cũ.

## Mẫu điền nhanh

Sao chép khối sau, chỉ thêm thông tin không bí mật:

```text
S02 Backend: [provider] | [trạng thái] | [URL] | [region/gói]
S03 Frontend riêng: [provider hoặc Không cần] | [trạng thái] | [URL]
S04 PostgreSQL: [provider] | [major/region] | [runtime/migration tách: có/chưa]
S05 Private storage: [provider] | [bucket/region] | [private: có/chưa]
S06 DeepSeek: [trạng thái] | [model] | [budget còn lại/kỳ] | [key đã đặt backend: có/chưa]
S07 Search: [provider] | [credits/kỳ] | [key backend: có/chưa]
S08 Email: [provider] | [sender xác minh: có/chưa] | [gửi thử: có/chưa]
S09 Domain/DNS: [domain hoặc Chưa có] | [quyền DNS: có/chưa]
S10 DP certificate: [đã chuẩn bị/chưa] | [backup: có/chưa]
S11 Backup/xóa: [lịch] | [TTL] | [restore đã thử/chưa]
S12 Tổng ngân sách: [đã dùng/còn lại] | [kỳ]
```

**Không điền** API key, password, cookie/token, connection string có mật khẩu, PFX/private key, recovery code hoặc signed URL vào Markdown/chat/Git. Secrets chỉ đặt trực tiếp vào backend/secret store. Bảng chỉ ghi trạng thái và tên tham chiếu cấu hình, không tự đọc/in giá trị secret để kiểm tra.

## Thứ tự tham khảo sau khi được mở lại deploy

Các bước sau đang hoãn, không phải việc cần làm ngay. Công việc hiện tại theo [ưu tiên localhost](tasks/phase-01-foundation.md#ưu-tiên-hiện-tại--localhost).

1. Hoàn thiện S02/S04/S05/S10/S12 cho luồng demo private; S03 chỉ cần nếu tách frontend.
2. Chạy HTTPS/file/DP/restart/restore, rồi thử DeepSeek S06 có giới hạn.
3. Kiểm chứng S07 và S08/S09 cho các spike GĐ1; không coi email sản phẩm GĐ2 đã xong.
4. Ghi commit/run/URL/usage/bằng chứng vào [task](tasks/phase-01-foundation.md) và [report](report/phase-01-foundation-report.md). Bảng điền đủ thông tin không tự chứng minh dịch vụ đã hoạt động.
