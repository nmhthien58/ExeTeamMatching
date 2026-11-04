# Thay đổi task ngày 04/11/2026 – yêu cầu PRM393 Lab 3 và Final

> **Google Sheet bản chính chưa đổi** – người giữ sheet dán các dòng dưới đây vào sheet tương ứng (cột theo thứ tự của sheet: Mã · Giai đoạn · Module · Task · Mô tả · Tham chiếu · Ưu tiên · Thành viên · Phụ thuộc · Chờ người khác · Trạng thái · Ghi chú). File xlsx trong `docs/` chưa cập nhật.
> Giai đoạn mới `GĐ7 – PRM393` cần thêm vào danh sách chọn của cột Giai đoạn.
> Đề bài: `docs/PRM393 - Lab 3 Cloud-Enabled Mobile App (Firebase, Patrol, AI Code Review).pdf`, `docs/PRM393 - Final Assignment One Complete System on Firebase.pdf`.

## Bối cảnh

- Rà soát ngày 04/11/2026: phần **PRN232** gần như đủ (REST, JWT, Kafka, gRPC, Hangfire, Docker, Swagger, DB script, demo newman). Phần **PRM393** còn thiếu nhiều: chưa có dịch vụ Firebase nào, mobile chưa theo MVVM (chỉ 1 Notifier, màn hình gọi API trực tiếp), chưa có ARTEMIS, AI code review và các tài liệu đề yêu cầu.
- **Giảng viên PRM393 đã cho dùng backend C#.** Vì vậy bỏ phần "Firebase làm backend" của đề Final: Firestore, Security Rules + test trên Emulator, `packages/core/`. Vẫn phải làm: 6 dịch vụ Firebase phía client của Lab 3, App Check, tính năng Gemini qua Firebase AI Logic, Analytics và Remote Config trên web, URL web công khai, luồng xuyên vai trò.
- **Đăng nhập không đổi contract:** app giữ Google Sign-In, dùng cùng Google ID token để `signInWithCredential` vào Firebase Auth (user hiện trên Console) và gửi Identity như cũ để lấy JWT.
- **Refactor MVVM làm một lượt sau khi hết task mobile dở dang**, để ít conflict nhất: đóng băng `mobile/lib` trong lúc refactor, chia PR theo feature, chủ mảng duyệt PR của mảng mình.

## Cập nhật trạng thái (đã merge vào `develop` nhưng sheet chưa ghi Xong)

| Mã | Trạng thái | Bằng chứng |
|---|---|---|
| FE-45 | Xong | #117 |
| MB-46 | Xong | #117 (C09 mobile) |
| MB-43 | Xong | #131 |
| MB-32 | Xong | #130, #133 |
| MB-33 | Xong | #133 |
| MB-34 | Xong | #133 |
| MB-44 | Xong | #133 (L01 tạm hiện "—" cho Trễ hạn nộp và Họp hôm nay, chờ BE-86) |
| BE-83 | Xong | #135 sửa ngày Checkpoint theo giờ Việt Nam |
| MB-36 | Xong | #134, #138 (checklist §5 cho mọi màn còn lại) |
| MB-09 | Xong | #41 (A06, badge, polling, đọc tất cả, chạm mở màn); 04/11 Thành kiểm lại trên emulator |
| BE-59 | Xong | #136 (thêm luồng 5, 6, 7, 10, 11; newman 260/260 assertion), #139 (README đủ mục PRN232) |

Không còn task mobile dở dang – có thể **đóng băng `mobile/lib`** và bắt đầu MB-47.

## Task mới (20)

### Backend

| Mã | Giai đoạn | Module | Task | Mô tả / tiêu chí hoàn thành | Tham chiếu | Ưu tiên | Thành viên | Phụ thuộc |
|---|---|---|---|---|---|---|---|---|
| BE-84 | GĐ7 – PRM393 | Notification | Gửi push FCM | API đăng ký / gỡ device token (`POST`, `DELETE /api/notifications/devices`); bảng `device_tokens` trong `notification_db` (DB script do Thiên sửa); khi tạo thông báo trong app thì gửi FCM HTTP v1 tới các thiết bị của người nhận, payload có `type` và id để app mở đúng màn; token hỏng thì xóa; khóa service account đọc từ biến môi trường, không commit. Contract do Thiên duyệt | Lab 3 §6 (Cloud Messaging); Final §3 (inbox) | P1 | Thành | MB-52 |
| BE-85 | GĐ7 – PRM393 | DevOps | Triển khai cloud, URL công khai | Chạy toàn bộ `docker compose` trên một máy chủ công khai (VPS hoặc cloud), có HTTPS cho Gateway và web; URL web mở được ở cửa sổ ẩn danh; mobile build release trỏ về Gateway công khai; README có hướng dẫn triển khai. Hỏi giảng viên trước nếu cần nơi triển khai cụ thể | Final §7 ("URL works"); PRN232 §3 | P0 | Thiên | – |
| BE-86 | GĐ7 – PRM393 | Collaboration | `GET /api/advisor/overview` | Endpoint đã có trong `collab.yaml` nhưng chưa có controller (gọi trên Docker trả 404). Trả `needsReplyCount`, `overdueDeliverableCount`, số họp hôm nay của giảng viên / mentor trong một request; L01 dùng lại thay cho số "—" hiện tại | Góp ý của Nguyên ở #133; L01 | P1 | Thiên | – |

