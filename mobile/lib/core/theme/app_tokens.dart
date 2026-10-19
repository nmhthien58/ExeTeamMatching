import 'package:flutter/material.dart';

/// Token màu theo docs/ux/DESIGN.md bản 4 (mục 2). Chỉ dùng trong lớp theme; màn hình đọc qua
/// `Theme.of(context).colorScheme` hoặc `context.appColors`, không viết `Color(...)` trực tiếp.
abstract final class Palette {
  // Thương hiệu – cam = hành động, xanh = vị trí / lựa chọn
  static const primary = Color(0xFFC2410C);
  static const primaryDark = Color(0xFF9A3412);
  static const primaryContainer = Color(0xFFFFF1E8);
  static const primaryBorder = Color(0xFFFED7AA);
  static const brandOrange = Color(0xFFF37021); // chỉ trang trí
  static const secondary = Color(0xFF0E5BA8);
  static const secondaryDark = Color(0xFF0A4D8C);
  static const secondaryContainer = Color(0xFFE8F2FC);

  // Nền, viền, chữ
  static const surface = Color(0xFFFFFFFF);
  static const background = Color(0xFFF7F9FC);
  static const border = Color(0xFFE3E8EF);
  static const borderStrong = Color(0xFF7A8599);
  static const textPrimary = Color(0xFF1A1F36);
  static const textSecondary = Color(0xFF5B6478);
  static const textDisabled = Color(0xFFA3ABBA);

  // Trạng thái
  static const success = Color(0xFF1E7F3C);
  static const successContainer = Color(0xFFE7F6EC);
  static const warning = Color(0xFF8A5300);
  static const warningContainer = Color(0xFFFFF4E0);
  static const error = Color(0xFFB3261E);
  static const errorContainer = Color(0xFFFDECEA);
  static const neutralContainer = Color(0xFFEEF1F5);
}

/// Khoảng cách (DESIGN.md 4.1), lưới 4 px.
abstract final class AppSpacing {
  static const s1 = 4.0;
  static const s2 = 8.0;
  static const s3 = 12.0;
  static const s4 = 16.0; // lề màn hình, padding thẻ
  static const s6 = 24.0;
  static const s8 = 32.0;
}

/// Bo góc (DESIGN.md 4.2).
abstract final class AppRadius {
  static const sm = 8.0;
  static const md = 12.0; // nút, ô nhập, banner
  static const lg = 20.0; // thẻ
  static const xl = 28.0; // bottom sheet, dialog
  static const full = 999.0;
}

/// Chuyển động (DESIGN.md 4.5). Đóng nhanh hơn mở; giảm chuyển động thì dùng [Duration.zero].
abstract final class AppMotion {
  static const fast = Duration(milliseconds: 100);
  static const base = Duration(milliseconds: 200);
  static const slow = Duration(milliseconds: 300);

  static Duration of(BuildContext context, Duration d) =>
      MediaQuery.maybeDisableAnimationsOf(context) == true ? Duration.zero : d;
}

/// Nhóm màu Material không có vai trò tương ứng (DESIGN.md 2.3, flutter-handoff mục 1).
@immutable
class AppColors extends ThemeExtension<AppColors> {
  const AppColors({
    required this.brandOrange,
    required this.primaryBorder,
    required this.background,
    required this.textDisabled,
    required this.success,
    required this.successContainer,
    required this.warning,
    required this.warningContainer,
    required this.neutralContainer,
  });

  static const light = AppColors(
    brandOrange: Palette.brandOrange,
    primaryBorder: Palette.primaryBorder,
    background: Palette.background,
    textDisabled: Palette.textDisabled,
    success: Palette.success,
    successContainer: Palette.successContainer,
    warning: Palette.warning,
    warningContainer: Palette.warningContainer,
    neutralContainer: Palette.neutralContainer,
  );

  final Color brandOrange;
  final Color primaryBorder;
  final Color background;
  final Color textDisabled;
  final Color success;
  final Color successContainer;
  final Color warning;
  final Color warningContainer;
  final Color neutralContainer;

  @override
  AppColors copyWith({
    Color? brandOrange,
    Color? primaryBorder,
    Color? background,
    Color? textDisabled,
    Color? success,
    Color? successContainer,
    Color? warning,
    Color? warningContainer,
    Color? neutralContainer,
  }) =>
      AppColors(
        brandOrange: brandOrange ?? this.brandOrange,
        primaryBorder: primaryBorder ?? this.primaryBorder,
        background: background ?? this.background,
        textDisabled: textDisabled ?? this.textDisabled,
        success: success ?? this.success,
        successContainer: successContainer ?? this.successContainer,
        warning: warning ?? this.warning,
        warningContainer: warningContainer ?? this.warningContainer,
        neutralContainer: neutralContainer ?? this.neutralContainer,
      );

  @override
  AppColors lerp(AppColors? other, double t) {
    if (other == null) return this;
    return AppColors(
      brandOrange: Color.lerp(brandOrange, other.brandOrange, t)!,
      primaryBorder: Color.lerp(primaryBorder, other.primaryBorder, t)!,
      background: Color.lerp(background, other.background, t)!,
      textDisabled: Color.lerp(textDisabled, other.textDisabled, t)!,
      success: Color.lerp(success, other.success, t)!,
      successContainer: Color.lerp(successContainer, other.successContainer, t)!,
      warning: Color.lerp(warning, other.warning, t)!,
      warningContainer: Color.lerp(warningContainer, other.warningContainer, t)!,
      neutralContainer: Color.lerp(neutralContainer, other.neutralContainer, t)!,
    );
  }
}

extension AppColorsContext on BuildContext {
  /// `context.appColors.success` …
  AppColors get appColors => Theme.of(this).extension<AppColors>() ?? AppColors.light;
}
