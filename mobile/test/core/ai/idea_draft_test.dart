import 'package:exe_team_matching/core/ai/ai_exception.dart';
import 'package:exe_team_matching/core/ai/idea_draft.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _json({Object? tags = const ['EdTech']}) => {
  'title': ' Tìm trọ gần trường ',
  'problem': 'Sinh viên năm nhất khó tìm phòng trọ uy tín.',
  'solution': 'App liệt kê phòng trọ kèm đánh giá của người thuê cũ.',
  'targetCustomer': 'Sinh viên năm nhất và chủ nhà trọ quanh trường.',
  'tags': tags,
};

Matcher _aiError(String code) => throwsA(isA<AiException>().having((e) => e.code, 'code', code));

/// MB-55 – đọc JSON mô hình trả về.
void main() {
  test('parse đúng: cắt khoảng trắng, giữ 5 trường', () {
    final draft = IdeaDraft.fromJson(_json(), allowedTags: const ['EdTech', 'FinTech']);
    expect(draft.title, 'Tìm trọ gần trường');
    expect(draft.problem, startsWith('Sinh viên'));
    expect(draft.solution, startsWith('App'));
    expect(draft.targetCustomer, startsWith('Sinh viên năm nhất'));
    expect(draft.tags, ['EdTech']);
    expect(IdeaDraft.fromJson(draft.toJson()), draft);
  });

  test('thiếu trường hoặc chuỗi trống → bad_json', () {
    for (final key in ['title', 'problem', 'solution', 'targetCustomer', 'tags']) {
      expect(() => IdeaDraft.fromJson(_json()..remove(key)), _aiError(AiException.badJson), reason: key);
    }
    expect(() => IdeaDraft.fromJson(_json()..['title'] = '   '), _aiError(AiException.badJson));
  });

  test('sai kiểu → bad_json', () {
    expect(() => IdeaDraft.fromJson(_json()..['title'] = 42), _aiError(AiException.badJson));
    expect(() => IdeaDraft.fromJson(_json(tags: 'EdTech')), _aiError(AiException.badJson));
    expect(() => IdeaDraft.fromJson(_json(tags: ['EdTech', 1])), _aiError(AiException.badJson));
    expect(() => IdeaDraft.fromJson(['không phải object']), _aiError(AiException.badJson));
    expect(() => IdeaDraft.fromJson(null), _aiError(AiException.badJson));
  });

  test('lọc tag: chỉ giữ tag trong danh sách, không phân biệt hoa thường, bỏ trùng, tối đa 3', () {
    final draft = IdeaDraft.fromJson(
      _json(tags: ['edtech', 'Blockchain', 'EDTECH', 'FinTech', 'Du lịch', 'HealthTech']),
      allowedTags: const ['EdTech', 'FinTech', 'Du lịch', 'HealthTech'],
    );
    expect(draft.tags, ['EdTech', 'FinTech', 'Du lịch']);
    expect(() => (draft.tags as dynamic).add('x'), throwsUnsupportedError);

    expect(IdeaDraft.fromJson(_json(tags: ['Blockchain']), allowedTags: const ['EdTech']).tags, isEmpty);
    expect(IdeaDraft.fromJson(_json(tags: <String>[]), allowedTags: const ['EdTech']).tags, isEmpty);
  });

  test('mỗi chuỗi tối đa 300 ký tự', () {
    final draft = IdeaDraft.fromJson(_json()..['problem'] = 'a' * 500);
    expect(draft.problem, hasLength(IdeaDraft.maxTextLength));
  });
}
