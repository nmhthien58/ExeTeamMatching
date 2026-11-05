import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'client_error_api.dart';

/// Thông tin máy gắn vào báo cáo.
class ClientDeviceInfo {
  const ClientDeviceInfo({required this.platform, required this.appVersion, this.deviceModel, this.osVersion});

  final String platform;
  final String appVersion;
  final String? deviceModel;
  final String? osVersion;
}

const _deviceChannel = MethodChannel('exe_team_matching/device_info');

/// Đọc qua kênh trong `MainActivity.kt`. iOS chưa có kênh này nên chỉ có tên nền tảng và phiên bản hệ điều hành.
Future<ClientDeviceInfo> _readDeviceInfo() async {
  var info = const <String, String>{};
  try {
    info = await _deviceChannel.invokeMapMethod<String, String>('read') ?? info;
  } catch (e) {
    debugPrint('Read device info failed: $e');
  }
  return ClientDeviceInfo(
    platform: Platform.operatingSystem,
    appVersion: info['appVersion'] ?? 'unknown',
    deviceModel: info['deviceModel'],
    osVersion: info['osVersion'] ?? Platform.operatingSystemVersion,
  );
}

/// Gửi bản báo cáo lỗi về backend song song với Crashlytics, để Bộ môn xem ở W30 (MB-61, BE-87).
/// Firebase không có API đọc Crashlytics miễn phí nên web không lấy được từ đó.
///
/// - Non-fatal: gửi ngay ở nền, lỗi mạng thì bỏ qua.
/// - Fatal: lưu vào shared_preferences rồi gửi ở lần mở app sau, giống cách Crashlytics gửi crash.
///
/// Mọi method không ném lỗi: báo lỗi hỏng thì app vẫn chạy. Chỉ gửi id nội bộ; email trong nội dung lỗi bị che.
class ClientErrorReporter {
  ClientErrorReporter({
    ClientErrorApi? api,
    Future<ClientDeviceInfo> Function()? deviceInfo,
    DateTime Function()? now,
  }) : _api = api ?? DioClientErrorApi(),
       _readInfo = deviceInfo ?? _readDeviceInfo,
       _now = now ?? DateTime.now;

  /// Giới hạn số báo cáo chờ gửi: lỗi lặp theo từng frame không được làm phình bộ nhớ máy.
  static const maxPending = 20;

  static const pendingKey = 'exe.pendingClientErrors';

  static final _email = RegExp(r'[\w.+-]+@[\w-]+\.[\w.-]+');

  final ClientErrorApi _api;
  final Future<ClientDeviceInfo> Function() _readInfo;
  final DateTime Function() _now;
  Future<ClientDeviceInfo>? _info;

  /// Id nội bộ của người đang đăng nhập; `null` khi chưa đăng nhập.
  String? userId;
  String? role;

  /// Route đang mở, dạng mẫu (`/sinh-vien/nhom/:id`) nên không chứa id cụ thể.
  String? screen;

  /// Gửi một lỗi non-fatal.
  Future<void> reportNonFatal(Object error, StackTrace? stack, {String? reason}) async {
    try {
      await _api.send(await _build('non_fatal', error, stack, reason: reason));
    } catch (e) {
      debugPrint('Send client error failed: $e');
    }
  }

  /// Lưu một lỗi fatal để lần mở app sau gửi.
  Future<void> saveFatal(Object error, StackTrace? stack) async {
    try {
      final report = jsonEncode(await _build('fatal', error, stack));
      final prefs = await SharedPreferences.getInstance();
      final pending = prefs.getStringList(pendingKey) ?? const <String>[];
      final kept = pending.length >= maxPending ? pending.sublist(pending.length - maxPending + 1) : pending;
      await prefs.setStringList(pendingKey, [...kept, report]);
    } catch (e) {
      debugPrint('Save fatal client error failed: $e');
    }
  }

  /// Gửi các báo cáo fatal đang chờ. Báo cáo nào server đã nhận hoặc từ chối thì xóa;
  /// gặp lỗi mạng thì dừng, phần còn lại đợi lần mở app sau.
  Future<void> flushPending() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      for (final raw in prefs.getStringList(pendingKey) ?? const <String>[]) {
        final report = _decode(raw);
        final result = report == null ? ClientErrorSendResult.rejected : await _api.send(report);
        if (result == ClientErrorSendResult.retryLater) return;
        // Đọc lại danh sách: trong lúc gửi có thể có lỗi mới được lưu thêm.
        final left = [...?prefs.getStringList(pendingKey)]..remove(raw);
        await prefs.setStringList(pendingKey, left);
      }
    } catch (e) {
      debugPrint('Flush client errors failed: $e');
    }
  }

  Map<String, Object?>? _decode(String raw) {
    try {
      return (jsonDecode(raw) as Map).cast<String, Object?>();
    } on FormatException {
      return null;
    } on TypeError {
      return null;
    }
  }

  Future<Map<String, Object?>> _build(String kind, Object error, StackTrace? stack, {String? reason}) async {
    final info = await (_info ??= _readInfo());
    final message = reason == null ? '$error' : '$reason: $error';
    final trace = stack?.toString() ?? '';
    return {
      'kind': kind,
      'message': _fit(_mask(message), 2000) ?? 'Unknown',
      'exceptionType': _fit(error.runtimeType.toString(), 200),
      'stackTrace': _fit(_mask(trace), 8192),
      'platform': _fit(info.platform, 20),
      'appVersion': _fit(info.appVersion, 30),
      'deviceModel': _fit(info.deviceModel, 100),
      'osVersion': _fit(info.osVersion, 100),
      'userId': userId,
      'role': _fit(role, 20),
      'screen': _fit(screen, 200),
      'occurredAt': _now().toUtc().toIso8601String(),
    };
  }

  /// Nội dung lỗi có thể chứa email (ví dụ thông báo từ API); backend không được nhận email.
  String _mask(String text) => text.replaceAll(_email, '[email]');

  /// Cắt theo giới hạn của backend; chuỗi rỗng thành `null`.
  String? _fit(String? value, int max) {
    if (value == null || value.isEmpty) return null;
    return value.length > max ? value.substring(0, max) : value;
  }
}

/// Dùng chung cho `initCrashlytics` (chạy trước `ProviderScope`) và mọi [FirebaseCrashReporter].
final clientErrorReporter = ClientErrorReporter();
