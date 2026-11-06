import 'package:exe_team_matching/core/ai/ai_service.dart';
import 'package:exe_team_matching/features/discover/idea_suggest_view_model.dart';
import 'package:firebase_ai/firebase_ai.dart';
import 'package:flutter_test/flutter_test.dart';

/// Phân loại lỗi AI (MB-55): App Check từ chối không còn bị gộp vào "unknown" (ARTEMIS TC-19, 06/11/2026).
void main() {
  test('App Check từ chối (403) là denied, có câu tiếng Việt riêng', () {
    final code = aiErrorCode(FirebaseAIException('403 PERMISSION_DENIED: Firebase App Check token is invalid.'));
    expect(code, AiException.denied);
    expect(aiErrorMessage(code), contains('từ chối'));
  });

  test('quota vẫn là quota, lỗi lạ vẫn là unknown', () {
    expect(aiErrorCode(FirebaseAIException('429 RESOURCE_EXHAUSTED')), AiException.quota);
    expect(aiErrorCode(FirebaseAIException('something else')), AiException.unknown);
  });

  test('model quá tải (503) báo như quota, App Check bị giới hạn lượt thử là denied', () {
    expect(aiErrorCode(FirebaseAIException('503 UNAVAILABLE: This model is currently experiencing high demand.')), AiException.quota);
    expect(aiErrorCode(FirebaseAIException('firebase_app_check: Too many attempts.')), AiException.denied);
  });
}
