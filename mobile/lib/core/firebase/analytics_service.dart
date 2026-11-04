import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Nơi mở màn chi tiết nhóm (tham số `source` của `view_team`).
enum TeamViewSource { discover, suggestions, invitation, myTeam, lecturer }

/// Nơi gửi lời mời (tham số `source` của `send_invitation`).
enum InviteSource { suggestions, search }

/// Tên sự kiện – khớp `docs/analytics-plan.md`. Ưu tiên tên Firebase khuyến nghị (`login`, `search`, `join_group`).
abstract final class AnalyticsEvents {
  static const login = 'login';
  static const logout = 'logout';
  static const search = 'search';
  static const viewTeam = 'view_team';
  static const viewLecturer = 'view_lecturer';
  static const viewChangeRequest = 'view_change_request';
  static const viewSuggestions = 'view_suggestions';
  static const joinGroup = 'join_group';
  static const createTeam = 'create_team';
  static const sendInvitation = 'send_invitation';
  static const submitChangeRequest = 'submit_change_request';
  static const exportReport = 'export_report';
  static const aiRequest = 'ai_request';
  static const aiResultAccepted = 'ai_result_accepted';
  static const aiError = 'ai_error';
}

/// Google Analytics for Firebase (MB-51). Mỗi sự kiện trong `docs/analytics-plan.md` là một method có kiểu,
/// để ViewModel không tự ghép tên sự kiện. Không đưa email hay tên người vào tham số; chỉ dùng id nội bộ.
/// Mọi method không ném lỗi.
abstract interface class AnalyticsService {
  /// Id nội bộ của người dùng (Identity), `null` khi đăng xuất.
  Future<void> setUserId(String? id);

  Future<void> logLogin({String method = 'google'});

  Future<void> logLogout();

  /// S05: tìm nhóm. [term] bị cắt còn 100 ký tự; có `@` (giống email) thì không gửi.
  Future<void> logSearch(String term, int resultCount, {String? filter});

  Future<void> logViewTeam(String teamId, {TeamViewSource source = TeamViewSource.discover});

  Future<void> logViewLecturer(String lecturerId);

  Future<void> logViewChangeRequest(String requestId, {required String status});

  /// S07: số nhóm gợi ý hiển thị so với giới hạn Remote Config `suggestions_max_items`.
  Future<void> logViewSuggestions(int count, {required int maxItems});

  /// S09: gửi yêu cầu tham gia nhóm thành công.
  Future<void> logJoinGroup(String teamId, {required int ideaCount});

  Future<void> logCreateTeam(String teamId, {String? course});

  /// S18 / S19: trưởng nhóm gửi lời mời thành công.
  Future<void> logSendInvitation(String teamId, {required InviteSource source});

  /// S28: gửi yêu cầu đổi nhóm. [elapsed] tính từ lúc mở S27 (chỉ số thành công #1: ≤ 2 phút).
  Future<void> logSubmitChangeRequest({required int changeCount, Duration? elapsed});

  /// Xuất / tải file (C05, C06…). [reportType] ví dụ `workspace_file`, [format] là đuôi file (`pdf`, `xlsx`).
  Future<void> logExportReport({required String reportType, required String format});

  /// MB-55: gửi yêu cầu tới mô hình AI.
  Future<void> logAiRequest({required String feature, required String model});

  /// MB-55: người dùng dùng kết quả AI (ví dụ chép ý tưởng gợi ý vào form S09).
  Future<void> logAiResultAccepted({required String feature, required int index});

  /// MB-55: gọi AI lỗi. [errorCode] là mã ngắn (`network`, `quota`, `blocked`…), không phải nội dung lỗi.
  Future<void> logAiError({required String feature, required String errorCode});
}

class FirebaseAnalyticsService implements AnalyticsService {
  /// [analytics] để test truyền bản giả; mặc định lấy instance khi gọi, không lấy lúc tạo.
  FirebaseAnalyticsService({FirebaseAnalytics Function()? analytics})
    : _analytics = analytics ?? (() => FirebaseAnalytics.instance);

