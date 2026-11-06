# Kế hoạch Analytics, Crashlytics, Remote Config – mobile

> MB-50, MB-51 (04/11/2026), theo PRM393 Lab 3 §6 và Phụ lục A. Code: `mobile/lib/core/firebase/`
> (`analytics_service.dart`, `crash_reporter.dart`, `remote_config_service.dart`). Luồng F1–F3: [ux/user-flow.md](ux/user-flow.md);
> chỉ số thành công: [ux/persona.md](ux/persona.md) mục 9.

## 1. Nguyên tắc

- Widget không gọi SDK. ViewModel lấy `analyticsServiceProvider`, `crashReporterProvider`, `remoteConfigProvider` bằng `ref.read` / `ref.watch` ([architecture.md](architecture.md)); test thay bằng bản giả trong `mobile/test/support/firebase_fakes.dart`.
- Mỗi sự kiện là một method có kiểu trong `AnalyticsService`. Không tự ghép tên sự kiện trong ViewModel.
- Chỉ ghi sự kiện khi thao tác **thành công** (server đã nhận), trừ `ai_error`.
- **Không có thông tin cá nhân:** không email, không họ tên, không MSSV, không SĐT. Người dùng nhận diện bằng id nội bộ của Identity (`setUserId`). Từ khóa tìm kiếm chứa `@` được thay bằng `(redacted)`, chuỗi cắt còn 100 ký tự.
- Ưu tiên tên sự kiện Firebase khuyến nghị (`login`, `search`, `join_group`); tên tự đặt dùng `snake_case`, ≤ 40 ký tự.
- Tham số kiểu `bool` gửi dạng `int` (0/1) vì Analytics chỉ nhận chuỗi và số.

## 2. Sự kiện

