# Bằng chứng Firebase trên máy thật (MB-50, MB-51)

Chụp ngày 05/11/2026 trên Samsung Galaxy A71 (Android, cắm cáp, `adb reverse tcp:8080`), app build debug từ `develop` (`2d7cc98`) với `--dart-define=CRASHLYTICS_DEBUG=true --dart-define=FIREBASE_DEMO=true`, backend Docker trên laptop. Tài khoản sinh viên EXE101; ảnh A07 đã cắt bỏ dòng email.

## MB-51 – Remote Config đổi hành vi app mà không build lại

| Ảnh | Nội dung |
|---|---|
| `mb51-1-s07-truoc-suggestions_max_items-5.png` | S07 "Gợi ý cho bạn" khi `suggestions_max_items = 5`: danh sách có từ 3 nhóm trở lên |
| `mb51-2-s07-sau-suggestions_max_items-2.png` | Đổi `suggestions_max_items = 2` trên Firebase Console → Publish lúc 18:33; mở lại S07 lúc 18:34 (không cài lại app): còn đúng 2 nhóm |
| `mb51-3-a07-demo-firebase-gia-tri-remote-config.png` | A07 → Demo Firebase: "Số nhóm gợi ý tối đa: 2", "Gợi ý ý tưởng bằng AI: bật · gemini-2.5-flash" – giá trị đang dùng đọc từ Remote Config |

Sau khi chụp đã đặt lại `suggestions_max_items = 5` trên Console.

## MB-50 – Crashlytics

| Ảnh | Nội dung |
|---|---|
| `mb50-1-gui-loi-thu-non-fatal.png` | A07 → "Gửi lỗi thử (non-fatal)" → xác nhận: SnackBar "Đã gửi lỗi thử…"; logcat có khối `FIREBASE CRASHLYTICS` |
| `mb50-2-crash-thu-xac-nhan.png` | A07 → "Crash thử": hộp xác nhận trước khi app đóng |

Sau khi bấm "Crash thử", app đóng (`AndroidRuntime` ghi crash từ `FlutterFirebaseCrashlyticsPlugin`); mở lại app thì Crashlytics gửi báo cáo (`crashlyticsreports-pa.googleapis.com … batchlog` trong logcat).

Cùng lúc, MB-61 gửi bản sao về backend: bảng `identity.client_error_reports` có 1 dòng `non_fatal / DemoNonFatalError` và 2 dòng `fatal / TestCrashError` (màn `/sv/ho-so`), Bộ môn xem ở W30 "Theo dõi lỗi ứng dụng".

## Còn thiếu (chụp trên Chrome của Thiên)

- [ ] Firebase Console → Crashlytics: issue `TestCrashError` (fatal) và `DemoNonFatalError` (non-fatal), kèm custom key và user ID.
- [ ] Firebase Console → Analytics → DebugView: các sự kiện khi thao tác (thiết bị đã bật debug: `adb shell setprop debug.firebase.analytics.app vn.edu.fpt.exe.exe_team_matching`).
- [ ] Firebase Console → Remote Config: tham số `suggestions_max_items` trước / sau khi đổi.
- [ ] Web W30 (`/admin/loi-ung-dung`): 3 báo cáo lỗi ở trên.
