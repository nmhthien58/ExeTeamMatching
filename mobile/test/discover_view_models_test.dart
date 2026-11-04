import 'package:dio/dio.dart';
import 'package:exe_team_matching/core/network/api_exception.dart';
import 'package:exe_team_matching/core/view_state.dart';
import 'package:exe_team_matching/features/discover/discover_api.dart';
import 'package:exe_team_matching/features/discover/join_request_view_model.dart';
import 'package:exe_team_matching/features/discover/paged_list.dart';
import 'package:exe_team_matching/features/discover/post_form_view_model.dart';
import 'package:exe_team_matching/features/discover/posts_api.dart';
import 'package:exe_team_matching/features/discover/seeking_posts_view_model.dart';
import 'package:exe_team_matching/features/my_team/my_team_api.dart' show Idea;
import 'package:exe_team_matching/features/suggestions/suggestions_api.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

SeekingPost _post(String id) => SeekingPost(
  id: id,
  title: 'Vai trò $id',
  student: StudentRef(id: 's$id', studentCode: 'SE$id', fullName: 'SV $id'),
  domainIds: const [],
);

const _error = ApiException(status: 400, code: 'x', title: 'Lỗi');

class _FakePosts extends PostsApi {
  _FakePosts() : super(Dio());

  List<List<SeekingPost>> pages = const [];
  Object? error;
  final calls = <String>[];

  @override
  Future<SeekingPage> seeking({int? specializationId, required int page}) async {
    calls.add('seeking $specializationId $page');
    if (error != null) throw error!;
    return SeekingPage(items: page <= pages.length ? pages[page - 1] : const [], page: page, totalPages: pages.length);
  }

  @override
  Future<SeekingPost> saveMySeeking({required String title, String? description, required List<int> domainIds}) async {
    calls.add('save $title $description $domainIds');
    if (error != null) throw error!;
    return _post('me');
  }

  @override
  Future<void> removeMySeeking() async {
    calls.add('remove');
    if (error != null) throw error!;
  }
}

class _FakeDiscover extends DiscoverApi {
  _FakeDiscover() : super(Dio());

  Object? error;
  final sent = <(String, String?, List<Idea>, bool)>[];

  @override
  Future<void> requestToJoin(String teamId, {String? message, required List<Idea> ideas, required bool hasNoIdea}) async {
    if (error != null) throw error!;
    sent.add((teamId, message, ideas, hasNoIdea));
  }
}

Future<void> _settle() => Future<void>.delayed(Duration.zero);