### Frontend Web

| Mã | Giai đoạn | Module | Task | Mô tả / tiêu chí hoàn thành | Tham chiếu | Ưu tiên | Thành viên | Phụ thuộc |
|---|---|---|---|---|---|---|---|---|
| FE-46 | GĐ7 – PRM393 | Nền tảng | Analytics và Remote Config trên web | Firebase JS SDK; service riêng trong `web/src/shared`, màn hình không gọi SDK; ghi sự kiện đăng nhập / đăng xuất và thao tác chính của Bộ môn; ≥ 1 tham số Remote Config làm đổi hành vi web; không đưa email / tên vào tham số | Final §6 ("Remote Config and Analytics in both clients") | P1 | Thiên | MB-47 |
| FE-47 | GĐ7 – PRM393 | Đổi nhóm | Luồng xuyên vai trò | `docs/cross-role-flow.md` theo Phụ lục C: trưởng nhóm gửi yêu cầu đổi nhóm trên mobile (S26–S28) → Bộ môn duyệt trên web (W18) → mobile cập nhật realtime và có thông báo ở A06. Mỗi bước ghi người làm, màn hình, API, sự kiện; sơ đồ sequence Mermaid; đo độ trễ giữa lúc duyệt và lúc mobile cập nhật. Chạy 3 lần trên bản đã triển khai | Final §3, §9, Phụ lục C | P0 | Thành | BE-85, MB-52 |
| FE-48 | GĐ7 – PRM393 | Chất lượng | Checklist web admin cho Final | Đối chiếu 5 màn đề yêu cầu với màn đã có: đăng nhập + từ chối sai vai trò (W01), quản lý một thực thể (ví dụ W11 / W27), hàng chờ duyệt (W17, W18), dashboard ≥ 3 số liệu + 1 biểu đồ (W02), form thông báo (W23). Mỗi màn: thao tác chính, trạng thái trống và lỗi, một ảnh chụp; lưu ở `evidence/web/` và `docs/test-report.md` | Final §7, §9 | P1 | Nguyên | BE-85 |

### Mobile

