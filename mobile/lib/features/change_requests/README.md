# MB-29 / MB-30 — Đổi nhóm EXE201 (S26–S29)

Có màn S26–S29 và API client theo `docs/contracts/openapi/team.yaml` (Team Service BE-45).

### Trạng thái triển khai
- **Router:** Đã đăng ký `changeRequestRoutes` và các alias (`/sv/doi-nhom`, `/sv/nhom/doi-nhom`, `/sv/change-requests`, `/app://change-requests`) trong `mobile/lib/app/feature_routes.dart` thuộc `Shell.student`.
- **Lối vào:** Đã nối từ màn hình S15/S13 (`my_team_screen.dart`, `team_form_screen.dart`) cho nhóm kế thừa EXE201 và banner thay đổi chờ duyệt.
- **Deep link:** Đã nối `app://change-requests` từ thông báo A06 vào thẳng S26 (`/sv/yeu-cau-doi-nhom`).
- **Kiểm thử:** 15 widget tests (`change_requests_test.dart`, `change_requests_screen_test.dart`) và 8 tests (`my_team_test.dart`) đều vượt qua.
- **Phân tích tĩnh:** `flutter analyze` đạt 0 lỗi, 0 cảnh báo.
- **Build APK:** Lỗi build APK do plugin dùng chung `file_picker` (thuộc Nguyên phụ trách) không chặn PR này theo xác nhận của Thiên.
