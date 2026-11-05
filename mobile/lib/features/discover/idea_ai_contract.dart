import 'package:flutter_riverpod/flutter_riverpod.dart';

// Hợp đồng tạm với service AI của MB-55. Khi `lib/core/ai/` merge thì xóa file này, đổi import sang đó;
// `aiServiceProvider` lúc ấy trả service Gemini thật. Cho tới lúc đó nút AI vẫn ẩn vì cờ `ai_idea_enabled` mặc định tắt.

/// Ý tưởng AI đề xuất. [tags] là mã lĩnh vực.
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
  /// [description] là mô tả sinh viên gõ; [allowedTags] là mã lĩnh vực được phép dùng.
  Future<IdeaDraft> suggestIdea(String description, {required List<String> allowedTags});
}

class _UnavailableAiService implements AiService {
  const _UnavailableAiService();

  @override
  Future<IdeaDraft> suggestIdea(String description, {required List<String> allowedTags}) =>
      Future.error(const AiException('disabled'));
}

final aiServiceProvider = Provider<AiService>((ref) => const _UnavailableAiService());
