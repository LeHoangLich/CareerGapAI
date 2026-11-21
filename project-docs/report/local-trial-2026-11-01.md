# Chạy thử local Defense Baseline ngày01/11/2026

Đã chạy thử release20261031 với dữ liệu synthetic/providerFake theo yêu cầu owner. **Tất cả kiểm thử đạt**; source freeze và trạng thái M6 `final_defense_ready` giữ nguyên.

| Kiểm tra | Kết quả mới |
|---|---|
| Backend trên PostgreSQL tạm riêng |160/160 đạt,0skip |
| Frontend unit |15/15 đạt |
| Lint/typecheck/build |Đạt cả ba; Vite có cảnh báo bundle>500kB không chặn |
| Playwright E2E |22/22 đạt, gồm integrated/Fake và mock |
| Runner/migration/seed/cleanup |Đạt, completed=true/cleanup=true |
| Source freeze |222/222hash khớp |

Pipeline: `python scripts/test-frontend-regression.py`; run `20261101-141707-55ce`. Không nạp.env; database audit non_fake_methods0/USDbudgets0, chỉ ngân sáchFake/test_credit. Không gọi provider thật hoặc phát sinh credits/USD. Không gọi đây là đánh giá chất lượng model mới.

## Demo5190

Demo lúc đầu chưa chạy; đã khởi động bằng `python scripts/handoff-demo.py start --release 20261031`, dùng đúng runtime đã đóng băng. Sau chạy giữ app ở mode start để owner mở **http://127.0.0.1:5190/**. Không wipe/reseedDBdemo hoặc đổi mật khẩu tài khoản dùng chung.

Playwright trên static frontend/API5190 thực, không intercept:

- Candidate đăng nhập, reload và mở lại đúng kết quả cặp lịch sử; mở tiêu chí/chủ đềDocker. Pha xem lại có0POSTmatching/recommendation, CV3versions/private và đơnreviewing/CVv1 đúng trạng thái đã kiểm.
- Reset bằng token synthetic không hợp lệ nhận400invalid_or_expired_reset, hiển thị lỗi riêng và nút yêu cầu liên kết mới; bấm mở formforgot, querytoken/lỗi cũ được xóa.0emailrequest tự động, không đổi password. Ca này kiểm invalid token; các trường hợp khác thuộc bộ regression backend/E2E, không gọi đây là xem token hết hạn trực tiếp trên5190.

DLLruntime31 SHA-256 vẫn `21b449d8110bbda1bf007cd1f18c74b8e16410ccb811de7253ce5b6cee270387`. DLLtestbuild có hash riêng trongresult, không đồng nhất với runtime5190. Không chạy lại vòng rollback/backup/restore trong lượt thử này; bằng chứng phát hành31 vẫn giữ lịch sử.

## Bằng chứng

- Pipeline/result/TRX/logs: `.local/frontend-step3-20261101-141707-55ce/`.
- UIreceipt, ảnh và video mới: `.local/local-trial-20261101-141707-55ce/`; không ghi đè media đã bàn giao.
- [Manifest chạy thử](../evaluation/local-trial-2026-11-01.json) ghim SHA-256 từng bằng chứng, audit nguồn và phiên bản.
- [Nghiệm thu nội bộ M6](m6-internal-acceptance-2026-11-01.md) và [bộ baseline31](../handoff/stage-e-20261031/README.md) tiếp tục là hồ sơ quyết định/bàn giao hiện hành.