| Mã | Giai đoạn | Module | Task | Mô tả / tiêu chí hoàn thành | Tham chiếu | Ưu tiên | Thành viên | Phụ thuộc |
|---|---|---|---|---|---|---|---|---|
| MB-47 | GĐ7 – PRM393 | Nền tảng | FlutterFire, Firebase Auth, App Check | `flutterfire configure`, `firebase_options.dart`, `google-services.json`; SHA-1 và SHA-256 debug + release trên Console; `Firebase.initializeApp()` trong `main()`; sau Google Sign-In gọi `signInWithCredential` rồi vẫn gửi ID token cho Identity; đăng xuất cả Firebase và xóa dữ liệu cục bộ; hủy đăng nhập / mất mạng hiện thông báo; App Check (Play Integrity, debug provider cho APK demo); ghi phiên bản đã pin vào README | Lab 3 §6 (Authentication); Final §6 (App Check) | P0 | Thiên | – |
| MB-48 | GĐ7 – PRM393 | Nền tảng | Mẫu MVVM và `docs/architecture.md` | Chốt mẫu: ViewModel (Riverpod Notifier) giữ trạng thái và nghiệp vụ, state model dạng sealed (đang tải / trống / lỗi / có dữ liệu), service inject qua provider để test bằng fake. Áp mẫu cho auth, shell và một feature có cả danh sách lẫn form (`discover`). `docs/architecture.md`: cấu trúc thư mục mỗi dòng một thư mục, sơ đồ lớp, một sơ đồ sequence cho cả luồng, bảng ViewModel ↔ service | Lab 3 §5 | P0 | Thiên | MB-47 |
| MB-49 | GĐ7 – PRM393 | Nền tảng | Refactor MVVM toàn bộ mobile | Áp mẫu MB-48 cho mọi feature còn lại; widget không gọi API / SDK, không có nghiệp vụ trong `build()`; không đổi hành vi. Mỗi PR một hoặc vài feature, chủ mảng duyệt PR của mảng mình; `flutter analyze` sạch và `flutter test` qua sau mỗi PR. Trong thời gian làm, không mở PR mobile khác | Lab 3 §5, §10 (Architecture 0.75) | P0 | Thiên | MB-48 |
| MB-50 | GĐ7 – PRM393 | Nền tảng | Crashlytics và Analytics | Service riêng cho mỗi SDK. Crashlytics: bộ xử lý lỗi Flutter, 1 lỗi non-fatal từ luồng lỗi thật, user ID, ≥ 1 custom key. Analytics: `docs/analytics-plan.md` theo Phụ lục A với ≥ 7 sự kiện (đăng nhập, đăng xuất, thao tác chính, xem từng loại màn chi tiết, xuất / tải file, `ai_request`, `ai_result_accepted`, `ai_error`), gắn với bước của luồng F1–F3, có câu hỏi sản phẩm; đặt user ID sau đăng nhập; không có email / tên trong tham số; bằng chứng DebugView từng sự kiện | Lab 3 §6, Phụ lục A | P0 | Thiên | MB-48 |
| MB-51 | GĐ7 – PRM393 | Nền tảng | Remote Config | Service riêng; giá trị mặc định trong app và `firebase/remote-config-defaults.json`; ≥ 2 tham số làm đổi hành vi thấy được (đề xuất: số nhóm gợi ý tối đa ở S07, cờ bật/tắt gợi ý AI ở S09; thêm `ai_model`, `ai_prompt` cho MB-55); đổi trên Console thì app đổi mà không build lại; mất mạng vẫn dùng giá trị mặc định | Lab 3 §6 (Remote Config) | P0 | Thiên | MB-48 |
| MB-52 | GĐ7 – PRM393 | Thông báo | FCM trên mobile | Xin quyền thông báo Android 13+, xử lý khi bị từ chối; nhận push ở 3 trạng thái (đang mở, chạy nền, đã đóng); thông báo nhận được hiện trong A06; chạm thông báo mở đúng màn; gửi device token lên BE-84 sau đăng nhập, gỡ khi đăng xuất; bằng chứng tin thử từ Console ở cả 3 trạng thái | Lab 3 §6 (Cloud Messaging); A06 | P0 | Thành | MB-47, MB-49 |
| MB-53 | GĐ7 – PRM393 | Workspace | Xuất PDF và Firebase Storage | Tạo PDF báo cáo dự án (nội dung đúng như C01 đang hiện) và tải lên `reports/{uid}/…`; hiện tiến độ, URL tải và kết quả thành công / thất bại; `firebase/storage.rules` chỉ cho chủ UID đọc/ghi, không dùng `if true`; thử truy cập sai quyền và mất mạng. Kiểm tra trước: tạo bucket Storage có thể cần gói Blaze | Lab 3 §3, §6 (Cloud Storage) | P0 | Nguyên | MB-47, MB-49 |
| MB-54 | GĐ7 – PRM393 | Cài đặt | Khu demo Firebase ở A07 | Mục "Demo Firebase" trong Cài đặt: giá trị Remote Config đang dùng, nút lỗi non-fatal và crash thử (có xác nhận, không dùng trong luồng thường), lối vào xuất / tải file, lối vào thông báo, gửi lại device token | Lab 3 §3 (Firebase demo area) | P1 | Nam | MB-50, MB-51, MB-53 |
| MB-55 | GĐ7 – PRM393 | Nền tảng | Service AI (Gemini) | `firebase_ai` qua Firebase AI Logic (Gemini Developer API), có App Check; tên model, prompt và cờ bật/tắt đọc từ Remote Config; trả JSON có cấu trúc → model Dart; xử lý lỗi quota, mạng, JSON sai để luồng vẫn chạy khi không có AI; không gửi tên, email, ảnh người; log `ai_request`, `ai_result_accepted`, `ai_error`. `docs/ai-feature.md`: 3 đầu vào / đầu ra thật và 1 lỗi đã xử lý | Final §8 (mức 2) | P0 | Thiên | MB-47, MB-51 |
| MB-56 | GĐ7 – PRM393 | Khám phá | Gợi ý ý tưởng bằng AI ở S09 | Trong "Ý tưởng của bạn" ở S09: nút "Gợi ý bằng AI" → sinh viên gõ vài câu mô tả → AI điền sẵn tên ý tưởng, vấn đề, giải pháp, khách hàng mục tiêu, tag lấy trong danh mục lĩnh vực; nhãn "Gợi ý của AI", sửa được trước khi gửi; cờ tắt hoặc AI lỗi thì ẩn nút / báo lỗi và vẫn nhập tay được. Làm cả web cùng mã màn hình nếu kịp | Final §8; S09 (flutter-handoff mục 4a) | P0 | Cường | MB-55 |
| MB-57 | GĐ7 – PRM393 | Tài liệu | Design traceability và gap review | `docs/design-traceability.md`: mỗi màn, luồng, component Lab 2 → route, widget, nguồn dữ liệu, trạng thái; mọi chỗ app khác Figma kèm lý do (Figma đang là bản Lab 2, chưa vẽ lại). Cập nhật Figma cho các chỗ lệch hoặc ghi rõ lý do giữ. `docs/gap-review.md`: Lab 2 / Lab 3 còn thiếu gì và đã đóng thế nào | Lab 3 §3, §4; Final §5 | P0 | Thiên | – |
| MB-58 | GĐ7 – PRM393 | Chất lượng | Kiểm thử E2E bằng ARTEMIS | `docs/test-plan.md` theo Phụ lục B: luồng quan trọng ↔ màn hình, ≥ 10 ca (mỗi luồng Lab 2 gồm nhánh lỗi, đăng nhập, mỗi tab, mỗi loại màn chi tiết, xuất / tải file, Remote Config trên màn, đăng xuất, bước mobile của luồng xuyên vai trò, tính năng AI), ≥ 1 ca lỗi, ≥ 1 tương tác Android gốc. `e2e_test/` (pytest + `ArtemisClient`, `conftest.py`, `pyproject.toml`), kiểm tra giá trị agent báo về; mỗi ca chạy ≥ 3 lần, bảng tỷ lệ đạt, trace trong `evidence/`; 1 phiên khám phá 15–30 phút (profile Pro); Semantics label cho widget chính; pin commit ARTEMIS trong README | Lab 3 §7, Phụ lục B; Final §9 | P0 | Nguyên | MB-49, MB-52, MB-53, MB-56 |
| MB-59 | GĐ7 – PRM393 | Chất lượng | AI code review | Chạy một công cụ review AI (Copilot, CodeRabbit, SonarCloud…) trên PR; `docs/code-review.md` theo Phụ lục C: ≥ 5 phát hiện gắn file:dòng, thuộc ≥ 2 loại, mỗi phát hiện có quyết định và lý do kỹ thuật; ≥ 3 fix, mỗi fix một commit ghi mã phát hiện; liệt kê mọi công cụ AI đã dùng. Phát hiện trong mảng nào thì chủ mảng sửa | Lab 3 §8; Final §9 | P0 | Thiên | MB-49 |
| MB-60 | GĐ7 – PRM393 | Nộp bài | Báo cáo, slide, video PRM393 | `evidence/` (Console, DebugView, Crashlytics, ARTEMIS). Lab 3: report 8–12 trang, slide 12–15, video 5–10 phút. Final: report 10–15 trang, slide 12–15, video 8–12 phút. README: vai trò, URL web, cách chạy, tài khoản demo theo vai trò, phiên bản, công cụ AI, link video; mọi link mở được ở cửa sổ ẩn danh | Lab 3 §9; Final §10 | P0 | Thiên (cả nhóm cùng làm) | Tất cả task trên |

