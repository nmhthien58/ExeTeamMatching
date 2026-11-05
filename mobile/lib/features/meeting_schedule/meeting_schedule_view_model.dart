import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/api_client.dart';
import '../files/file_services.dart' show openUrlProvider;
import '../realtime/collab_hub.dart';
import '../workspace/workspace_api.dart' show myWorkspacesProvider;
import 'meeting_schedule_api.dart';

/// Câu lỗi C09: mã lỗi riêng của lịch họp trước, rồi detail / title của ProblemDetails.
String scheduleErrorText(Object e) => scheduleErrors[e.apiError.code] ?? e.apiError.detail ?? e.apiError.title;

/// Nghe `slotsChanged` của [workspaceIds] (payload chỉ có scheduleId) rồi gọi [reload]; tự bỏ nghe khi provider bị hủy.
void _listenSlots(Ref ref, Iterable<String> workspaceIds, void Function() reload) {
  final hub = ref.read(collabHubProvider);
  final unsubscribe = [for (final id in workspaceIds) hub.join(HubGroup.workspace, id), hub.on('slotsChanged', (_) => reload())];
  ref.onDispose(() {
    for (final u in unsubscribe) {
      u();
    }
  });
}

/// Trạng thái thao tác C09 phía nhóm: slot vừa chọn lỗi (báo tại ô) và câu lỗi.
class TeamScheduleState {
  const TeamScheduleState({this.failedSlot, this.error});

  final String? failedSlot;
  final String? error;
}

/// ViewModel của C09 phía nhóm (MB-46, MVVM MB-49), theo workspace: trưởng nhóm chọn / đổi slot; lưới tự tải lại khi
/// nhóm khác chọn slot (hub). Dữ liệu lịch lấy từ `teamScheduleProvider`.
class TeamScheduleViewModel extends Notifier<TeamScheduleState> {
  TeamScheduleViewModel(this.workspaceId);

  final String workspaceId;

  @override
  TeamScheduleState build() {
    _listenSlots(ref, [workspaceId], refresh);
    return const TeamScheduleState();
  }

  void refresh() => ref.invalidate(teamScheduleProvider(workspaceId));

  /// Mở link họp (Meet / Teams) bằng ứng dụng ngoài.
  Future<void> openLink(String url) async => ref.read(openUrlProvider)(url);

  /// Ai chọn trước được trước: slot đã bị nhóm khác lấy thì báo tại ô. Luôn tải lại lưới. Trả true khi chọn được.
  Future<bool> book(ScheduleSlot slot) async {
    var ok = false;
    try {
      await ref.read(meetingScheduleApiProvider).book(workspaceId, slot.id);
      if (ref.mounted) state = const TeamScheduleState();
      ok = true;
    } catch (e) {
      if (ref.mounted) state = TeamScheduleState(failedSlot: slot.id, error: scheduleErrorText(e));
    }
    if (ref.mounted) refresh();
    return ok;
  }
}

final teamScheduleViewModelProvider = NotifierProvider.autoDispose.family<TeamScheduleViewModel, TeamScheduleState, String>(TeamScheduleViewModel.new);

/// Giảng viên: nghe `slotsChanged` của mọi dự án EXE201 mình hướng dẫn rồi tải lại lịch. Màn C09 giảng viên watch provider này.
final lecturerScheduleSyncProvider = Provider.autoDispose<void>((ref) {
  final joined = <String>{};
  ref.listen(myWorkspacesProvider, (_, next) {
    final ids = (next.value ?? const []).where((w) => w.courseCode == 'EXE201').map((w) => w.id).where(joined.add).toList();
    if (ids.isNotEmpty) _listenSlots(ref, ids, () => ref.invalidate(myScheduleProvider));
  }, fireImmediately: true);
});

/// Trạng thái form khung giờ của giảng viên: khung giờ, độ dài slot và link họp đang gõ, đã bấm lưu, đang gửi, lỗi.
class LecturerScheduleState {
  const LecturerScheduleState({
    required this.windows,
    required this.minutesText,
    required this.location,
    this.submitted = false,
    this.busy = false,
    this.error,
  });

  final List<ScheduleWindow> windows;
  final String minutesText;
  final String location;
  final bool submitted;
  final bool busy;
  final String? error;

