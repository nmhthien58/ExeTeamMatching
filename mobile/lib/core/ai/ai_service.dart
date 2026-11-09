import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:firebase_ai/firebase_ai.dart';
import 'package:firebase_app_check/firebase_app_check.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../firebase/analytics_service.dart';
import '../firebase/crash_reporter.dart';
import '../firebase/remote_config_service.dart';
import 'ai_exception.dart';
import 'idea_draft.dart';

export 'ai_exception.dart';
export 'idea_draft.dart';

/// Tên tính năng trong tham số `feature` của `ai_request`, `ai_error`, `ai_result_accepted`.
const aiFeatureIdeaSuggest = 'idea_suggest';

/// Service AI dùng chung (MB-55). ViewModel chỉ dùng interface này, không gọi `FirebaseAI` trực tiếp.
/// Lỗi luôn là [AiException] (xem [AiException.codes]); luồng nhập tay vẫn chạy khi AI lỗi.
abstract interface class AiService {
  /// Viết bản nháp ý tưởng từ [description] sinh viên gõ. Tag chỉ lấy trong [allowedTags], tối đa 3.
  /// Chỉ gửi [description] và [allowedTags] lên mô hình – không gửi tên, email, MSSV hay ảnh.
  Future<IdeaDraft> suggestIdea({required String description, required List<String> allowedTags});
}

/// Phần gọi mô hình, tách riêng để test thay bằng bản giả. Trả text JSON thô (`null` khi không có text);
/// lỗi SDK / mạng được ném nguyên, [FirebaseAiService] đổi sang [AiException].
abstract interface class AiModelClient {
  Future<String?> generateJson({
    required String model,
    required String systemInstruction,
    required String prompt,
    required Schema responseSchema,
  });
}

/// Gọi Gemini Developer API qua Firebase AI Logic. Không có API key trong app: request đi qua proxy của
/// Firebase, kèm token App Check (`FirebaseAI.googleAI` tự lấy instance App Check đã đăng ký với app).
class FirebaseAiModelClient implements AiModelClient {
  @override
  Future<String?> generateJson({
    required String model,
    required String systemInstruction,
    required String prompt,
    required Schema responseSchema,
  }) async {
    // firebase_ai 5.x bỏ tham số `appCheck` của googleAI() và đọc App Check từ registry của app.
    // Đi qua `FirebaseAppCheck.instance` để chắc chắn App Check (đã activate ở bootstrap) được đăng ký.
    final generative = FirebaseAI.googleAI(app: FirebaseAppCheck.instance.app).generativeModel(
      model: model,
      systemInstruction: Content.system(systemInstruction),
      generationConfig: GenerationConfig(responseMimeType: 'application/json', responseSchema: responseSchema),
    );
    final response = await generative.generateContent([Content.text(prompt)]);
    return response.text;
  }
}

/// Schema JSON mô hình phải trả: 5 trường bắt buộc, `tags` là mảng chuỗi (chọn trong [allowedTags] nếu có).
Schema ideaDraftSchema(List<String> allowedTags) => Schema.object(
  properties: {
    IdeaDraft.fieldTitle: Schema.string(description: 'Tên ý tưởng, tối đa 60 ký tự'),
    IdeaDraft.fieldProblem: Schema.string(description: 'Vấn đề, 1–3 câu'),
    IdeaDraft.fieldSolution: Schema.string(description: 'Giải pháp, 1–3 câu'),
    IdeaDraft.fieldTargetCustomer: Schema.string(description: 'Khách hàng mục tiêu, 1–3 câu'),
    IdeaDraft.fieldTags: Schema.array(
      items: allowedTags.isEmpty ? Schema.string() : Schema.enumString(enumValues: allowedTags),
      description: 'Tối đa ${IdeaDraft.maxTags} tag trong danh sách được cho',
      maxItems: IdeaDraft.maxTags,
    ),
  },
  propertyOrdering: const [
    IdeaDraft.fieldTitle,
    IdeaDraft.fieldProblem,
    IdeaDraft.fieldSolution,
    IdeaDraft.fieldTargetCustomer,
    IdeaDraft.fieldTags,
  ],
);

class FirebaseAiService implements AiService {
  FirebaseAiService({
    required this._client,
    required this._config,
    required this._analytics,
    required this._crash,
    this.timeout = defaultTimeout,
    this.retryDelay = defaultRetryDelay,
  });

  final AiModelClient _client;
  final AppRemoteConfig _config;
  final AnalyticsService _analytics;
  final CrashReporter _crash;
  final Duration timeout;
  final Duration retryDelay;

  static const defaultTimeout = Duration(seconds: 30);
  static const defaultRetryDelay = Duration(milliseconds: 1500);

  /// Model dự phòng khi model chính hết quota / quá tải / không tồn tại (free tier riêng, rộng hơn).
  static const fallbackModel = 'gemini-2.5-flash-lite';

  /// Mô tả dài hơn thì cắt bớt trước khi gửi (giới hạn chi phí token).
  static const maxDescriptionLength = 1000;

