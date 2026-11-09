import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:exe_team_matching/core/ai/ai_service.dart';
import 'package:exe_team_matching/core/firebase/analytics_service.dart';
import 'package:exe_team_matching/core/firebase/crash_reporter.dart';
import 'package:exe_team_matching/core/firebase/remote_config_service.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/settings/firebase_demo_view_model.dart';
import 'package:firebase_ai/firebase_ai.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../support/firebase_fakes.dart';

const _okJson = '{"title":"Tìm trọ gần trường","problem":"Khó tìm phòng uy tín.",'
    '"solution":"App có đánh giá của người thuê cũ.","targetCustomer":"Sinh viên năm nhất.",'
    '"tags":["EdTech","Blockchain","FinTech","Du lịch","HealthTech"]}';

const _tags = ['EdTech', 'FinTech', 'Du lịch', 'HealthTech'];

/// Mô hình giả: trả [reply] hoặc ném [error]; ghi lại tham số để kiểm tra nội dung gửi đi.
class FakeAiModelClient implements AiModelClient {
  FakeAiModelClient({this.reply = _okJson, this.error, this.hang = false});

  final String? reply;
  final Object? error;
  final bool hang;
  final calls = <({String model, String systemInstruction, String prompt, Schema schema})>[];

  @override
  Future<String?> generateJson({
    required String model,
    required String systemInstruction,
    required String prompt,
    required Schema responseSchema,
  }) async {
    calls.add((model: model, systemInstruction: systemInstruction, prompt: prompt, schema: responseSchema));
    if (hang) return Completer<String?>().future;
    if (error != null) throw error!;
    return reply;
  }
}

class _QuotaThenOk extends FakeAiModelClient {
  @override
  Future<String?> generateJson({
    required String model,
    required String systemInstruction,
    required String prompt,
    required Schema responseSchema,
  }) async {
    final reply = await super.generateJson(model: model, systemInstruction: systemInstruction, prompt: prompt, responseSchema: responseSchema);
    if (calls.length == 1) throw ServerException('429 RESOURCE_EXHAUSTED');
    return reply;
  }
}

const _enabled = AppRemoteConfig(aiIdeaEnabled: true, aiModel: 'gemini-test', aiPrompt: 'PROMPT');

