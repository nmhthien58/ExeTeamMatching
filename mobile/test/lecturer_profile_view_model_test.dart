import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_screen.dart';
import 'package:exe_team_matching/features/lecturer_profile/lecturer_profile_view_model.dart';
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart' show CatalogRef;
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _profile = LecturerProfile(
  fullName: 'Hà',
  email: 'ha@example.edu',
  domains: [CatalogRef(id: 5, code: 'D05', name: 'Giáo dục')],
);

class _FakeApi extends LecturerProfileApi {
  _FakeApi() : super(Dio());

  Object? error;
  final saved = <List<int>>[];

  @override
  Future<LecturerProfile> me() async => _profile;

  @override
  Future<LecturerProfile> saveDomains(List<int> domainIds) async {
    if (error != null) throw error!;
    saved.add(domainIds);
    return _profile;
  }
}

/// MB-49 – ViewModel của L06 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [lecturerProfileApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
    c.listen(lecturerProfileViewModelProvider, (_, _) {});
  });

  LecturerProfileViewModel vm() => c.read(lecturerProfileViewModelProvider.notifier);
  LecturerProfileEditState state() => c.read(lecturerProfileViewModelProvider);

  test('sửa: bắt đầu từ domain hiện có, bật / tắt, hủy về chế độ xem', () {
    expect(state().editing, isNull);
    vm().startEditing(_profile);
    expect(state().editing, {5});
    vm()
      ..toggle(3)
      ..toggle(5);
    expect(state().editing, {3});
    vm().cancel();
    expect(state().editing, isNull);
  });

  test('lưu: gửi id đã sắp xếp, xong về chế độ xem', () async {
    vm()
      ..startEditing(_profile)
      ..toggle(2);
    expect(await vm().save(), isTrue);
    expect(api.saved.single, [2, 5]);
    expect((state().editing, state().saving), (null, false));
  });

  test('lỗi lưu: giữ lựa chọn, lỗi theo ô domainIds trước', () async {
    api.error = const ApiException(
      status: 400,
      code: 'v',
      title: 'Sai',
      fieldErrors: {
        'domainIds': ['Tối đa 18 domain'],
      },
    );
    vm().startEditing(_profile);
    expect(await vm().save(), isFalse);
    expect(state().editing, {5});
    expect((state().error, state().saving), ('Tối đa 18 domain', false));
  });
}