## Task cũ thay đổi

| Mã | Thay đổi |
|---|---|
| MB-36 | **Ghi chú:** đã xong; kiểm thử sau refactor MVVM thuộc MB-58 |
| MB-09 | **Ghi chú:** đã xong; phần push FCM vào A06 làm ở MB-52 |

## Thứ tự nên làm

1. **Đóng băng `mobile/lib`** (mọi task mobile cũ đã xong).
2. MB-47 (Firebase + Auth) → MB-48 (mẫu MVVM). MB-57, BE-85 làm song song được.
3. MB-49 (refactor), song song MB-50, MB-51 (chỉ đụng service mới trong `core/`).
4. MB-52 + BE-84, MB-53, MB-55 → MB-56, MB-54, FE-46, BE-86.
5. FE-47, FE-48, MB-59, MB-58 (cần app đã ổn định).
6. MB-60.

## Cần hỏi giảng viên PRM393

- Nơi triển khai để có URL công khai (BE-85).
- Một repo dùng chung cho PRN232 và PRM393 có được không, tên repo có phải đổi thành `PRM393_Final_<ID>` không.
- Storage: nếu không dùng được gói Blaze thì có chấp nhận dịch vụ tương đương không.

## Số task mỗi người thêm

| Thành viên | Task mới |
|---|---|
| Thiên | 12 (BE-85, BE-86, FE-46, MB-47, MB-48, MB-49, MB-50, MB-51, MB-55, MB-57, MB-59, MB-60 – MB-60 cả nhóm cùng làm) |
| Nam | 1 (MB-54) |
| Cường | 1 (MB-56) |
| Nguyên | 3 (FE-48, MB-53, MB-58) |
| Thành | 3 (BE-84, FE-47, MB-52) |