void main() {
  late FakeAnalyticsService analytics;
  late FakeCrashReporter crash;

  setUp(() {
    analytics = FakeAnalyticsService();
    crash = FakeCrashReporter();
  });

  FirebaseAiService service(FakeAiModelClient client, {AppRemoteConfig config = _enabled}) => FirebaseAiService(
    client: client,
    config: config,
    analytics: analytics,
    crash: crash,
    timeout: const Duration(milliseconds: 50),
    retryDelay: Duration.zero,
  );

  Future<IdeaDraft> suggest(FirebaseAiService s, [String description = 'App tìm phòng trọ cho sinh viên']) =>
      s.suggestIdea(description: description, allowedTags: _tags);

  Matcher aiError(String code) => throwsA(isA<AiException>().having((e) => e.code, 'code', code));

  test('cờ tắt → disabled, không gọi mô hình, không log ai_request', () async {
    final client = FakeAiModelClient();
    await expectLater(suggest(service(client, config: AppRemoteConfig.defaults)), aiError(AiException.disabled));
    expect(client.calls, isEmpty);
    expect(analytics.events, isEmpty);
  });

  test('thành công: dùng model / prompt từ Remote Config, ép JSON theo schema, lọc tag, log ai_request', () async {
    final client = FakeAiModelClient();
    final draft = await suggest(service(client), '  App tìm phòng trọ cho sinh viên  ');

    expect(draft.title, 'Tìm trọ gần trường');
    expect(draft.tags, ['EdTech', 'FinTech', 'Du lịch']);

    final call = client.calls.single;
    expect(call.model, 'gemini-test');
    expect(call.systemInstruction, 'PROMPT');
    expect(call.prompt, contains('App tìm phòng trọ cho sinh viên'));
    expect(call.prompt, contains(_tags.join(', ')));

    final schema = call.schema.toJson();
    expect(schema['type'], 'OBJECT');
    final properties = schema['properties'] as Map;
    expect(properties.keys, unorderedEquals(['title', 'problem', 'solution', 'targetCustomer', 'tags']));
    expect((properties['tags'] as Map)['type'], 'ARRAY');
    expect(((properties['tags'] as Map)['items'] as Map)['enum'], _tags);

    expect(analytics.events.single.name, AnalyticsEvents.aiRequest);
    expect(analytics.events.single.parameters, {'feature': 'idea_suggest', 'model': 'gemini-test'});
  });

  test('quyền riêng tư: prompt chỉ gồm mô tả và danh sách tag', () {
    final prompt = FirebaseAiService.buildIdeaPrompt('mô tả', ['A', 'B']);
    expect(prompt, 'Mô tả của sinh viên:\n"""\nmô tả\n"""\n\nDanh sách tag được chọn: A, B');
    expect(FirebaseAiService.buildIdeaPrompt('x' * 2000, const []).length, lessThan(1100));
  });

  final cases = <String, (FakeAiModelClient, String)>{
    'QuotaExceeded': (FakeAiModelClient(error: QuotaExceeded('Quota exceeded for model')), AiException.quota),
    '429 RESOURCE_EXHAUSTED': (
      FakeAiModelClient(error: ServerException('429 RESOURCE_EXHAUSTED: try later')),
      AiException.quota,
    ),
    'SocketException': (FakeAiModelClient(error: const SocketException('Failed host lookup')), AiException.network),
    'TimeoutException (quá 20 giây)': (FakeAiModelClient(hang: true), AiException.network),
    'safety block': (
      FakeAiModelClient(error: FirebaseAIException('Candidate was blocked due to FinishReason.safety')),
      AiException.blocked,
    ),
    'không có text': (FakeAiModelClient(reply: null), AiException.blocked),
    'JSON hỏng': (FakeAiModelClient(reply: '{"title": "thiếu ngoặc'), AiException.badJson),
    'JSON thiếu trường': (FakeAiModelClient(reply: jsonEncode({'title': 'Chỉ có tên'})), AiException.badJson),
    'lỗi lạ': (FakeAiModelClient(error: StateError('bất ngờ')), AiException.unknown),
  };

  for (final MapEntry(key: name, value: (client, code)) in cases.entries) {
    test('map lỗi: $name → $code, log ai_error', () async {
      await expectLater(suggest(service(client)), aiError(code));
      expect(analytics.names, [AnalyticsEvents.aiRequest, AnalyticsEvents.aiError]);
      expect(analytics.events.last.parameters, {'feature': 'idea_suggest', 'error_code': code});
      // Mọi lỗi đều ghi non-fatal lên Crashlytics để chẩn đoán.
      expect(crash.errors, hasLength(1));
    });
  }

  test('timeout mặc định 30 giây', () {
    expect(FirebaseAiService.defaultTimeout, const Duration(seconds: 30));
  });

  test('hết quota → thử model dự phòng', () async {
    final client = _QuotaThenOk();
    final draft = await suggest(service(client));
    expect(draft.title, 'Tìm trọ gần trường');
    expect(client.calls.map((c) => c.model), ['gemini-test', FirebaseAiService.fallbackModel]);
  });

  test('lỗi mạng → thử lại cùng model một lần', () async {
    final client = FakeAiModelClient(error: const SocketException('x'));
    await expectLater(suggest(service(client)), aiError(AiException.network));
    expect(client.calls.map((c) => c.model), ['gemini-test', 'gemini-test']);
  });

  test('lỗi JSON → không thử lại', () async {
    final client = FakeAiModelClient(reply: '{');
    await expectLater(suggest(service(client)), aiError(AiException.badJson));
    expect(client.calls, hasLength(1));
  });

  group('provider', () {
    ProviderContainer container(FakeAiModelClient client, FakeRemoteConfigService rc) {
      final c = ProviderContainer(overrides: [
        aiModelClientProvider.overrideWithValue(client),
        remoteConfigServiceProvider.overrideWithValue(rc),
        analyticsServiceProvider.overrideWithValue(analytics),
        crashReporterProvider.overrideWithValue(crash),
      ]);
      addTearDown(c.dispose);
      return c;
    }

    test('aiServiceProvider đọc cờ từ Remote Config, đổi trên Console thì có hiệu lực', () async {
      final rc = FakeRemoteConfigService();
      final client = FakeAiModelClient();
      final c = container(client, rc);
      c.listen(remoteConfigProvider, (_, _) {});

      await expectLater(
        c.read(aiServiceProvider).suggestIdea(description: 'x', allowedTags: _tags),
        aiError(AiException.disabled),
      );

      rc.push(_enabled);
      await Future<void>.delayed(Duration.zero);
      final draft = await c.read(aiServiceProvider).suggestIdea(description: 'x', allowedTags: _tags);
      expect(draft.title, 'Tìm trọ gần trường');
      expect(client.calls.single.model, 'gemini-test');
    });

    test('Demo Firebase "Thử AI": ViewModel trả bản nháp hoặc mã lỗi', () async {
      final ok = container(FakeAiModelClient(), FakeRemoteConfigService(_enabled));
      ok.listen(aiDemoViewModelProvider, (_, _) {});
      expect(ok.read(aiDemoViewModelProvider), isNull);
      final pending = ok.read(aiDemoViewModelProvider.notifier).suggest('App tìm trọ');
      expect(ok.read(aiDemoViewModelProvider), isA<ViewLoading<IdeaDraft>>());
      await pending;
      expect(ok.read(aiDemoViewModelProvider)?.valueOrNull?.tags, ['EdTech', 'FinTech', 'Du lịch']);

      final off = container(FakeAiModelClient(), FakeRemoteConfigService());
      off.listen(aiDemoViewModelProvider, (_, _) {});
      await off.read(aiDemoViewModelProvider.notifier).suggest('App tìm trọ');
      final state = off.read(aiDemoViewModelProvider);
      expect(state, isA<ViewError<IdeaDraft>>());
      expect((state! as ViewError<IdeaDraft>).error, const AiException(AiException.disabled));
    });
  });
}
