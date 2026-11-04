import 'package:firebase_app_check/firebase_app_check.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';

import '../../firebase_options.dart';
import '../config/env.dart';

/// Khởi tạo Firebase trước `runApp` (MB-47). App Check dùng debug provider khi [Env.appCheckDebug]
/// (bản debug, hoặc APK demo cài tay không qua Google Play); còn lại dùng Play Integrity.
Future<void> initFirebase() async {
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
  try {
    await FirebaseAppCheck.instance.activate(
      providerAndroid: Env.appCheckDebug ? const AndroidDebugProvider() : const AndroidPlayIntegrityProvider(),
    );
  } catch (e) {
    // App Check chưa bật enforcement thì app vẫn chạy được; lỗi chỉ ghi log.
    debugPrint('App Check activate failed: $e');
  }
}