  int get slotMinutes => int.tryParse(minutesText) ?? 0;

  /// Khung đầu tiên có giờ kết thúc không sau giờ bắt đầu; -1 khi hợp lệ.
  int get badWindow => windows.indexWhere((w) => w.startTime.compareTo(w.endTime) >= 0);

  /// Lỗi khung giờ chỉ hiện sau lần bấm lưu.
  int get shownBadWindow => submitted ? badWindow : -1;

  String? get locationError => submitted && location.trim().isEmpty ? 'Nhập link họp.' : null;

  int get previewCount => countSlots(windows, slotMinutes);

  bool get valid => badWindow < 0 && location.trim().isNotEmpty && slotMinutes >= 5 && slotMinutes <= 60;

  LecturerScheduleState copyWith({
    List<ScheduleWindow>? windows,
    String? minutesText,
    String? location,
    bool? submitted,
    bool? busy,
    String? Function()? error,
  }) => LecturerScheduleState(
    windows: windows ?? this.windows,
    minutesText: minutesText ?? this.minutesText,
    location: location ?? this.location,
    submitted: submitted ?? this.submitted,
    busy: busy ?? this.busy,
    error: error == null ? this.error : error(),
  );
}

/// ViewModel của C09 phía giảng viên (MB-46, MVVM MB-49), theo id lịch (null = chưa tạo): sửa khung giờ, lưu (cắt slot),
/// đăng lịch, đổi slot cho nhóm. Lệnh trả câu SnackBar khi thành công, null khi không gửi / lỗi.
/// Form lấy giá trị ban đầu từ `myScheduleProvider` một lần; tải lại lịch (cùng id) không xóa chỗ đang sửa.
class LecturerScheduleViewModel extends Notifier<LecturerScheduleState> {
  LecturerScheduleViewModel(this.scheduleId);

  static const defaultWindow = ScheduleWindow(dayOfWeek: 2, startTime: '09:00', endTime: '11:00');

  final String? scheduleId;

  @override
  LecturerScheduleState build() {
    final schedule = ref.read(myScheduleProvider).value;
    final windows = [...?schedule?.windows];
    return LecturerScheduleState(
      windows: windows.isEmpty ? const [defaultWindow] : windows,
      minutesText: '${schedule?.slotMinutes ?? 15}',
      location: schedule?.location ?? '',
    );
  }

  void setWindow(int i, ScheduleWindow window) => state = state.copyWith(windows: [...state.windows]..[i] = window);

  void addWindow() => state = state.copyWith(windows: [...state.windows, defaultWindow]);

  void removeWindow(int i) => state = state.copyWith(windows: [...state.windows]..removeAt(i));

  void minutesChanged(String text) => state = state.copyWith(minutesText: text);

  void locationChanged(String text) => state = state.copyWith(location: text);

  Future<String?> save() async {
    state = state.copyWith(submitted: true);
    if (!state.valid) return null;
    final s = state;
    return _run((api) => api.save(s.windows, s.slotMinutes, s.location.trim()), 'Đã lưu khung giờ');
  }

  Future<String?> publish() => _run((api) => api.publish(), 'Đã đăng lịch cho các nhóm');

  Future<String?> move(ScheduleSlot slot, ScheduleSlot target) => _run((api) => api.move(slot.workspaceId!, target.id), 'Đã đổi slot cho ${slot.teamName}');

  Future<String?> assign(UnslottedTeam team, ScheduleSlot target) => _run((api) => api.move(team.workspaceId, target.id), 'Đã xếp slot cho ${team.teamName}');

  Future<String?> _run(Future<MeetingSchedule> Function(MeetingScheduleApi api) action, String done) async {
    state = state.copyWith(busy: true, error: () => null);
    try {
      await action(ref.read(meetingScheduleApiProvider));
      if (!ref.mounted) return done;
      state = state.copyWith(busy: false);
      ref.invalidate(myScheduleProvider);
      return done;
    } catch (e) {
      if (ref.mounted) state = state.copyWith(busy: false, error: () => scheduleErrorText(e));
      return null;
    }
  }
}

final lecturerScheduleViewModelProvider = NotifierProvider.autoDispose.family<LecturerScheduleViewModel, LecturerScheduleState, String?>(
  LecturerScheduleViewModel.new,
);
