# Mobile (Flutter) – Sinh viên và Giảng viên

```bash
cd mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080   # Android emulator → Gateway trên máy
flutter analyze
flutter test
```

Cấu trúc: `lib/app` (MaterialApp, router), `lib/core` (cấu hình, lớp mạng, theme – Thiên), `lib/shared` (widget dùng chung – Thiên), `lib/features/<mảng>` (màn hình của từng thành viên, xem AGENTS.md mục 2).
State management: Riverpod. Điều hướng: go_router. Spec giao diện: docs/ux/flutter-handoff.md.
