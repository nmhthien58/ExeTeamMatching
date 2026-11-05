import 'dart:async';

import 'package:firebase_app_check/firebase_app_check.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';

import '../../firebase_options.dart';
import '../config/env.dart';
import 'client_error_reporter.dart';
import 'crash_reporter.dart';
import 'remote_config_service.dart';

/// Khởi tạo Firebase trước `runApp` (MB-47). App Check dùng debug provider khi [Env.appCheckDebug]
/// (bản debug, hoặc APK demo cài tay không qua Google Play); còn lại dùng Play Integrity.
/// MB-50/51: bật Crashlytics và tải Remote Config (có timeout, lỗi thì giữ mặc định).
Future<void> initFirebase() async {
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
  await initCrashlytics();
  try {
    await FirebaseAppCheck.instance.activate(
      providerAndroid: Env.appCheckDebug ? const AndroidDebugProvider() : const AndroidPlayIntegrityProvider(),
    );
  } catch (e) {
    // App Check chưa bật enforcement thì app vẫn chạy được; lỗi chỉ ghi log.
    debugPrint('App Check activate failed: $e');
  }
  // Cùng cách provider mặc định tạo service; service không giữ trạng thái nên provider đọc lại được giá trị.
  await FirebaseRemoteConfigService(crash: FirebaseCrashReporter()).initialize();
  // MB-61: báo cáo fatal của lần chạy trước. Gửi ở nền, không giữ màn khởi động.
  if (Env.crashlyticsEnabled) unawaited(clientErrorReporter.flushPending());
}

/// Crashlytics (MB-50): bản debug tắt thu thập, trừ khi có `--dart-define=CRASHLYTICS_DEBUG=true`.
/// Khi bật: lỗi Flutter (`FlutterError.onError`) và lỗi bất đồng bộ (`PlatformDispatcher.onError`) đều gửi lên,
/// đồng thời lưu một bản để gửi về backend ở lần mở app sau (MB-61).
Future<void> initCrashlytics() async {
  final crashlytics = FirebaseCrashlytics.instance;
  try {
    await crashlytics.setCrashlyticsCollectionEnabled(Env.crashlyticsEnabled);
  } catch (e) {
    debugPrint('Crashlytics setup failed: $e');
    return;
  }
  if (!Env.crashlyticsEnabled) return;
  installFatalHandlers(crashlytics, clientErrorReporter);
}

/// Tách riêng để test với Crashlytics và reporter giả.
@visibleForTesting
void installFatalHandlers(FirebaseCrashlytics crashlytics, ClientErrorReporter clientErrors) {
  // recordFlutterFatalError vẫn in lỗi ra console (FlutterError.presentError).
  FlutterError.onError = (details) {
    unawaited(clientErrors.saveFatal(details.exception, details.stack));
    crashlytics.recordFlutterFatalError(details);
  };
  PlatformDispatcher.instance.onError = (error, stack) {
    unawaited(clientErrors.saveFatal(error, stack));
    crashlytics.recordError(error, stack, fatal: true);
    return true;
  };
}
