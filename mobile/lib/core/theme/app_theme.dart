import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app_tokens.dart';

/// Theme Material 3 (MB-02, MB-37): ColorScheme khai báo tay theo DESIGN.md bản 5 (không dùng fromSeed),
/// chữ Be Vietnam Pro 3 độ đậm, component theo flutter-handoff mục 2.
abstract final class AppTheme {
  /// Test đặt `false` để không tải font qua mạng (google_fonts).
  static bool useGoogleFonts = true;

  static const _scheme = ColorScheme(
    brightness: Brightness.light,
    primary: Palette.primary,
    onPrimary: Colors.white,
    primaryContainer: Palette.primaryContainer,
    onPrimaryContainer: Palette.primaryDark,
    // Bản 5 bỏ màu nhấn thứ hai: secondary = Primary Dark (DESIGN.md 12.1); container dùng cho
    // chip lọc, SegmentedButton, NavigationBar khi đang chọn nên cũng là cam nhạt.
    secondary: Palette.primaryDark,
    onSecondary: Colors.white,
    secondaryContainer: Palette.primaryContainer,
    onSecondaryContainer: Palette.primaryDark,
    tertiary: Palette.primaryDark,
    onTertiary: Colors.white,
    error: Palette.error,
    onError: Colors.white,
    errorContainer: Palette.errorContainer,
    onErrorContainer: Palette.error,
    surface: Palette.surface,
    onSurface: Palette.textPrimary,
    onSurfaceVariant: Palette.textSecondary,
    surfaceContainerLowest: Palette.surface,
    surfaceContainerLow: Palette.background,
    surfaceContainer: Palette.background,
    surfaceContainerHigh: Palette.neutralContainer,
    surfaceContainerHighest: Palette.neutralContainer,
    outline: Palette.borderStrong,
    outlineVariant: Palette.border,
    shadow: Palette.textPrimary,
    scrim: Color(0x661A1F36),
    inverseSurface: Palette.textPrimary,
    onInverseSurface: Colors.white,
    inversePrimary: Palette.primaryBorder,
  );

  /// Cỡ / dòng / độ đậm theo DESIGN.md mục 3 (Display → headlineSmall … Caption Bold → labelSmall).
  static TextTheme _textTheme(TextTheme base) {
    TextStyle s(double size, double height, FontWeight w) =>
        TextStyle(fontSize: size, height: height / size, fontWeight: w, color: Palette.textPrimary);
    final t = TextTheme(
      headlineSmall: s(24, 32, FontWeight.w600), // Display
      titleLarge: s(20, 28, FontWeight.w600), // Title
      titleMedium: s(16, 24, FontWeight.w600), // Subtitle
      bodyLarge: s(16, 24, FontWeight.w400), // Body Large
      bodyMedium: s(14, 20, FontWeight.w400), // Body
      labelLarge: s(14, 20, FontWeight.w500), // Label
      bodySmall: s(12, 16, FontWeight.w400).copyWith(color: Palette.textSecondary), // Caption
      labelSmall: s(12, 16, FontWeight.w600), // Caption Bold
    );
    final merged = base.merge(t);
    return useGoogleFonts ? GoogleFonts.beVietnamProTextTheme(merged) : merged;
  }

  static ThemeData light() {
    final base = ThemeData(useMaterial3: true, colorScheme: _scheme);
    final text = _textTheme(base.textTheme);
    final roundedMd = RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md));
    const buttonSize = Size(64, 48); // vùng chạm 48 (DESIGN.md 4.4)

    return base.copyWith(
      scaffoldBackgroundColor: Palette.background,
      textTheme: text,
      materialTapTargetSize: MaterialTapTargetSize.padded,
      extensions: const [AppColors.light],
      // Màn cấp 2 (push): kiểu chuyển trang mới của Android (trượt + mờ dần), mượt hơn kiểu zoom mặc định.
      pageTransitionsTheme: PageTransitionsTheme(builders: {
        TargetPlatform.android: const FadeForwardsPageTransitionsBuilder(),
      }),
      appBarTheme: AppBarTheme(
        backgroundColor: Palette.surface,
        foregroundColor: Palette.textPrimary,
        elevation: 0,
        scrolledUnderElevation: 1,
        surfaceTintColor: Colors.transparent,
        titleTextStyle: text.titleLarge,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(minimumSize: buttonSize, shape: roundedMd, textStyle: text.labelLarge),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: buttonSize,
          shape: roundedMd,
          side: const BorderSide(color: Palette.primary),
          foregroundColor: Palette.primaryDark,
          textStyle: text.labelLarge,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(minimumSize: buttonSize, foregroundColor: Palette.primaryDark, textStyle: text.labelLarge),
      ),
      cardTheme: CardThemeData(
        color: Palette.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.lg),
          side: const BorderSide(color: Palette.border),
        ),
      ),
      chipTheme: ChipThemeData(
        // Material 3 lấy màu chữ chip đang chọn từ labelStyle (không dùng secondaryLabelStyle): chữ đậm trên nền cam chỉ đạt
        // tương phản 3,5:1, nên đổi sang trắng khi chọn (4,6:1).
        labelStyle: text.labelLarge?.copyWith(
          color: WidgetStateColor.resolveWith((s) => s.contains(WidgetState.selected) ? Colors.white : Palette.textPrimary),
        ),
        shape: const StadiumBorder(),
        side: const BorderSide(color: Palette.border),
        selectedColor: Palette.primary,
        secondaryLabelStyle: text.labelLarge?.copyWith(color: Colors.white),
        checkmarkColor: Colors.white,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Palette.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: Palette.borderStrong),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: Palette.primary, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          borderSide: const BorderSide(color: Palette.error),
        ),
        hintStyle: text.bodyLarge?.copyWith(color: Palette.textSecondary),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: Palette.surface,
        indicatorColor: Palette.primaryContainer,
        surfaceTintColor: Colors.transparent,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => text.labelSmall?.copyWith(
            color: states.contains(WidgetState.selected) ? Palette.primaryDark : Palette.textSecondary,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            color: states.contains(WidgetState.selected) ? Palette.primary : Palette.textSecondary,
          ),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: Palette.textPrimary,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: Palette.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.xl)),
      ),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: Palette.surface,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadius.xl))),
      ),
      dividerTheme: const DividerThemeData(color: Palette.border, space: 1),
    );
  }
}
