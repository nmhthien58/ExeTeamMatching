import 'package:exe_team_matching/app/navigation.dart';
import 'package:exe_team_matching/shared/widgets/profile_avatar_button.dart';
import 'package:flutter_test/flutter_test.dart';

/// MB-62: tab Tin nhắn thay tab Hồ sơ của sinh viên; hồ sơ mở từ avatar.
void main() {
  for (final formation in [true, false]) {
    test('sinh viên có tab Tin nhắn, không còn tab Hồ sơ (đợt lập nhóm: $formation)', () {
      final paths = roleNav(Shell.student, isTeamFormation: formation, isMentoringWindow: false).map((i) => i.path).toList();
      expect(paths, hasLength(5));
      expect(paths, contains('/sv/tin-nhan'));
      expect(paths, isNot(contains('/sv/ho-so')));
    });
  }

  test('avatar mở đúng màn hồ sơ theo khung', () {
    expect(shellProfile[Shell.student], '/sv/ho-so');
    expect(shellProfile[Shell.lecturer], '/gv/ho-so');
    expect(shellProfile[Shell.mentor], '/mentor/cai-dat');
  });

  test('chữ cái avatar là chữ đầu của tên gọi', () {
    expect(initialsOf('Nguyễn Minh Thiên'), 'T');
    expect(initialsOf('  '), '?');
  });
}
