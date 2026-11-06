import 'package:flutter/material.dart';

/// Token màu theo docs/ux/DESIGN.md bản 5 (mục 2, theo Sổ tay thương hiệu FPT). Chỉ dùng trong lớp theme; màn hình đọc qua
/// `Theme.of(context).colorScheme` hoặc `context.appColors`, không viết `Color(...)` trực tiếp.
abstract final class Palette {
  // Thương hiệu – cam = bấm được / đang chọn, xanh dương FPT = thông tin, xanh lá = thành công
  static const primary = Color(0xFFC6510B);
  static const primaryDark = Color(0xFFA8440F);
  static const primaryContainer = Color(0xFFFFF3EB);
  static const primaryBorder = Color(0xFFFBC9A8);
  static const brandOrange = Color(0xFFF37021); // chỉ trang trí
  static const info = Color(0xFF0066B3); // không dùng cho điều khiển
  static const infoDark = Color(0xFF004C87);
  static const infoContainer = Color(0xFFE6F0F8);
  static const brandGreen = Color(0xFF0DB14B); // chỉ trang trí

  // Nền, viền, chữ
  static const surface = Color(0xFFFFFFFF);
  static const background = Color(0xFFF7F6F4); // bản 6: xám ấm theo cam (bản 5: #F7F8F9)
  static const border = Color(0xFFE3E6EA);
  static const borderStrong = Color(0xFF7D8590);
  static const textPrimary = Color(0xFF1A1F36);
  static const textSecondary = Color(0xFF5B6370);
  static const textDisabled = Color(0xFFA7A9AC);

  // Trạng thái
  static const success = Color(0xFF0A7E3A);
  static const successContainer = Color(0xFFE7F7ED);
  static const warning = Color(0xFF8A5300);
  static const warningContainer = Color(0xFFFFF4E0);
  static const error = Color(0xFFB3261E);
  static const errorContainer = Color(0xFFFDECEA);
  static const neutralContainer = Color(0xFFEEF0F2);
}

/// Bản 6 (06/11/2026, MB-64): chiều sâu thay cho viền. Bóng hai tầng màu nâu ấm (không dùng đen thuần) để hợp nền cam;
/// bóng nổi ngả cam cho khối nhấn (đầu trang, logo). Chỉ dùng trong theme và widget dùng chung.
abstract final class AppShadows {
  static const _warm = Color(0xFF3C1E0A);
  static const card = [
    BoxShadow(color: Color(0x0F3C1E0A), blurRadius: 2, offset: Offset(0, 1)),
    BoxShadow(color: Color(0x0F3C1E0A), blurRadius: 8, offset: Offset(0, 2)),
  ];
  static const raised = [
    BoxShadow(color: Color(0x0F3C1E0A), blurRadius: 4, offset: Offset(0, 2)),
    BoxShadow(color: Color(0x2EC6510B), blurRadius: 24, spreadRadius: -6, offset: Offset(0, 10)),
  ];

  /// Màu bóng cho Material `elevation` (Card, NavigationBar).
  static const materialShadow = _warm;
}

/// Dải màu trang trí (bản 6): FPT Orange → Primary → cam đậm. Chỉ cho khối đầu trang và logo, chữ trên dải là trắng đậm
/// cỡ ≥ 16 (đạt 3:1 ở đầu sáng nhất, 4,5:1 từ giữa dải nơi đặt chữ nhỏ).
abstract final class AppGradients {
  static const hero = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [Palette.brandOrange, Palette.primary, Color(0xFF8F3A08)],
    stops: [0, .62, 1],
  );
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
  static const tab = Duration(milliseconds: 280); // bản 6: đổi tab
  static const slow = Duration(milliseconds: 300);

  static Duration of(BuildContext context, Duration d) =>
      MediaQuery.maybeDisableAnimationsOf(context) == true ? Duration.zero : d;
}

/// Nhóm màu Material không có vai trò tương ứng: Info, trạng thái, trang trí (DESIGN.md 2.1, 2.3, 12.1).
@immutable
class AppColors extends ThemeExtension<AppColors> {
  const AppColors({
    required this.brandOrange,
    required this.brandGreen,
    required this.primaryBorder,
    required this.info,
    required this.infoContainer,
    required this.onInfoContainer,
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
    brandGreen: Palette.brandGreen,
    primaryBorder: Palette.primaryBorder,
    info: Palette.info,
    infoContainer: Palette.infoContainer,
    onInfoContainer: Palette.infoDark,
    background: Palette.background,
    textDisabled: Palette.textDisabled,
    success: Palette.success,
    successContainer: Palette.successContainer,
    warning: Palette.warning,
    warningContainer: Palette.warningContainer,
    neutralContainer: Palette.neutralContainer,
  );

  final Color brandOrange;
  final Color brandGreen;
  final Color primaryBorder;
  final Color info;
  final Color infoContainer;
  final Color onInfoContainer;
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
    Color? brandGreen,
    Color? primaryBorder,
    Color? info,
    Color? infoContainer,
    Color? onInfoContainer,
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
        brandGreen: brandGreen ?? this.brandGreen,
        primaryBorder: primaryBorder ?? this.primaryBorder,
        info: info ?? this.info,
        infoContainer: infoContainer ?? this.infoContainer,
        onInfoContainer: onInfoContainer ?? this.onInfoContainer,
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
      brandGreen: Color.lerp(brandGreen, other.brandGreen, t)!,
      primaryBorder: Color.lerp(primaryBorder, other.primaryBorder, t)!,
      info: Color.lerp(info, other.info, t)!,
      infoContainer: Color.lerp(infoContainer, other.infoContainer, t)!,
      onInfoContainer: Color.lerp(onInfoContainer, other.onInfoContainer, t)!,
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