| # | Sự kiện | Kích hoạt khi | Tham số (kiểu) | Câu hỏi sản phẩm | Luồng / bước | Màn – nơi gọi | Method |
|---|---|---|---|---|---|---|---|
| 1 | `login` *(khuyến nghị)* | Identity trả token sau Google → Firebase → Identity | `method` (string, `google`) | Bao nhiêu người đăng nhập được, có bị chặn ở A04 không (so với số mở A03) | F1, F2, F3 – bước vào app | A03 – `AuthController.signInWithIdToken` (**đã gắn**) | `logLogin` |
| 2 | `logout` | Người dùng đăng xuất | – | Người dùng đổi tài khoản / bỏ app sau phiên nào | – | A07 – `AuthController.signOut` (**đã gắn**) | `logLogout` |
| 3 | `search` *(khuyến nghị)* | Trang 1 của kết quả tìm nhóm về (có từ khóa hoặc đổi bộ lọc) | `search_term` (string ≤ 100), `result_count` (int), `filter` (string, tùy chọn) | Sinh viên tìm gì, bao nhiêu lần tìm ra 0 kết quả (cần thêm gợi ý S07) | F1 – bước 1 *Khám phá nhóm* | S05 – `TeamsViewModel.search` / `_reload` | `logSearch` |
| 4 | `view_team` | Mở chi tiết nhóm | `team_id` (string), `source` (string: `discover`, `suggestions`, `invitation`, `myTeam`, `lecturer`) | Đường nào dẫn tới nhóm nhiều nhất; gợi ý S07 có được bấm không | F1 – bước 2 *Xem chi tiết*; F3 – L05 | S08 `team_detail_screen.dart` (ViewModel chi tiết nhóm), L05 | `logViewTeam` |
| 5 | `view_lecturer` | Mở chi tiết giảng viên | `lecturer_id` (string) | Nhóm có xem giảng viên trước khi xếp nguyện vọng không | F3 (phía nhóm) | S24 – `lecturer_detail_screen.dart` | `logViewLecturer` |
| 6 | `view_change_request` | Mở chi tiết yêu cầu đổi nhóm | `request_id` (string), `status` (string) | Người dùng quay lại xem trạng thái bao nhiêu lần (chỉ số #2) | F2 – bước cuối *Theo dõi* | S29 – `change_request_screens.dart` | `logViewChangeRequest` |
| 7 | `view_suggestions` | Danh sách nhóm gợi ý tải xong | `count` (int), `max_items` (int, từ Remote Config) | Đổi `suggestions_max_items` có làm tăng `view_team` từ `suggestions` không | F1 – nhánh thay thế | S07 – `suggested_teams_screen.dart` | `logViewSuggestions` |
| 8 | `join_group` *(khuyến nghị)* | Gửi yêu cầu tham gia nhóm thành công | `group_id` (string), `idea_count` (int, 0 = "Chưa có ý tưởng") | Tỷ lệ xem → gửi yêu cầu; form ý tưởng có làm giảm tỷ lệ gửi không (persona §10) | F1 – bước 3 *Gửi yêu cầu* (chỉ số #3) | S09 – sheet gửi yêu cầu (`DiscoverApi.requestToJoin`) | `logJoinGroup` |
| 9 | `create_team` | Tạo nhóm thành công | `team_id` (string), `course` (string, mã môn) | Bao nhiêu sinh viên tự lập nhóm thay vì xin vào | F1 – nhánh *Tự lập nhóm* | S13/S14 – `team_form_screen.dart` (`MyTeamApi.create`) | `logCreateTeam` |
| 10 | `send_invitation` | Trưởng nhóm gửi lời mời thành công | `team_id` (string), `source` (string: `suggestions`, `search`) | Gợi ý thành viên S18 có được dùng hơn tìm tay S19 không | F2 – bước *Mời thành viên* | S18 `suggested_members_screen.dart`, S19 `invite_students_screen.dart` (`SuggestionsApi.invite`) | `logSendInvitation` |
| 11 | `submit_change_request` | Gửi yêu cầu đổi nhóm thành công | `change_count` (int), `elapsed_sec` (int, từ lúc mở S27) | Gửi yêu cầu đổi nhóm có ≤ 2 phút không (chỉ số #1) | F2 – bước *Xác nhận và gửi* | S28 – `change_request_screens.dart` (`ChangeRequestsApi.create`) | `logSubmitChangeRequest` |
| 12 | `export_report` | Tải / mở file thành công | `report_type` (string, ví dụ `workspace_file`), `format` (string, đuôi file) | Workspace có thay được việc gửi file qua Zalo không | Workspace (C05/C06) | C05/C06 – `files_screen.dart`, `file_detail_sheet.dart` (`WorkspaceApi.downloadUrl`) | `logExportReport` |
| 13 | `ai_request` | Gửi yêu cầu gợi ý ý tưởng tới Gemini | `feature` (string, `idea_suggestion`), `model` (string, từ Remote Config) | Tính năng AI có được dùng không, theo model nào | F1 – S09 (MB-55) | S09 – ViewModel gợi ý AI (MB-55) | `logAiRequest` |
| 14 | `ai_result_accepted` | Người dùng chọn một ý tưởng AI đưa vào form | `feature` (string), `index` (int, 0-based) | Kết quả AI có hữu ích không (tỷ lệ accepted / request) | F1 – S09 (MB-55) | S09 – ViewModel gợi ý AI | `logAiResultAccepted` |
| 15 | `ai_error` | Gọi AI lỗi | `feature` (string), `error_code` (string ngắn: `network`, `quota`, `blocked`, `unknown`) | Lỗi AI hay gặp loại nào, có cần tắt bằng `ai_idea_enabled` không | F1 – S09 (MB-55) | S09 – ViewModel gợi ý AI | `logAiError` |

**Thuộc tính người dùng:** `setUserId(<id Identity>)` sau đăng nhập và khi mở lại app có phiên; `setUserId(null)` khi đăng xuất (**đã gắn** trong `AuthController`).

### Phễu cần dựng trên Console (Explore → Funnel)

- **F1 Tham gia nhóm:** `login` → `search` → `view_team` → `join_group`.
- **F2 Đổi thành viên:** `login` → `send_invitation` / `submit_change_request` → `view_change_request`.
- **AI (MB-55):** `ai_request` → `ai_result_accepted` (tỷ lệ chấp nhận), `ai_error` / `ai_request` (tỷ lệ lỗi).

## 3. Crashlytics

| Thứ | Giá trị |
|---|---|
| Lỗi fatal | `FlutterError.onError`, `PlatformDispatcher.instance.onError` (bootstrap) |
| Bản debug | Không thu thập; bật bằng `--dart-define=CRASHLYTICS_DEBUG=true` |
| User id | Id nội bộ Identity (không phải email); xóa khi đăng xuất |
| Custom key | `role` (vd `student`, `lecturer,mentor`), `course` (mã môn của sinh viên, vd `EXE101`) |
| Non-fatal | `CrashReporter.recordNonFatal(error, stack, reason:)`; hiện có `remote_config_fetch`, `remote_config_update`, `remote_config_listen` |
| Thử crash | `CrashReporter.testCrash()` – cần một nút ẩn trong A07 Cài đặt (bản debug / demo) |

## 4. Remote Config

Mặc định trong app (`AppRemoteConfig`) trùng [`firebase/remote-config-defaults.json`](../firebase/remote-config-defaults.json); test `firebase_services_test.dart` kiểm tra hai bên khớp nhau.

| Tham số | Kiểu | Mặc định | Dùng ở | Ghi chú |
|---|---|---|---|---|
| `suggestions_max_items` | int | `5` | S07 – số nhóm gợi ý tối đa | Ngoài 1–20 thì app kẹp lại (≤ 0 → 5) |
| `ai_idea_enabled` | bool | `false` | S09 – hiện nút gợi ý AI (MB-55) | Tắt nhanh khi AI lỗi nhiều hoặc hết quota |
| `ai_model` | string | `gemini-3.8-flash` | MB-55 – model Firebase AI Logic | Trống → mặc định |
| `ai_prompt` | string | prompt ngắn tiếng Việt | MB-55 – prompt hệ thống | Trống → mặc định |

- Khởi động: `setDefaults` → `fetchAndActivate` với timeout 5 giây. Lỗi / mất mạng / quá giờ: giữ mặc định (hoặc giá trị đã kích hoạt lần trước), ghi non-fatal, app vẫn mở.
- Khi chạy: nghe `onConfigUpdated` → `activate` → `remoteConfigProvider` phát giá trị mới, màn đang `ref.watch` vẽ lại, không cần build lại app.
- Khoảng fetch tối thiểu: 1 phút (debug), 1 giờ (release).

## 5. Kiểm tra trên điện thoại thật

```bash
adb shell setprop debug.firebase.analytics.app vn.edu.fpt.exe.exe_team_matching
```

Mở Firebase Console → Analytics → DebugView, làm F1 và chụp các sự kiện. Tắt DebugView:

```bash
adb shell setprop debug.firebase.analytics.app .none.
```
