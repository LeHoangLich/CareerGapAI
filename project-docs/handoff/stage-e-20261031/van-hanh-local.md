# Vận hành và rollback31

Container/volume `careergap-handoff-demo-20261029`, DB `careergap_test_ui_handoff_demo`, port55449 và storage/accounts giữ nguyên. Không wipe/reset hoặc xóa volume. Không gọi seed/migration thủ công trên DB hiện có để diễn tập lại.

## Phiên bản

| Release | Runtime riêng | Trạng thái |
|---|---|---|
| 20261029 | `.local/handoff-demo-20261029/runtime` | Giữ hash lịch sử |
| 20261030 | `.local/handoff-demo-20261029/releases/20261030/runtime` | Giữ hash lịch sử, rollback đã kiểm mới |
| 20261031 | `.local/handoff-demo-20261029/releases/20261031/runtime` | Mặc định start/fault, có A12 |

Launcher kiểm runtime29/30 đã tồn tại trước khi stop/chuyển; thiếu sẽ từ chối, không build source hiện tại dưới nhãn cũ. Trên máy mới, phục hồi runtime cũ từ bản lưu tin cậy hoặc build đúng source lịch sử ở workspace riêng; không copy source31 thành runtime30. ZIP31 không chứa binary/DB private.

## Backup và kiểm restore riêng

```powershell
python scripts/handoff-backup.py backup
python scripts/handoff-backup.py verify
```

Container DB phải đang chạy/ready; backup dừng API, tạo dump và bản sao objects offline. Lệnh verify khôi phục vào cloneDB riêng và để clone offline; không ghi đè DBdemo. Sau kiểm có thể dọn đúng clone được receipt ghi, không xóa DBdemo. Backup31 tại `.local/handoff-demo-20261029/backups/20261031T122502Z/`;43tables/7objects/DPkeys khớp. Clone kiểm của lượt phát hành đã dọn, dump/objects giữ nguyên. Backup chứa dữ liệu private và khóa phiên nên không đưa vào ZIP public. Giữ chính sách retention hiện hành.

## Quay lại và tiến lên

```powershell
python scripts/handoff-demo.py start --release 20261030
python scripts/handoff-demo.py start --release 20261031
python scripts/handoff-demo.py fault --release 20261031
python scripts/handoff-demo.py start --release 20261031
```

Sau mỗi lệnh đối chiếu health và `.local/handoff-demo-20261029/process.json`: release/runtime/PID/hashDLL/providersFake/mode. Không coi health200 một mình là đúng binary. Chu kỳ thực đã kiểm cookie còn dùng được và đọc history được; không cam kết cookie sống qua mọi môi trường hoặc mọi lần restart. Fault31:503 cho writing mới; downloadCV khớp hash và builder-content đọc được.

Có thể chọn29 trên máy này vì runtime lịch sử được giữ; lượt31 kiểm vòng rollback30, không tuyên bố đã chạy lại vòng29. Release31 không đổi schema/migration nên không cần restoreDB để rollback binary. Nếu có sự cố dữ liệu, dừngAPI, giữ log/backup và phục hồi qua kế hoạch riêng; không chạy restore đè ngay khi chỉ có lỗi giao diện.

```powershell
python scripts/handoff-demo.py stop
```

Stop chỉ dừng API thuộc demo, giữ DB/container/storage. Không chạy `docker compose down -v` để dọn buổi demo.
