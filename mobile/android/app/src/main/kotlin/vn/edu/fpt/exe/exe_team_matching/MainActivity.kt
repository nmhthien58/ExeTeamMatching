package vn.edu.fpt.exe.exe_team_matching

import android.os.Build
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    // MB-61: thông tin máy cho báo cáo lỗi. Không dùng device_info_plus, package_info_plus vì hai plugin
    // luôn áp dụng 'kotlin-android', làm build lỗi với AGP 9 (android.builtInKotlin=true).
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "exe_team_matching/device_info")
            .setMethodCallHandler { call, result ->
                if (call.method == "read") result.success(deviceInfo()) else result.notImplemented()
            }
    }

    private fun deviceInfo(): Map<String, String> {
        val pkg = packageManager.getPackageInfo(packageName, 0)
        val build = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            pkg.longVersionCode
        } else {
            @Suppress("DEPRECATION")
            pkg.versionCode.toLong()
        }
        return mapOf(
            "appVersion" to "${pkg.versionName}+$build",
            "deviceModel" to "${Build.MANUFACTURER} ${Build.MODEL}",
            "osVersion" to "Android ${Build.VERSION.RELEASE}",
        )
    }
}
