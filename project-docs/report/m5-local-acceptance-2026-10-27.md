# Biên bản bước 7 — nghiệm thu M5 phạm vi localhost

Ngày27/10/2026, Asia/Saigon. Chủ dự án ủy quyền “TUI CHO PHÉP NÍ TOÀN QUYỀN ĐỂ HOÀN THÀNH BƯỚC 7”. Codex trực tiếp kiểm tra, lập quyết định phát triển local và bàn giao; không giả chủ dự án hoặc giảng viên đã đọc/ký biên bản.

**Kết luận: 7/7 điều kiện M5 đạt trong phạm vi chức năng localhost và kế hoạch đánh giá hẹp đã chốt. Giai đoạn D hoàn thành; sẵn sàng chuyển sang E/P20.** Chưa chạy đánh giá chính thức E, chưa có kết quả chất lượng đại diện/M3 hoặc nghiệm thu M6. Deploy tiếp tục hoãn; không commit/push hoặc CIremote mới.

## 1. Quyết định theo từng điều kiện

| Điều kiện M5 trong roadmap | Kết quả và bằng chứng |
|---|---|
| Ba luồng cốt lõi xuyên suốt localhost | Đạt:9/9E2E và136/136backend trên DLL sau sửa quyền. Ứng viên CVprivate→JD, recruiter JD→CVpublic và application/snapshot/status có coverage test và bằng chứng luồng/provider lịch sử đúng phạm vi. Fake tests mới không được ghi thành probe provider thật. |
| Writing không bịa dữ kiện; nguồn học có URL thật | Đạt chức năng:2proposal DeepSeek synthetic được owner rà/accept/PDF/lưu private;17workflow artifacts kiểm hash.2Tavily requests thật,6links, owner rà và đối soát2credits; giữ journal/receipt sau dọn URL metadata, không giả đã đọc toàn trang hoặc mọi nguồn miễn phí. |
| Tính năng cốt lõi đã tích hợp | Đạt P16–P19:writing/review/builder/version, gap-topic/search, administration/report/audit/usage, account recovery và UX. Full backend gồm tests writing/learning/admin/email/retention; không chuyển một tính năng cốt lõi thiếu sang nhãn mở rộng. |
| Không còn lỗi quyền/snapshot/sàng lọc chặn đã biết | Đạt phạm vi rà:hai race quyền bước5/ba regressions giữ nguyên và đạt trong136fulltests. Không có sửa scoring/trigger/snapshot hoặc tự đổi trạng thái ứng tuyển trong lượt này. |
| Bộ test cần thiết và đường quota/unavailable | Đạt:136backend/9E2E/3frontend;lint/typecheck/productionbuild;DockerbuildLinux và3HTTPsmokes. Backend chứa migrationfresh/upgrade,quota,lease,idempotency,unknown và provider-unavailable controls.61evaluationtooltests giữ bằng chứng lịch sử cùnghash;không chạy lại E để lấy test. |
| Cấu hình/final/phương án chi phí cố định | Đạt scope đăng ký:bước6 freeze vẫnready/errors0/pending0.114runtime/27tests/23methodfiles/2DLLkhớp;2CVAnh×4JD/131labels/8pairs/k3, actor/quyền/bindings/exposure ghi rõ;E14jobs/tốiđa28attempts,trần0,50USD trong2USD và4creditssearchfree;demo replay0calls. |
| Tồn đọng có ảnh hưởng/người phụ trách/thời điểm | Đạt:register10mục giữ coverage/exposure/human0,template/catalog,providerowner-only,ledger/retention,CI và bàn giao sạch. Không phát hiện blocker chức năng local còn mở; những claim chất lượng thiếu vẫn chưa được công nhận. |

Manifest quyết định: [M5 local acceptance](../evaluation/m5-local-acceptance-2026-10-27.json). [Bàn giao E](../tasks/stage-e-entry-2026-10-27.md) có thứ tự và guard để tiếp tục, không cần lặp quyền/hạn mức đã cấp trong scope.

## 2. Đúng bản nguồn và phân biệt loại bằng chứng

Bản được nghiệm thu là working tree local có sourcebundle `17c07f2ee2c72da8795cd2420930774c8e2d683bdedf6feb03602fef31cfd383`, HEAD `64fbc991833f42ffad6a101579026b6ad5414aaf` còn dirty. Authoritative API DLL dùng cho test và E: `a21e45f59c8ff7982660cd233a3830a2d9064cbf1b0e5d02a32703a42aa29319`. Không có commit hoặc CI mới được suy ra từ kết quả local.

Đã tái kiểm188private+11public+6handoff/ledger artifacts của bước6 trước cập nhật; giữ snapshot mutable files và manifest cũ.114runtimefiles/27tests/2DLL không đổi trong bước7. So với candidate provider thật ngày23/10,112/114runtimefiles giữ nguyên;hai khác biệt là KeywordMatchingService/PairRecommendationService sửa hậu kiểm quyền, không đổi contract/adapter/prompt/scoring. Vì vậy dùng provider/ownerworkflow lịch sử làm bằng chứng tương ứng với component source không đổi, cùng regression mới đúng DLL cho phần quyền; **không tuyên bố đã probe provider lại trên DLL mới**.

