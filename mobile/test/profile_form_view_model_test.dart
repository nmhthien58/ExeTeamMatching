import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/features/profile/profile_api.dart';
import 'package:exe_team_matching/features/profile/profile_form_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _profile = StudentProfile(
  id: 's1',
  studentCode: 'SE1',
  fullName: 'An',
  specialization: 'SE',
  specializationId: 1,
  majorCode: 'SE',
  majorName: 'Kỹ thuật phần mềm',
  skills: {'TECHNOLOGY': 3},
  description: 'cũ',
);

class _FakeApi extends ProfileApi {
  _FakeApi() : super(Dio());

  Object? error;
  final saved = <(Map<String, int>, String, bool?)>[];

  @override
  Future<StudentProfile> me() async => _profile;

  @override
  Future<StudentProfile> saveMe({
    required Map<String, int> skills,
    required String description,
    int? specializationId,
    bool? isSeekingTeam,
  }) async {
    if (error != null) throw error!;
    saved.add((skills, description, isSeekingTeam));
    return _profile;
  }
}

/// MB-49 – ViewModel form hồ sơ A05 / S03 test riêng, không dựng widget.
void main() {
  late _FakeApi api;
  late ProviderContainer c;

  setUp(() {
    api = _FakeApi();
    c = ProviderContainer(overrides: [profileApiProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
    c.listen(profileFormViewModelProvider(_profile), (_, _) {});
  });

  ProfileFormState state() => c.read(profileFormViewModelProvider(_profile));
  ProfileFormViewModel vm() => c.read(profileFormViewModelProvider(_profile).notifier);

  test('khởi tạo từ hồ sơ, đếm kỹ năng còn thiếu', () {
    expect(state().levels, {'TECHNOLOGY': 3});
    expect(state().missing, skillLabels.length - 1);
    for (final skill in skillLabels.keys) {
      vm().setLevel(skill, 4);
    }
    expect(state().missing, 0);
  });

  test('lưu: gửi kỹ năng, giới thiệu đã trim; chỉ gửi isSeekingTeam khi có công tắc', () async {
    vm().setSeeking(true);
    expect(await vm().save(description: ' Xin chào ', withSeeking: false), isTrue);
    expect(await vm().save(description: '', withSeeking: true), isTrue);
    expect(api.saved.map((s) => (s.$2, s.$3)), [('Xin chào', null), ('', true)]);
    expect(state().saving, isFalse);
  });

  test('lỗi: giữ dữ liệu, có câu lỗi', () async {
    api.error = const ApiException(status: 400, code: 'x', title: 'Sai', detail: 'Thiếu kỹ năng');
    vm().setLevel('TECHNOLOGY', 5);
    expect(await vm().save(description: '', withSeeking: false), isFalse);
    expect((state().error, state().saving, state().levels['TECHNOLOGY']), ('Thiếu kỹ năng', false, 5));
  });
}
