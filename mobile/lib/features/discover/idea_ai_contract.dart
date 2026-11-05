import 'package:flutter_riverpod/flutter_riverpod.dart';

// Bản tạm cùng interface với `lib/core/ai/` của MB-55 (#169). Khi #169 merge thì xóa file này, đổi import sang
// `core/ai/ai_service.dart`; `aiServiceProvider` lúc ấy trả service Gemini thật.

/// Ý tưởng AI đề xuất. [tags] lấy trong `allowedTags`.
class IdeaDraft {
  const IdeaDraft({
    required this.title,
    required this.problem,
    required this.solution,
    required this.targetCustomer,
    this.tags = const [],
  });

  final String title;
  final String problem;
  final String solution;
  final String targetCustomer;
  final List<String> tags;
}

/// Lỗi của service AI. [code]: `disabled`, `quota`, `network`, `bad_json`, `blocked`, `unknown`.
class AiException implements Exception {
  const AiException(this.code);

  final String code;

  @override
  String toString() => 'AiException($code)';
}

abstract interface class AiService {
  Future<IdeaDraft> suggestIdea({required String description, required List<String> allowedTags});
}

class _UnavailableAiService implements AiService {
  const _UnavailableAiService();

  @override
  Future<IdeaDraft> suggestIdea({required String description, required List<String> allowedTags}) =>
      Future.error(const AiException('disabled'));
}

final aiServiceProvider = Provider<AiService>((ref) => const _UnavailableAiService());
