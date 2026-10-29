import 'package:go_router/go_router.dart';
import 'change_request_screens.dart';

/// Đăng ký trong shell sinh viên khi tích hợp router chung.
final changeRequestRoutes = <GoRoute>[
  GoRoute(
    path: changeRequestListPath,
    builder: (_, _) => const ChangeRequestListScreen(),
  ),
  GoRoute(
    path: changeRequestCreatePath,
    builder: (_, _) => const CreateChangeRequestScreen(),
  ),
  GoRoute(
    path: '$changeRequestListPath/:id',
    builder: (_, state) =>
        ChangeRequestDetailScreen(requestId: state.pathParameters['id']!),
  ),
];