  final FirebaseAnalytics Function() _analytics;

  static const _maxText = 100;

  static String _text(String value) => value.length > _maxText ? value.substring(0, _maxText) : value;

  Future<void> _safe(String what, Future<void> Function(FirebaseAnalytics a) call) async {
    try {
      await call(_analytics());
    } catch (e) {
      debugPrint('Analytics $what failed: $e');
    }
  }

  Future<void> _log(String name, [Map<String, Object>? parameters]) =>
      _safe(name, (a) => a.logEvent(name: name, parameters: parameters));

  @override
  Future<void> setUserId(String? id) => _safe('setUserId', (a) => a.setUserId(id: id));

  @override
  Future<void> logLogin({String method = 'google'}) => _log(AnalyticsEvents.login, {'method': method});

  @override
  Future<void> logLogout() => _log(AnalyticsEvents.logout);

  @override
  Future<void> logSearch(String term, int resultCount, {String? filter}) {
    final trimmed = term.trim();
    return _log(AnalyticsEvents.search, {
      'search_term': trimmed.contains('@') ? '(redacted)' : _text(trimmed),
      'result_count': resultCount,
      if (filter != null) 'filter': _text(filter),
    });
  }

  @override
  Future<void> logViewTeam(String teamId, {TeamViewSource source = TeamViewSource.discover}) =>
      _log(AnalyticsEvents.viewTeam, {'team_id': teamId, 'source': source.name});

  @override
  Future<void> logViewLecturer(String lecturerId) => _log(AnalyticsEvents.viewLecturer, {'lecturer_id': lecturerId});

  @override
  Future<void> logViewChangeRequest(String requestId, {required String status}) =>
      _log(AnalyticsEvents.viewChangeRequest, {'request_id': requestId, 'status': _text(status)});

  @override
  Future<void> logViewSuggestions(int count, {required int maxItems}) =>
      _log(AnalyticsEvents.viewSuggestions, {'count': count, 'max_items': maxItems});

  @override
  Future<void> logJoinGroup(String teamId, {required int ideaCount}) =>
      _log(AnalyticsEvents.joinGroup, {'group_id': teamId, 'idea_count': ideaCount});

  @override
  Future<void> logCreateTeam(String teamId, {String? course}) =>
      _log(AnalyticsEvents.createTeam, {'team_id': teamId, 'course': ?course});

  @override
  Future<void> logSendInvitation(String teamId, {required InviteSource source}) =>
      _log(AnalyticsEvents.sendInvitation, {'team_id': teamId, 'source': source.name});

  @override
  Future<void> logSubmitChangeRequest({required int changeCount, Duration? elapsed}) =>
      _log(AnalyticsEvents.submitChangeRequest, {'change_count': changeCount, 'elapsed_sec': ?elapsed?.inSeconds});

  @override
  Future<void> logExportReport({required String reportType, required String format}) =>
      _log(AnalyticsEvents.exportReport, {'report_type': _text(reportType), 'format': _text(format)});

  @override
  Future<void> logAiRequest({required String feature, required String model}) =>
      _log(AnalyticsEvents.aiRequest, {'feature': feature, 'model': _text(model)});

  @override
  Future<void> logAiResultAccepted({required String feature, required int index}) =>
      _log(AnalyticsEvents.aiResultAccepted, {'feature': feature, 'index': index});

  @override
  Future<void> logAiError({required String feature, required String errorCode}) =>
      _log(AnalyticsEvents.aiError, {'feature': feature, 'error_code': _text(errorCode)});
}

/// Ghi đè trong test bằng `FakeAnalyticsService` (`test/support/firebase_fakes.dart`).
final analyticsServiceProvider = Provider<AnalyticsService>((ref) => FirebaseAnalyticsService());
