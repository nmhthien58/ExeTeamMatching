import 'package:exe_team_matching/core/auth/session.dart';
import 'package:exe_team_matching/core/providers.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_store.dart';
import 'package:exe_team_matching/features/onboarding/onboarding_view_model.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Store extends OnboardingStore {
  bool seen = false;
  bool fail = false;
  int marks = 0;

  @override
  Future<bool> hasSeen() async {
    if (fail) throw StateError('đọc lỗi');
    return seen;
  }

  @override
  Future<void> markSeen() async {
    marks++;
    if (fail) throw StateError('ghi lỗi');
  }
}

class _Auth extends AuthController {
  _Auth(this.user);

  final UserInfo? user;

  @override
  Future<UserInfo?> build() async => user;
}

/// MB-49 – ViewModel của A01, A02, không dựng widget.
void main() {
  late _Store store;

  ProviderContainer create({UserInfo? user}) {
    final c = ProviderContainer(overrides: [
      onboardingStoreProvider.overrideWithValue(store),
      authControllerProvider.overrideWith(() => _Auth(user)),
    ]);
    addTearDown(c.dispose);
    c.listen(splashViewModelProvider, (_, _) {});
    c.listen(onboardingViewModelProvider, (_, _) {});
    return c;
  }

  setUp(() => store = _Store());

  test('A01: chưa đăng nhập → A02 lần đầu, đã xem → A03, lỗi đọc → A03', () async {
    final c = create();
    final vm = c.read(splashViewModelProvider.notifier);
    expect(await vm.resolve(), '/gioi-thieu');
    store.seen = true;
    expect(await vm.resolve(), '/dang-nhap');
    store.fail = true;
    expect(await vm.resolve(), '/dang-nhap');
  });

  test('A01: đã đăng nhập → khung theo vai trò; admin → chỉ dùng web', () async {
    var c = create(user: const UserInfo(id: 'u', email: 'gv@fe.edu.vn', fullName: 'GV', roles: {Role.lecturer}));
    expect(await c.read(splashViewModelProvider.notifier).resolve(), '/gv');
    c = create(user: const UserInfo(id: 'u', email: 'a@fe.edu.vn', fullName: 'A', roles: {Role.admin}));
    expect(await c.read(splashViewModelProvider.notifier).resolve(), '/chi-dung-web');
  });

  test('A02: lưu đã xem, chặn bấm lần hai, lỗi trả false', () async {
    final c = create();
    final vm = c.read(onboardingViewModelProvider.notifier);
    final first = vm.finish();
    expect(c.read(onboardingViewModelProvider), isTrue);
    expect(await vm.finish(), isFalse);
    expect(await first, isTrue);
    expect((store.marks, c.read(onboardingViewModelProvider)), (1, false));

    store.fail = true;
    expect(await vm.finish(), isFalse);
    expect(c.read(onboardingViewModelProvider), isFalse);
  });

  test('A02: store truyền từ màn được ưu tiên', () async {
    final other = _Store();
    final c = create();
    expect(await c.read(onboardingViewModelProvider.notifier).finish(store: other), isTrue);
    expect((other.marks, store.marks), (1, 0));
  });
}
