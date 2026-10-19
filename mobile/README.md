# Mobile (Flutter) – Sinh viên, Giảng viên, Mentor

```bash
cd mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080 
flutter analyze
flutter test
flutter build apk --debug
```

Cấu trúc: `lib/app` (MaterialApp, router, shell theo vai trò), `lib/core` (cấu hình, phiên đăng nhập, lớp mạng, theme, giai đoạn môn – Thiên), `lib/shared/widgets` (widget dùng chung A08 – Thiên), `lib/features/<mảng>` (màn hình của từng thành viên, AGENTS.md mục 2). State: Riverpod. Điều hướng: go_router. Spec: `docs/ux/flutter-handoff.md`.

## Nền dùng chung (MB-02 → MB-05, MB-37)

| Cần gì | Dùng |
|---|---|
| Màu, chữ | `Theme.of(context).colorScheme` / `.textTheme` (Display = `headlineSmall`, Body = `bodyMedium`…); màu trạng thái `context.appColors.success`… Không viết `Color(...)` hay `fontSize` trong màn hình |
| Khoảng cách, bo góc, chuyển động | `AppSpacing.s4`, `AppRadius.lg`, `AppMotion.of(context, AppMotion.base)` (`lib/core/theme/app_tokens.dart`) |
| Gọi API | `ref.watch(dioProvider)` – tự gắn JWT, tự refresh khi 401. Lỗi: `catch (e) { final err = e.apiError; }` → `ApiException` có `code` (`round-closed`…), `fieldErrors`, `isNetwork` |
| Người dùng | `ref.watch(authControllerProvider)`; đăng xuất `ref.read(authControllerProvider.notifier).signOut()` |
| Giai đoạn môn | `ref.watch(shellPhaseProvider)`; API trả `409 round-closed` thì gọi `showPhaseClosedDialog(context)` |
| Widget dùng chung (A08) | `EmptyState`, `ErrorState` (Thử lại, mất mạng, 403), `ErrorBanner`, `Skeleton`, `SkeletonList`, `LoadingButton`, `DeadlineBanner` (4 mức), `TeamRequirementBar` (gọn / đầy đủ) |

## Khung theo vai trò

- Chưa đăng nhập → A03 (`/dang-nhap`); email không có trong danh sách → A04. Tài khoản chỉ có vai trò Admin → màn nhắc dùng web.
- Sinh viên `/sv/*`, giảng viên `/gv/*` (gồm nhóm mình làm mentor), mentor `/mentor/*`. Tab bar đổi theo giai đoạn: Khám phá ↔ Dự án (sinh viên), Đề tài trong đợt lập nhóm / Lịch trong học kỳ (giảng viên), Đề tài trước tuần 6 / Nhóm từ tuần 6 (mentor).
- Danh sách tab kèm mã màn hình và người phụ trách: `lib/app/navigation.dart`. Mỗi tab đang là `ScreenPlaceholder`; người làm thay bằng màn hình trong `lib/features/<mảng>/` và thêm màn cấp 2 vào `lib/app/router.dart`.
- Đường dẫn cũ tới Khám phá sau khi đợt đóng mở hộp nhắc (`TeamFormationOnly`), theo quy tắc 18.