  @override
  Future<IdeaDraft> suggestIdea({required String description, required List<String> allowedTags}) async {
    if (!_config.aiIdeaEnabled) throw const AiException(AiException.disabled);
    final model = _config.aiModel;
    await _analytics.logAiRequest(feature: aiFeatureIdeaSuggest, model: model);
    final prompt = buildIdeaPrompt(description, allowedTags);
    final schema = ideaDraftSchema(allowedTags);
    // Thử tối đa 2 lần: lỗi mạng → thử lại cùng model; hết quota / quá tải / model không có → model dự phòng.
    Object? lastError;
    StackTrace? lastStack;
    var current = model;
    for (var attempt = 1; attempt <= 2; attempt++) {
      try {
        final text = await _client
            .generateJson(model: current, systemInstruction: _config.aiPrompt, prompt: prompt, responseSchema: schema)
            .timeout(timeout);
        if (text == null || text.trim().isEmpty) throw const AiException(AiException.blocked);
        return IdeaDraft.fromJson(jsonDecode(text), allowedTags: allowedTags);
      } catch (e, s) {
        lastError = e;
        lastStack = s;
        final code = aiErrorCode(e);
        debugPrint('[AI] $aiFeatureIdeaSuggest model=$current attempt=$attempt code=$code error=$e');
        final modelProblem = code == AiException.quota || isModelNotFound(e);
        if (attempt == 2 || !(modelProblem || code == AiException.network)) break;
        if (modelProblem && current != fallbackModel) current = fallbackModel;
        await Future<void>.delayed(retryDelay);
      }
    }
    final code = aiErrorCode(lastError!);
    await _analytics.logAiError(feature: aiFeatureIdeaSuggest, errorCode: code);
    // Ghi mọi lỗi lên Crashlytics (non-fatal) kèm mã và model để chẩn đoán trên APK release.
    await _crash.recordNonFatal(lastError, lastStack, reason: 'ai_${aiFeatureIdeaSuggest}_${code}_$current');
    throw AiException(code);
  }

  /// Model không tồn tại / không hỗ trợ (HTTP 404) – ví dụ `ai_model` trên Console gõ sai tên.
  static bool isModelNotFound(Object error) {
    if (error is! FirebaseAIException) return false;
    final lower = error.toString().toLowerCase();
    return lower.contains('404') || lower.contains('not_found') || lower.contains('is not found') || lower.contains('not supported');
  }

  /// Nội dung gửi lên mô hình: chỉ mô tả sinh viên gõ và danh sách tag.
  static String buildIdeaPrompt(String description, List<String> allowedTags) {
    final trimmed = description.trim();
    final cut = trimmed.length > maxDescriptionLength ? trimmed.substring(0, maxDescriptionLength) : trimmed;
    final tags = allowedTags.isEmpty ? '(không có)' : allowedTags.join(', ');
    return 'Mô tả của sinh viên:\n"""\n$cut\n"""\n\nDanh sách tag được chọn: $tags';
  }
}

/// Đổi lỗi SDK / mạng / parse sang mã [AiException].
String aiErrorCode(Object error) {
  if (error is AiException) return error.code;
  if (error is TimeoutException || error is SocketException) return AiException.network;
  if (error is FormatException) return AiException.badJson;
  if (error is QuotaExceeded) return AiException.quota;
  final text = error.toString();
  if (error is FirebaseAIException) {
    final lower = text.toLowerCase();
    // 503 / "high demand": model đang quá tải phía Google, báo như quota (thử lại sau).
    if (lower.contains('429') || lower.contains('resource_exhausted') || lower.contains('quota') ||
        lower.contains('503') || lower.contains('unavailable') || lower.contains('high demand')) {
      return AiException.quota;
    }
    // `response.text` ném "Response was blocked…" / "Candidate was blocked due to safety…".
    if (lower.contains('blocked')) return AiException.blocked;
    if (lower.contains('app check') || lower.contains('appcheck') || lower.contains('app_check') || lower.contains('permission_denied') || lower.contains('403')) {
      return AiException.denied;
    }
    return AiException.unknown;
  }
  // package:http bọc lỗi socket trong ClientException ("ClientException with SocketException: …").
  if (text.startsWith('ClientException') || text.contains('SocketException') || error is HandshakeException) {
    return AiException.network;
  }
  return AiException.unknown;
}

final aiModelClientProvider = Provider<AiModelClient>((ref) => FirebaseAiModelClient());

/// Model, prompt và cờ bật/tắt lấy từ Remote Config (`ai_model`, `ai_prompt`, `ai_idea_enabled`);
/// Console publish giá trị mới thì service được tạo lại. Test ghi đè [aiModelClientProvider] bằng bản giả,
/// hoặc ghi đè thẳng provider này.
final aiServiceProvider = Provider<AiService>(
  (ref) => FirebaseAiService(
    client: ref.watch(aiModelClientProvider),
    config: ref.watch(remoteConfigProvider),
    analytics: ref.watch(analyticsServiceProvider),
    crash: ref.watch(crashReporterProvider),
  ),
);
