import 'dart:async';

import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/firebase/crash_reporter.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';

/// Bản giả của các service Firebase (MB-50/51) cho test ViewModel và widget:
/// ```dart
/// final analytics = FakeAnalyticsService();
/// final container = ProviderContainer(overrides: [analyticsServiceProvider.overrideWithValue(analytics)]);
/// ...
/// expect(analytics.names, ['search']);
/// ```

/// Một sự kiện đã ghi.
class LoggedEvent {
  const LoggedEvent(this.name, [this.parameters = const {}]);

  final String name;
  final Map<String, Object> parameters;

  @override
  String toString() => 'LoggedEvent($name, $parameters)';
}

class FakeAnalyticsService implements AnalyticsService {
  final events = <LoggedEvent>[];
  final userIds = <String?>[];

  List<String> get names => [for (final e in events) e.name];

  Future<void> _log(String name, [Map<String, Object> parameters = const {}]) async =>
      events.add(LoggedEvent(name, parameters));

  @override
  Future<void> setUserId(String? id) async => userIds.add(id);

  @override
  Future<void> logLogin({String method = 'google'}) => _log(AnalyticsEvents.login, {'method': method});

  @override
  Future<void> logLogout() => _log(AnalyticsEvents.logout);

  @override
  Future<void> logSearch(String term, int resultCount, {String? filter}) =>
      _log(AnalyticsEvents.search, {'search_term': term, 'result_count': resultCount, 'filter': ?filter});

  @override
  Future<void> logViewTeam(String teamId, {TeamViewSource source = TeamViewSource.discover}) =>
      _log(AnalyticsEvents.viewTeam, {'team_id': teamId, 'source': source.name});

  @override
  Future<void> logViewLecturer(String lecturerId) => _log(AnalyticsEvents.viewLecturer, {'lecturer_id': lecturerId});

  @override
  Future<void> logViewChangeRequest(String requestId, {required String status}) =>
      _log(AnalyticsEvents.viewChangeRequest, {'request_id': requestId, 'status': status});

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
      _log(AnalyticsEvents.exportReport, {'report_type': reportType, 'format': format});

  @override
  Future<void> logAiRequest({required String feature, required String model}) =>
      _log(AnalyticsEvents.aiRequest, {'feature': feature, 'model': model});

  @override
  Future<void> logAiResultAccepted({required String feature, required int index}) =>
      _log(AnalyticsEvents.aiResultAccepted, {'feature': feature, 'index': index});

  @override
  Future<void> logAiError({required String feature, required String errorCode}) =>
      _log(AnalyticsEvents.aiError, {'feature': feature, 'error_code': errorCode});
}

class RecordedError {
  const RecordedError(this.error, this.reason);

  final Object error;
  final String? reason;
}

class FakeCrashReporter implements CrashReporter {
  final errors = <RecordedError>[];
  final keys = <String, Object>{};
  String? userId;
  int testCrashes = 0;

  @override
  Future<void> recordNonFatal(Object error, StackTrace? stack, {String? reason}) async =>
      errors.add(RecordedError(error, reason));

  @override
  Future<void> setUser(String? id) async => userId = id;

  @override
  Future<void> setKey(String key, Object value) async => keys[key] = value;

  @override
  Future<void> clearUser() async {
    userId = null;
    keys.removeWhere((k, _) => userKeys.contains(k));
  }

  @override
  Future<void> testCrash() async => testCrashes++;
}

/// Remote Config giả: đổi giá trị bằng [push] để thử màn hình phản ứng khi Console publish.
class FakeRemoteConfigService implements RemoteConfigService {
  FakeRemoteConfigService([this._current = AppRemoteConfig.defaults]);

  AppRemoteConfig _current;
  final _updates = StreamController<AppRemoteConfig>.broadcast();
  int initializeCalls = 0;

  void push(AppRemoteConfig value) {
    _current = value;
    _updates.add(value);
  }

  @override
  Future<void> initialize({Duration timeout = const Duration(seconds: 5)}) async => initializeCalls++;

  @override
  AppRemoteConfig get current => _current;

  @override
  Stream<AppRemoteConfig> get updates => _updates.stream;
}
