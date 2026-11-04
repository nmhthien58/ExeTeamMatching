import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/lecturers/lecturer_preferences_view_model.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_api.dart';
import 'package:exe_team_matching/features/lecturers/lecturers_models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeApi extends LecturersApi {
  _FakeApi() : super(Dio());

  Object? error;
  int loads = 0;
  final saved = <List<String>>[];

  @override
  Future<LecturerPreferenceListResponse> getPreferences(String teamId) async {
    loads++;
    return const LecturerPreferenceListResponse();
  }

  @override
  Future<LecturerPreferenceListResponse> updatePreferences(String teamId, List<String> lecturerIds) async {
    if (error != null) throw error!;
    saved.add(lecturerIds);
    return const LecturerPreferenceListResponse();
  }
}

/// MB-49 – ViewModel nguyện vọng giảng viên (S23–S25) và từ khóa tìm S24, test riêng không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [lecturersApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
  });

  LecturerPreferencesViewModel vm() => c.read(lecturerPreferencesViewModelProvider('t1').notifier);

  test('lưu thứ tự mới rồi tải lại nguyện vọng; hoàn tác lưu lại danh sách cũ', () async {
    c.listen(lecturerPreferencesProvider('t1'), (_, _) {});
    await c.read(lecturerPreferencesProvider('t1').future);
    expect(await vm().update(['b', 'a']), isA<PreferenceUpdated>());
    await c.read(lecturerPreferencesProvider('t1').future);
    expect(api.loads, 2);
    await vm().undo(['a', 'b']);
    expect(api.saved, [
      ['b', 'a'],
      ['a', 'b'],
    ]);
  });

  test('lỗi: câu lỗi từ Exception, lỗi khác dùng câu mặc định', () async {
    api.error = const ApiException(status: 409, code: 'locked', title: 'Đã khóa');
    final failed = await vm().update(['a']) as PreferenceUpdateFailed;
    expect(failed.messageOr('x'), isNot('x'));
    api.error = 'lạ';
    expect((await vm().update(['a']) as PreferenceUpdateFailed).messageOr('Không thể cập nhật nguyện vọng'), 'Không thể cập nhật nguyện vọng');
  });

  test('hoàn tác lỗi thì bỏ qua', () async {
    api.error = Exception('mất mạng');
    await vm().undo(['a']);
    expect(api.saved, isEmpty);
  });

  test('từ khóa tìm: trim, xóa', () {
    c.listen(lecturerSearchViewModelProvider, (_, _) {});
    final search = c.read(lecturerSearchViewModelProvider.notifier);
    search.search('  Hà ');
    expect(c.read(lecturerSearchViewModelProvider), 'Hà');
    search.clear();
    expect(c.read(lecturerSearchViewModelProvider), '');
  });
}
