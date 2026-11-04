import 'package:exe_team_matching/core/phase.dart';
import 'package:exe_team_matching/shared/widgets/brand_header_view_model.dart';
import 'package:flutter_test/flutter_test.dart';

/// Header trang chủ: tuần học, tiến độ học kỳ, chip giai đoạn – hàm thuần, không dựng widget.
void main() {
  final semester = CurrentSemester(code: 'SP90', startDate: DateTime(2026, 10, 26), endDate: DateTime(2027, 2, 7));
  final now = DateTime(2026, 11, 5, 9, 30); // Thứ Năm

  test('đang lập nhóm: chip đếm ngày tới hạn, dòng ngày có tuần học', () {
    final data = BrandHeaderData.compute(
      now: now,
      fullName: 'Nguyễn Mai Hào Thiên',
      courseCode: 'EXE101',
      semester: semester,
      phase: CoursePhase(courseCode: 'EXE101', isTeamFormation: true, teamFormationDeadlineAt: DateTime(2026, 11, 25, 14, 15)),
    );

    expect(data.greeting, 'Chào Thiên');
    expect(data.context, 'EXE101 · Học kỳ SP90');
    expect(data.dateLine, 'Thứ Năm, 05/11/2026 · Tuần 2/15');
    expect(data.chip, 'Còn 20 ngày');
    expect(data.semesterProgress, closeTo(10 / 105, 0.001));
    expect(data.progressLabel, 'Học kỳ đã qua 10%');
  });

  test('hạn là hôm nay: chip "Hạn hôm nay"; hết lập nhóm: "Đang làm dự án"', () {
    final dueToday = BrandHeaderData.compute(
      now: now,
      phase: CoursePhase(courseCode: 'EXE101', isTeamFormation: true, teamFormationDeadlineAt: DateTime(2026, 11, 5, 23)),
    );
    final running = BrandHeaderData.compute(now: now, phase: const CoursePhase(courseCode: 'EXE201', isTeamFormation: false));

    expect(dueToday.chip, 'Hạn hôm nay');
    expect(running.chip, 'Đang làm dự án');
  });

  test('chưa tải được gì: chỉ còn lời chào và ngày, không chip, không vạch', () {
    final data = BrandHeaderData.compute(now: now);

    expect(data.greeting, 'Xin chào');
    expect(data.dateLine, 'Thứ Năm, 05/11/2026');
    expect(data.context, isNull);
    expect(data.chip, isNull);
    expect(data.semesterProgress, isNull);
  });

  test('ngoài thời gian học kỳ: không ghi tuần, vạch kẹp trong 0–1', () {
    final before = BrandHeaderData.compute(now: DateTime(2026, 10, 1), semester: semester);
    final after = BrandHeaderData.compute(now: DateTime(2027, 3, 1), semester: semester);

    expect(before.dateLine, isNot(contains('Tuần')));
    expect(before.semesterProgress, 0);
    expect(after.semesterProgress, 1);
  });
}
