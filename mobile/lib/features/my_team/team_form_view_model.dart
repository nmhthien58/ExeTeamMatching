import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_exception.dart';
import '../suggestions/suggestions_api.dart' show CatalogRef;
import 'my_team_api.dart';
import '../../core/firebase/analytics_service.dart';
import 'dart:async';

/// Trạng thái form S13 / S16: tên đang gõ (kiểm tra lại khi gõ), domain đã chọn (đầu tiên là domain chính), lưu, lỗi.
class TeamFormState {
  const TeamFormState({this.name = '', this.domains = const [], this.saving = false, this.submitted = false, this.error});

  final String name;
  final List<CatalogRef> domains;
  final bool saving;
  final bool submitted;
  final ApiException? error;

  TeamFormState copyWith({String? name, List<CatalogRef>? domains, bool? saving, bool? submitted, ApiException? Function()? error}) => TeamFormState(
    name: name ?? this.name,
    domains: domains ?? this.domains,
    saving: saving ?? this.saving,
    submitted: submitted ?? this.submitted,
    error: error == null ? this.error : error(),
  );

  String? get nameError {
    if (error?.fieldErrors['name'] case [final first, ...]) return first;
    if (!submitted) return null;
    return name.trim().length < 2 ? 'Tên nhóm cần ít nhất 2 ký tự.' : null;
  }

  String? get domainError {
    if (error?.fieldErrors['domainIds'] case [final first, ...]) return first;
    return submitted && domains.isEmpty ? 'Chọn ít nhất một domain.' : null;
  }

  String? get projectNameError => error?.fieldErrors['projectName']?.first;

  /// Môn đã đủ số nhóm trong kỳ (BE-89): không thử lại được, View gợi ý sang Khám phá.
  bool get isTeamLimitReached => error?.code == 'team-limit-reached';

  /// Lỗi chung (không gắn ô nào) hiện bằng banner.
  String? get generalError => error != null && error!.fieldErrors.isEmpty ? error!.detail ?? error!.title : null;
}

/// Kết quả một lần của lệnh lưu: View hiện SnackBar rồi đóng form (tạo mới thì về S15).
sealed class TeamFormOutcome {
  const TeamFormOutcome();
}

final class TeamFormSaved extends TeamFormOutcome {
  const TeamFormSaved({required this.created});

  final bool created;

  String get message => created ? 'Đã tạo nhóm' : 'Đã lưu';
}

final class TeamFormNotSaved extends TeamFormOutcome {
  const TeamFormNotSaved();
}

/// ViewModel của S13 Tạo nhóm / S16 Sửa thông tin nhóm (MB-19, MB-21; MVVM MB-49). [team] null = tạo mới.
/// Ô nhập (TextEditingController) nằm ở View; tên được báo lại qua [nameChanged] để kiểm tra khi gõ.
class TeamFormViewModel extends Notifier<TeamFormState> {
  TeamFormViewModel(this.team);

  final MyTeam? team;

  bool get isEdit => team != null;

  @override
  TeamFormState build() => TeamFormState(name: team?.name ?? '', domains: [...?team?.domains]);

  void nameChanged(String name) => state = state.copyWith(name: name, error: () => null);

  void setDomains(List<CatalogRef> domains) => state = state.copyWith(domains: domains);

  void removeDomainAt(int index) => state = state.copyWith(domains: [...state.domains]..removeAt(index));

  Future<TeamFormOutcome> save({required String name, required String projectName, required String ideaDescription}) async {
    state = state.copyWith(name: name, submitted: true);
    if (state.nameError != null || state.domainError != null) return const TeamFormNotSaved();
    state = state.copyWith(saving: true, error: () => null);
    final domains = state.domains;
    final input = TeamInput(
      name: name.trim(),
      projectName: projectName.trim(),
      ideaDescription: ideaDescription.trim(),
      domainIds: [for (final d in domains) d.id],
      primaryDomainId: domains.first.id,
    );
    try {
      final api = ref.read(myTeamApiProvider);
      if (isEdit) {
        await api.update(team!.id, input);
      } else {
        final created = await api.create(input);
        unawaited(ref.read(analyticsServiceProvider).logCreateTeam(created.id, course: created.courseCode));
      }
      if (!ref.mounted) return const TeamFormNotSaved();
      refreshMyTeamFrom(ref);
      return TeamFormSaved(created: !isEdit);
    } on ApiException catch (e) {
      if (ref.mounted) state = state.copyWith(saving: false, error: () => e);
      return const TeamFormNotSaved();
    }
  }
}

final teamFormViewModelProvider = NotifierProvider.autoDispose.family<TeamFormViewModel, TeamFormState, MyTeam?>(TeamFormViewModel.new);
