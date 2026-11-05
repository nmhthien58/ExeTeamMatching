import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/firebase/analytics_service.dart';
import '../../core/firebase/remote_config_service.dart';
import '../my_team/my_team_api.dart' show domainsProvider;
import '../suggestions/suggestions_api.dart' show CatalogRef;
import 'idea_ai_contract.dart';

/// Tên tính năng trong sự kiện Analytics.
const ideaSuggestFeature = 'idea_suggest';

const _minDescriptionLength = 10;

/// Giới hạn ý tưởng theo quy tắc 17.
const _maxTitleLength = 80;
const _maxDescriptionLength = 300;

sealed class IdeaSuggestState {
  const IdeaSuggestState();
}

final class IdeaSuggestIdle extends IdeaSuggestState {
  const IdeaSuggestIdle({this.inputError});

  final String? inputError;
}

final class IdeaSuggestLoading extends IdeaSuggestState {
  const IdeaSuggestLoading();
}

final class IdeaSuggestReady extends IdeaSuggestState {
  const IdeaSuggestReady(this.draft);

  final IdeaDraft draft;
}

final class IdeaSuggestFailed extends IdeaSuggestState {
  const IdeaSuggestFailed(this.message);

  final String message;
}

/// Câu lỗi cho sinh viên theo `AiException.code`.
String aiErrorMessage(String code) => switch (code) {
  'disabled' => 'Gợi ý bằng AI đang tắt. Bạn vẫn có thể tự nhập ý tưởng.',
  'quota' => 'AI đang quá tải. Thử lại sau ít phút, hoặc tự nhập ý tưởng.',
  'network' => 'Mất kết nối. Kiểm tra mạng rồi thử lại.',
  'bad_json' => 'AI trả kết quả không đọc được. Bấm thử lại nhé.',
  'blocked' => 'AI không xử lý được mô tả này. Hãy viết lại mô tả khác.',
  _ => 'Không tạo được gợi ý. Bạn vẫn có thể tự nhập ý tưởng.',
};

/// Bỏ email và MSSV khỏi mô tả trước khi gửi lên AI (không gửi dữ liệu định danh).
String stripPersonalData(String text) => text
    .replaceAll(RegExp(r'[\w.+-]+@[\w-]+\.[\w.-]+'), '')
    .replaceAll(RegExp(r'\b[A-Za-z]{2}\d{6}\b'), '')
    .replaceAll(RegExp(r'[ \t]{2,}'), ' ')
    .trim();

/// Ghép bản nháp AI thành (tiêu đề, mô tả) của ý tưởng ở S09. Chỉ giữ tag có trong danh mục lĩnh vực; cắt theo quy tắc 17.
(String, String) ideaFieldsFrom(IdeaDraft draft, List<CatalogRef> domains) {
  final names = {for (final d in domains) d.name};
  final tags = draft.tags.where(names.contains).toList();
  final description = [
    'Vấn đề: ${draft.problem}',
    'Giải pháp: ${draft.solution}',
    'Khách hàng: ${draft.targetCustomer}',
    if (tags.isNotEmpty) 'Lĩnh vực: ${tags.join(', ')}',
  ].join('\n');
  return (_cut(draft.title, _maxTitleLength), _cut(description, _maxDescriptionLength));
}

String _cut(String text, int max) => text.length > max ? text.substring(0, max).trim() : text;

/// ViewModel của phần "Gợi ý bằng AI" ở S09 (MB-56).
class IdeaSuggestViewModel extends Notifier<IdeaSuggestState> {
  @override
  IdeaSuggestState build() => const IdeaSuggestIdle();

  /// Cờ Remote Config: tắt thì không hiện nút.
  bool get enabled => ref.read(remoteConfigProvider).aiIdeaEnabled;

  Future<void> suggest(String description) async {
    final input = stripPersonalData(description);
    if (input.length < _minDescriptionLength) {
      state = const IdeaSuggestIdle(inputError: 'Mô tả thêm vài câu về ý tưởng của bạn.');
      return;
    }
    if (!enabled) {
      state = IdeaSuggestFailed(aiErrorMessage('disabled'));
      return;
    }
    state = const IdeaSuggestLoading();
    try {
      final domains = await ref.read(domainsProvider.future);
      final draft = await ref.read(aiServiceProvider).suggestIdea(description: input, allowedTags: [for (final d in domains) d.name]);
      if (ref.mounted) state = IdeaSuggestReady(draft);
    } on AiException catch (e) {
      if (ref.mounted) state = IdeaSuggestFailed(aiErrorMessage(e.code));
    } catch (_) {
      if (ref.mounted) state = IdeaSuggestFailed(aiErrorMessage('unknown'));
    }
  }

  /// Sinh viên bấm dùng gợi ý: trả (tiêu đề, mô tả) để điền vào form. Null nếu chưa có gợi ý.
  Future<(String, String)?> accept() async {
    final current = state;
    if (current is! IdeaSuggestReady) return null;
    final domains = await ref.read(domainsProvider.future);
    unawaited(ref.read(analyticsServiceProvider).logAiResultAccepted(feature: ideaSuggestFeature, index: 0));
    return ideaFieldsFrom(current.draft, domains);
  }

  void reset() => state = const IdeaSuggestIdle();
}

final ideaSuggestViewModelProvider = NotifierProvider.autoDispose<IdeaSuggestViewModel, IdeaSuggestState>(IdeaSuggestViewModel.new);