/// MB-49 – ViewModel của S09, S10, S11 test riêng, không dựng widget.
void main() {
  group('SeekingPostsViewModel (S10)', () {
    late _FakePosts api;
    late ProviderContainer c;

    setUp(() {
      api = _FakePosts();
      c = ProviderContainer(overrides: [postsApiProvider.overrideWithValue(api)]);
      addTearDown(c.dispose);
    });

    SeekingPostsState state() => c.read(seekingPostsViewModelProvider);
    SeekingPostsViewModel vm() => c.read(seekingPostsViewModelProvider.notifier);
    void listen() => c.listen(seekingPostsViewModelProvider, (_, _) {});

    test('tải được: ViewLoading rồi ViewData, Xem thêm ghép trang 2', () async {
      api.pages = [
        [_post('1')],
        [_post('2')],
      ];
      listen();
      expect(state().results, isA<ViewLoading<PagedList<SeekingPost>>>());
      await _settle();
      expect(state().results.valueOrNull!.hasMore, isTrue);
      await vm().loadMore();
      final results = state().results.valueOrNull!;
      expect(results.items.map((p) => p.id), ['1', '2']);
      expect(results.hasMore, isFalse);
    });

    test('trống: ViewEmpty', () async {
      api.pages = [[]];
      listen();
      await _settle();
      expect(state().results, isA<ViewEmpty<PagedList<SeekingPost>>>());
    });

    test('lỗi lần đầu: ViewError; lỗi trang kế: giữ danh sách, có loadMoreError', () async {
      api.error = _error;
      listen();
      await _settle();
      expect((state().results as ViewError).error, _error);

      api
        ..error = null
        ..pages = [
          [_post('1')],
          [_post('2')],
        ];
      await vm().retry();
      api.error = _error;
      await vm().loadMore();
      final results = state().results.valueOrNull!;
      expect(results.items, hasLength(1));
      expect(results.loadMoreError, _error);
    });

    test('lọc chuyên ngành tải lại trang 1; đánh dấu đã mời', () async {
      api.pages = [
        [_post('1')],
      ];
      listen();
      await _settle();
      await vm().selectSpecialization(7);
      expect(state().specializationId, 7);
      expect(api.calls.last, 'seeking 7 1');
      vm().markInvited('s1');
      expect(state().invited, {'s1'});
    });
  });

  group('SeekingPostFormViewModel (S11)', () {
    late _FakePosts api;
    late ProviderContainer c;

    setUp(() {
      api = _FakePosts();
      c = ProviderContainer(overrides: [postsApiProvider.overrideWithValue(api)]);
      addTearDown(c.dispose);
      c.listen(seekingPostFormViewModelProvider, (_, _) {});
    });

    SeekingPostFormViewModel vm() => c.read(seekingPostFormViewModelProvider.notifier);

    test('tiêu đề trống: không gọi API, báo lỗi; gõ lại thì hết lỗi', () async {
      expect(await vm().save(title: ' ', description: '', domainIds: const [], isEdit: false), isA<PostFormNotSaved>());
      expect(api.calls, isEmpty);
      expect(c.read(seekingPostFormViewModelProvider).titleError, 'Nhập vai trò mong muốn.');
      vm().titleChanged('Mobile');
      expect(c.read(seekingPostFormViewModelProvider).titleError, isNull);
    });

    test('lưu được: trả câu SnackBar, đã trim', () async {
      final outcome = await vm().save(title: ' Mobile ', description: ' Flutter ', domainIds: const [5], isEdit: true);
      expect((outcome as PostFormDone).message, 'Đã lưu bài');
      expect(api.calls, ['save Mobile Flutter [5]']);
    });

    test('API lỗi: giữ form, banner theo postErrors', () async {
      api.error = const ApiException(status: 409, code: 'already-in-team', title: 'x');
      expect(await vm().remove(), isA<PostFormNotSaved>());
      final state = c.read(seekingPostFormViewModelProvider);
      expect(state.saving, isFalse);
      expect(state.generalError, postErrors['already-in-team']);
    });
  });

  group('JoinRequestViewModel (S09)', () {
    late _FakeDiscover api;
    late ProviderContainer c;

    setUp(() {
      api = _FakeDiscover();
      c = ProviderContainer(overrides: [discoverApiProvider.overrideWithValue(api)]);
      addTearDown(c.dispose);
      c.listen(joinRequestViewModelProvider('t1'), (_, _) {});
    });

    JoinRequestViewModel vm() => c.read(joinRequestViewModelProvider('t1').notifier);
    JoinRequestState state() => c.read(joinRequestViewModelProvider('t1'));

    test('chưa có ý tưởng nào và không chọn "Chưa có ý tưởng": báo thiếu, không gửi', () async {
      expect(await vm().send(message: '', ideas: [('  ', 'mô tả')]), isA<JoinRequestNotSent>());
      expect(state().ideasMissing, isTrue);
      expect(api.sent, isEmpty);
      vm().setNoIdea(true);
      expect(state().ideasMissing, isFalse);
    });

    test('gửi ý tưởng đã trim, mô tả rỗng thành null', () async {
      expect(await vm().send(message: ' Chào ', ideas: [(' App ', ' ')]), isA<JoinRequestSent>());
      final (teamId, message, ideas, noIdea) = api.sent.single;
      expect((teamId, message, noIdea), ('t1', 'Chào', false));
      expect((ideas.single.title, ideas.single.description), ('App', null));
    });

    test('lỗi gửi: giữ sheet, hiện detail', () async {
      api.error = const ApiException(status: 409, code: 'x', title: 'Lỗi', detail: 'Đã có nhóm');
      vm().setNoIdea(true);
      expect(await vm().send(message: '', ideas: const []), isA<JoinRequestNotSent>());
      expect(state().error, 'Đã có nhóm');
      expect(state().sending, isFalse);
    });
  });
}
