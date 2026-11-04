import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'meetings_api.dart';

const _pastSlot = 'Khung giờ phải ở tương lai.';

/// Kết quả một lần của thao tác trên cuộc họp: [MeetingActionDone.message] là câu SnackBar (null thì không hiện).
sealed class MeetingActionOutcome {
  const MeetingActionOutcome();

  bool get ok => this is MeetingActionDone;
}

final class MeetingActionDone extends MeetingActionOutcome {
  const MeetingActionDone([this.message]);

  final String? message;
}

final class MeetingActionFailed extends MeetingActionOutcome {
  const MeetingActionFailed();
}

/// Trạng thái thao tác của C08: đang gửi (khóa nút) và câu lỗi (banner). Dữ liệu cuộc họp lấy từ `meetingDetailProvider`.
class MeetingActionState {
  const MeetingActionState({this.busy = false, this.error});

  final bool busy;
  final String? error;
}

/// ViewModel của C08 – Chi tiết cuộc họp (MB-42, MVVM MB-49), theo id cuộc họp: trả lời khung, chốt, thêm khung, hủy,
/// lưu biên bản, tạo việc từ biên bản. Mỗi thao tác xong thì tải lại chi tiết.
class MeetingDetailViewModel extends Notifier<MeetingActionState> {
  MeetingDetailViewModel(this.meetingId);

  final String meetingId;

  @override
  MeetingActionState build() => const MeetingActionState();

  MeetingsApi get _api => ref.read(meetingsApiProvider);

  void refresh() => ref.invalidate(meetingDetailProvider(meetingId));

  Future<MeetingActionOutcome> answer(String slotId, String answer) => _run(() => _api.answer(meetingId, slotId, answer));

  Future<MeetingActionOutcome> confirm(String slotId) => _run(() => _api.confirm(meetingId, slotId), done: 'Đã chốt khung giờ');

  Future<MeetingActionOutcome> addSlot(DateTime at, {DateTime? now}) async {
    if (!at.isAfter(now ?? DateTime.now())) {
      state = const MeetingActionState(error: _pastSlot);
      return const MeetingActionFailed();
    }
    return _run(() => _api.addSlot(meetingId, SlotInput(at)), done: 'Đã thêm khung giờ');
  }

  Future<MeetingActionOutcome> cancel(String reason) => _run(() => _api.cancel(meetingId, reason), done: 'Đã hủy cuộc họp');

  Future<MeetingActionOutcome> saveMinutes(String text) => _run(() => _api.saveMinutes(meetingId, text), done: 'Đã lưu biên bản');

  Future<MeetingActionOutcome> createTasks(List<String> titles) => _run(() => _api.minutesToTasks(meetingId, titles), done: 'Đã tạo ${titles.length} việc');

  /// Lỗi hiện banner, dữ liệu đang nhập ở View giữ nguyên.
  Future<MeetingActionOutcome> _run(Future<Object?> Function() action, {String? done}) async {
    state = const MeetingActionState(busy: true);
    try {
      await action();
      if (!ref.mounted) return const MeetingActionFailed();
      refresh();
      state = const MeetingActionState();
      return MeetingActionDone(done);
    } catch (e) {
      if (ref.mounted) state = MeetingActionState(error: errorText(e));
      return const MeetingActionFailed();
    }
  }
}

final meetingDetailViewModelProvider = NotifierProvider.autoDispose.family<MeetingDetailViewModel, MeetingActionState, String>(MeetingDetailViewModel.new);

/// Trạng thái form đề xuất họp: các khung giờ đã thêm, lỗi theo ô, lỗi gửi, đang lưu.
class ProposeMeetingState {
  const ProposeMeetingState({this.slots = const [], this.titleError, this.slotError, this.error, this.saving = false});

  final List<DateTime> slots;
  final String? titleError;
  final String? slotError;
  final String? error;
  final bool saving;

  bool get canAddSlot => slots.length < maxSlots;

  /// Một khung giờ thì tạo luôn cuộc họp đã chốt.
  String get submitLabel => slots.length > 1 ? 'Gửi đề xuất' : 'Tạo cuộc họp';

  ProposeMeetingState copyWith({
    List<DateTime>? slots,
    String? Function()? titleError,
    String? Function()? slotError,
    String? Function()? error,
    bool? saving,
  }) => ProposeMeetingState(
    slots: slots ?? this.slots,
    titleError: titleError == null ? this.titleError : titleError(),
    slotError: slotError == null ? this.slotError : slotError(),
    error: error == null ? this.error : error(),
    saving: saving ?? this.saving,
  );
}

/// ViewModel của sheet đề xuất họp (C07 → C08, MB-42), theo workspace: 1–3 khung giờ ở tương lai, tiêu đề bắt buộc.
class ProposeMeetingViewModel extends Notifier<ProposeMeetingState> {
  ProposeMeetingViewModel(this.workspaceId);

  final String workspaceId;

  @override
  ProposeMeetingState build() => const ProposeMeetingState();

  void addSlot(DateTime at, {DateTime? now}) {
    final past = !at.isAfter(now ?? DateTime.now());
    state = state.copyWith(slotError: () => past ? _pastSlot : null, slots: past ? null : [...state.slots, at]);
  }

  void removeSlot(int index) => state = state.copyWith(slots: [...state.slots]..removeAt(index));

  /// Trả id cuộc họp mới; null khi thiếu dữ liệu hoặc lỗi (lỗi nằm trong state).
  Future<String?> submit({required String title, required String location, required String agenda}) async {
    state = state.copyWith(
      titleError: () => title.trim().isEmpty ? 'Nhập tiêu đề.' : null,
      slotError: () => state.slots.isEmpty ? 'Thêm ít nhất một khung giờ.' : null,
      error: () => null,
    );
    if (state.titleError != null || state.slotError != null) return null;
    state = state.copyWith(saving: true);
    try {
      final meeting = await ref
          .read(meetingsApiProvider)
          .propose(
            workspaceId,
            title: title.trim(),
            location: location.trim().isEmpty ? null : location.trim(),
            agenda: agenda.trim().isEmpty ? null : agenda.trim(),
            slots: [for (final s in state.slots) SlotInput(s)],
          );
      ref.invalidate(meetingListProvider(workspaceId));
      if (ref.mounted) state = state.copyWith(saving: false);
      return meeting.card.id;
    } catch (e) {
      if (ref.mounted) state = state.copyWith(saving: false, error: () => errorText(e));
      return null;
    }
  }
}

final proposeMeetingViewModelProvider = NotifierProvider.autoDispose.family<ProposeMeetingViewModel, ProposeMeetingState, String>(ProposeMeetingViewModel.new);
