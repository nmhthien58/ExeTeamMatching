import 'package:go_router/go_router.dart';

import 'lecturer_detail_screen.dart';
import 'lecturer_preferences_screen.dart';
import 'paths.dart';

final lecturerRoutes = [
  GoRoute(
    path: lecturerPreferencesPath,
    builder: (_, _) => const LecturerPreferencesScreen(),
  ),
  GoRoute(
    path: '$lecturerPreferencesPath/:id',
    builder: (_, s) => LecturerDetailScreen(lecturerId: s.pathParameters['id']!),
  ),
  // Route aliases for convenience
  GoRoute(
    path: '/sv/giang-vien',
    redirect: (_, _) => lecturerPreferencesPath,
  ),
  GoRoute(
    path: '/sv/giang-vien/:id',
    redirect: (_, s) => '$lecturerPreferencesPath/${s.pathParameters['id']}',
  ),
];
