import 'package:flutter/foundation.dart';

import 'ai_exception.dart';

/// Bản nháp ý tưởng do AI viết từ mô tả ngắn của sinh viên (MB-55), để S09 điền sẵn (MB-56). Bất biến.
@immutable
class IdeaDraft {
  const IdeaDraft({
    required this.title,
    required this.problem,
    required this.solution,
    required this.targetCustomer,
    this.tags = const [],
  });

  /// Độ dài tối đa của mỗi chuỗi sau khi nhận từ mô hình.
  static const maxTextLength = 300;

  /// Số tag tối đa giữ lại.
  static const maxTags = 3;

  /// Tên trường trong JSON (cũng là tên trong `responseSchema`).
  static const fieldTitle = 'title';
  static const fieldProblem = 'problem';
  static const fieldSolution = 'solution';
  static const fieldTargetCustomer = 'targetCustomer';
  static const fieldTags = 'tags';

  final String title;
  final String problem;
  final String solution;
  final String targetCustomer;

  /// Tag lĩnh vực, chỉ gồm tag có trong danh sách được cho, tối đa [maxTags].
  final List<String> tags;

  /// Đọc JSON mô hình trả về. Thiếu trường hoặc sai kiểu → `AiException('bad_json')`.
  /// Chuỗi được cắt khoảng trắng và cắt còn [maxTextLength] ký tự. Khi có [allowedTags], chỉ giữ tag
  /// có trong danh sách (so không phân biệt hoa thường, trả về đúng cách viết trong danh sách), bỏ trùng,
  /// tối đa [maxTags].
  factory IdeaDraft.fromJson(Object? json, {Iterable<String>? allowedTags}) {
    if (json is! Map<String, dynamic>) throw const AiException(AiException.badJson);

    String text(String key) {
      final value = json[key];
      if (value is! String || value.trim().isEmpty) throw const AiException(AiException.badJson);
      return _cut(value.trim());
    }

    final rawTags = json[fieldTags];
    if (rawTags is! List || rawTags.any((t) => t is! String)) throw const AiException(AiException.badJson);

    return IdeaDraft(
      title: text(fieldTitle),
      problem: text(fieldProblem),
      solution: text(fieldSolution),
      targetCustomer: text(fieldTargetCustomer),
      tags: List.unmodifiable(_pickTags(rawTags.cast<String>(), allowedTags)),
    );
  }

  static String _cut(String value) => value.length > maxTextLength ? value.substring(0, maxTextLength) : value;

  static List<String> _pickTags(Iterable<String> raw, Iterable<String>? allowed) {
    final byKey = allowed == null ? null : {for (final t in allowed) t.trim().toLowerCase(): t};
    final result = <String>[];
    for (final tag in raw) {
      final trimmed = tag.trim();
      if (trimmed.isEmpty) continue;
      final picked = byKey == null ? _cut(trimmed) : byKey[trimmed.toLowerCase()];
      if (picked == null || result.contains(picked)) continue;
      result.add(picked);
      if (result.length == maxTags) break;
    }
    return result;
  }

  Map<String, Object> toJson() => {
    fieldTitle: title,
    fieldProblem: problem,
    fieldSolution: solution,
    fieldTargetCustomer: targetCustomer,
    fieldTags: tags,
  };

  @override
  bool operator ==(Object other) =>
      other is IdeaDraft &&
      other.title == title &&
      other.problem == problem &&
      other.solution == solution &&
      other.targetCustomer == targetCustomer &&
      listEquals(other.tags, tags);

  @override
  int get hashCode => Object.hash(title, problem, solution, targetCustomer, Object.hashAll(tags));

  @override
  String toString() => 'IdeaDraft(title: $title, tags: $tags)';
}
