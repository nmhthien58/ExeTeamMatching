# Bằng chứng Firebase trên máy thật (MB-50, MB-51)

Chụp ngày 05/11/2026 trên Samsung Galaxy A71 (Android, cắm cáp, `adb reverse tcp:8080`), app build debug từ `develop` (`2d7cc98`) với `--dart-define=CRASHLYTICS_DEBUG=true --dart-define=FIREBASE_DEMO=true`, backend Docker trên laptop. Tài khoản sinh viên EXE101; ảnh A07 đã cắt bỏ dòng email.

## MB-51 – Remote Config đổi hành vi app mà không build lại

| Ảnh | Nội dung |
|---|---|
| `mb51-1-s07-truoc-suggestions_max_items-5.png` | S07 "Gợi ý cho bạn" khi `suggestions_max_items = 5`: danh sách có từ 3 nhóm trở lên |
| `mb51-2-s07-sau-suggestions_max_items-2.png` | Đổi `suggestions_max_items = 2` trên Firebase Console → Publish lúc 18:33; mở lại S07 lúc 18:34 (không cài lại app): còn đúng 2 nhóm |
| `mb51-3-a07-demo-firebase-gia-tri-remote-config.png` | A07 → Demo Firebase: "Số nhóm gợi ý tối đa: 2", "Gợi ý ý tưởng bằng AI: bật · gemini-2.5-flash" – giá trị đang dùng đọc từ Remote Config |

| `mb51-4-console-tham-so-remote-config.png` | Firebase Console → Remote Config (Version 4): 5 tham số `ai_idea_enabled`, `ai_model`, `ai_prompt`, `suggestions_max_items`, `web_notice`, sau khi đặt lại `suggestions_max_items = 5` |
| `mb51-5-console-lich-su-doi-gia-tri.webp` | Remote Config → Change history: Version 5 (05/11, 23:14) `suggestions_max_items = 2` so với Version 6 (23:28) đặt lại `5`; các tham số khác không đổi |

Sau khi chụp đã đặt lại `suggestions_max_items = 5` trên Console.

## MB-50 – Crashlytics

| Ảnh | Nội dung |
|---|---|
| `mb50-1-gui-loi-thu-non-fatal.png` | A07 → "Gửi lỗi thử (non-fatal)" → xác nhận: SnackBar "Đã gửi lỗi thử…"; logcat có khối `FIREBASE CRASHLYTICS` |
| `mb50-2-crash-thu-xac-nhan.png` | A07 → "Crash thử": hộp xác nhận trước khi app đóng |
| `mb50-3-console-crashlytics-crashes.webp` | Firebase Console → Crashlytics, lọc Crashes: 2 issue `FlutterFirebaseCrashlyticsPlugin.crash` ("This is a test crash…") – crash native từ nút "Crash thử" |
| `mb50-4-console-crashlytics-crash-va-non-fatal.webp` | Lọc Crashes + Non-fatals: `FirebaseDemoViewModel.sendNonFatal` (DemoNonFatalError, 3 lần), 2 issue crash thử, và 1 non-fatal **từ luồng lỗi thật** `FirebaseRemoteConfigService.initialize` (fetch Remote Config lỗi, app vẫn chạy bằng giá trị mặc định) |
| `mb50-5-console-crashlytics-custom-keys.png` | Chi tiết sự kiện → Keys: `course = EXE101`, `role = student`, `screen = /sv/ho-so`, `demo_action = test_crash` (custom key app tự ghi) |
| `mb50-6-console-crashlytics-user-id.png` | Chi tiết sự kiện → Data: User ID là mã tài khoản (GUID), không có email; máy Samsung SM-A715F, Android 13 |

Sau khi bấm "Crash thử", app đóng (`AndroidRuntime` ghi crash từ `FlutterFirebaseCrashlyticsPlugin`); mở lại app thì Crashlytics gửi báo cáo (`crashlyticsreports-pa.googleapis.com … batchlog` trong logcat).

Cùng lúc, MB-61 gửi bản sao về backend: bảng `identity.client_error_reports` có 1 dòng `non_fatal / DemoNonFatalError` và 2 dòng `fatal / TestCrashError` (màn `/sv/ho-so`), Bộ môn xem ở W30 "Theo dõi lỗi ứng dụng".


## MB-50 – Analytics (DebugView)

Chụp 06/11/2026 trên APK release (build từ `develop` `e1b5680`, backend Railway, `FIREBASE_DEMO=true`), máy đã bật `debug.firebase.analytics.app`.

| Ảnh | Nội dung |
|---|---|
| `mb50-7-debugview-su-kien.webp` | DebugView, thiết bị Samsung SM-A715F: chuỗi `user_id` → `logout` → `login` → `view_team` → `view_suggestions` cùng `screen_view`, `session_start`; user property `user_id` (mã ẩn danh) |
| `mb50-8-debugview-tham-so-view-team.webp` | Chi tiết sự kiện `view_team`: tham số `team_id`, `source` (không gửi tên hay email) |

## Còn thiếu (chụp trên Chrome của Thiên)

- [x] Firebase Console → Crashlytics (`mb50-3` … `mb50-6`): issue `TestCrashError` (fatal) và `DemoNonFatalError` (non-fatal), kèm custom key và user ID.
- [x] Firebase Console → Analytics → DebugView (`mb50-7`, `mb50-8`): các sự kiện khi thao tác (thiết bị đã bật debug: `adb shell setprop debug.firebase.analytics.app vn.edu.fpt.exe.exe_team_matching`).
- [x] Firebase Console → Remote Config: tham số `suggestions_max_items` trước / sau khi đổi (`mb51-4`, `mb51-5`).
- [ ] Web W30 (`/admin/loi-ung-dung`): 3 báo cáo lỗi ở trên.