Đã kiểm17workflow refs writing,2search receipt/cleanup refs và16email refs. Email thật đã có ownerInbox→reset→login→negativeUI→relogin và quota receipt, cộng Fake single-use regression; nguyên nhân riêng của lỗi UI không được suy chỉ từ ảnh. Phạm vi email là localhost tới địa chỉ owner đã xác nhận dùng sender thử provider; chưa có domain riêng/gửi tùy ý/production. Tài liệu công nghệ đề xuất verified domain cho triển khai rộng vẫn được giữ, không biến sender thử thành domain dự án đã xác minh.

## 3. Kiểm thử mới ở bước 7

| Kiểm tra | Kết quả mới |
|---|---|
| Backend PostgreSQL16.10, DLL đã ghim |136/136đạt,0skip;fullTRX riêng, gồm3regressions quyền |
| Browser E2E trên cùng API DLL |9/9đạt,0skip/0flaky |
| Frontend unit |3/3đạt |
| ESLint/TypeScript |Đạt |
| Vite production build |Đạt; còn cảnh báo chunk>500kB |
| DockerbuildLinux cùng source |Đạt;image local`careergap:m5-20261027`,khôngpush |
| Docker smoke |`/api/health`,`/`,`/api/auth/csrf` trả200;Fakeproviders,workerAItắt |
| Final-freeze recheck |valid/ready,errors0/pending0 |

Chạy riêng trên PostgreSQL16.10 tmpfs `careergap-m5-20261027`,loopback55443,API5087,Vite5177;Docker smoke5088. Chỉ synthetic fixtures; môi trường allowlist,khôngdotenv/key thật,AI/search/emailFake. Backend dùng output đã build ở bước5,khôngrebuild/đổi DLL đãfreeze;Docker có binaryLinux riêng từ cùngsource,chỉ làm bằng chứng Linux,không thay artifactWindows củaE.

Kết thúc còn0testDB,API/Vite tạm đãdừng,hai container vànetworktạm đãdọn. Các output E2E dùng đường dẫn legacy cố định được lưu riêng rồi phục hồi artifact cũ. DB/volume/dumpE55442 vẫn dừng và nguyên vẹn;không dùng DBứngdụng hoặc thay tiến trình người dùng. FileWord riêng của owner và188private artifacts bước6 khôngđổi. Không có migration/dependency/sourcecode mới trong repository;helper chỉ tạo schema test ở DBtạm.

## 4. Chất lượng, tài chính và phần E chưa làm

Nghiệm thu này là mốc đủ chức năng local và sẵn sàng thực nghiệm. Không tự đóng M3chấtlượngđại diện hoặc M6;không chứng minh CVViệtthật,JDViệtCNTT,layoutgốc hoặc cảhệthốngpristine. Các nhãn doCodex rà,human0;28criterion exclusions/5user_statement facts và lịch sửparser/JD được giữ trong protocol. Kết quả P20 phải công bố đúng scope2CVAnh×4JD và mẫu số,không dùng synthetic hay bản dịch để bù.

Không gọi provider,balance,search hoặcemail mới ở bước7;phí mới0USD. Ledger và allocation khôngđổi:trackedspent0,03USD,hold0,02,guarded1,95;allocationE0,50,unallocated1,45;search4free/email0/demo replay. Không hoànunknownemail cũ hoặc reset cap theo DB/kỳ. Trước P20 vẫn kiểm livefunds/credits/giá và hash theo guard đãchốt;không cần xin lại quyền khi scope/hash/cap khôngđổi.

P20 chưa chạy baseline/finalmodelcaptures/ranking/latency/quality;chưa có actualgap đểsearch. P21 chưa có diễn tậprestore/bàn giao sạch cho bản hiện tại,thửREADMEởmôi trườngsạch,dùngthửgiảngviên,báocáo/slide/video/demobàn giao. Các phần đó có backlog/chủ trì/thời điểm, không được gọi là đã đạt chỉ vì M5 đóng. Nếu sửa phương pháp saufreeze,tạo phiên bản mới và xửlý lại final;không tune kết quả rồi giữnhãnđộclập.

## 5. Hoàn thành bước 7

Đã lập decision/evidence matrix và limitation register, cập nhật current-status/roadmap/taskplan/README và privatehandoff. M5 được chấp nhận bởi Codex theo ủy quyền phát triển local của chủ dự án, không phải ký nghiệm thu học thuật của giảng viên. **Giai đoạn D kết thúc; công việc tiếp theo là E/P20 theo bản freeze hiện có. Chưa thực thi E trong lượt này.**
