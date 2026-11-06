import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';

final notificationsApiProvider = Provider<NotificationsApi>(
  (ref) => NotificationsApi(ref.watch(dioProvider)),
);

class AppNotification {
  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.content,
    required this.isRead,
    required this.createdAt,
    this.linkUrl,
  });

  final String id;
  final String type;
  final String title;
  final String content;
  final String? linkUrl;
  final bool isRead;
  final DateTime createdAt;

  factory AppNotification.fromJson(Map<String, dynamic> json) =>
      AppNotification(
        id: json['id'] as String,
        type: json['type'] as String? ?? '',
        title: json['title'] as String? ?? '',
        content: json['content'] as String? ?? '',
        linkUrl: json['linkUrl'] as String?,
        isRead: json['isRead'] as bool? ?? false,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  AppNotification read() => AppNotification(
    id: id,
    type: type,
    title: title,
    content: content,
    isRead: true,
    createdAt: createdAt,
    linkUrl: linkUrl,
  );
}

class NotificationPage {
  const NotificationPage({required this.items, required this.totalPages});
  final List<AppNotification> items;
  final int totalPages;

  factory NotificationPage.fromJson(Map<String, dynamic> json) =>
      NotificationPage(
        items: [
          for (final item in (json['items'] as List? ?? const []))
            AppNotification.fromJson(Map<String, dynamic>.from(item as Map)),
        ],
        totalPages: json['totalPages'] as int? ?? 1,
      );
}

class NotificationsApi {
  const NotificationsApi(this._dio);
  final Dio _dio;

  Future<NotificationPage> list({int page = 1}) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/notifications',
      queryParameters: {'page': page, 'pageSize': 20},
    );
    return NotificationPage.fromJson(response.data!);
  }

  Future<int> unreadCount() async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/api/notifications/unread-count',
    );
    return response.data?['count'] as int? ?? 0;
  }

  Future<void> read(String id) =>
      _dio.patch<void>('/api/notifications/$id/read');
  Future<void> readAll() => _dio.patch<void>('/api/notifications/read-all');
}
