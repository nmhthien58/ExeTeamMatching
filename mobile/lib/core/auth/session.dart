import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

enum Role { student, lecturer, mentor, admin }

Role? _roleFrom(String value) => switch (value) {
      'Student' => Role.student,
      'Lecturer' => Role.lecturer,
      'Mentor' => Role.mentor,
      'Admin' => Role.admin,
      _ => null,
    };

/// Người dùng đăng nhập (UserInfo trong docs/contracts/openapi/identity.yaml).
class UserInfo {
  const UserInfo({
    required this.id,
    required this.email,
    required this.fullName,
    required this.roles,
    this.studentId,
    this.lecturerId,
    this.mentorId,
    this.profileCompleted = true,
  });

  final String id;
  final String email;
  final String fullName;
  final Set<Role> roles;
  final String? studentId;
  final String? lecturerId;
  final String? mentorId;

  /// Sinh viên chưa đánh giá đủ 5 kỹ năng → A05 (quy tắc 16).
  final bool profileCompleted;

  factory UserInfo.fromJson(Map<String, dynamic> json) => UserInfo(
        id: json['id'] as String,
        email: json['email'] as String,
        fullName: json['fullName'] as String,
        roles: {for (final r in (json['roles'] as List)) ?_roleFrom('$r')},
        studentId: json['studentId'] as String?,
        lecturerId: json['lecturerId'] as String?,
        mentorId: json['mentorId'] as String?,
        profileCompleted: json['profileCompleted'] as bool? ?? true,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'email': email,
        'fullName': fullName,
        'roles': [
          for (final r in roles) switch (r) { Role.student => 'Student', Role.lecturer => 'Lecturer', Role.mentor => 'Mentor', Role.admin => 'Admin' },
        ],
        'studentId': studentId,
        'lecturerId': lecturerId,
        'mentorId': mentorId,
        'profileCompleted': profileCompleted,
      };
}

class TokenResponse {
  const TokenResponse({required this.accessToken, required this.refreshToken, required this.user});

  final String accessToken;
  final String refreshToken;
  final UserInfo user;

  factory TokenResponse.fromJson(Map<String, dynamic> json) => TokenResponse(
        accessToken: json['accessToken'] as String,
        refreshToken: json['refreshToken'] as String,
        user: UserInfo.fromJson(Map<String, dynamic>.from(json['user'] as Map)),
      );
}

/// Lưu phiên (MB-04): access token chỉ trong bộ nhớ, refresh token và thông tin người dùng trong secure storage
/// (Keystore / Keychain). Không lưu mật khẩu.
class SessionStore {
  SessionStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  static const _refreshKey = 'exe.refreshToken';
  static const _userKey = 'exe.user';

  final FlutterSecureStorage _storage;
  String? accessToken;

  Future<String?> readRefreshToken() => _storage.read(key: _refreshKey);

  Future<UserInfo?> readUser() async {
    final raw = await _storage.read(key: _userKey);
    if (raw == null) return null;
    try {
      return UserInfo.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } catch (_) {
      return null;
    }
  }

  Future<void> save(TokenResponse tokens) async {
    accessToken = tokens.accessToken;
    await _storage.write(key: _refreshKey, value: tokens.refreshToken);
    await _storage.write(key: _userKey, value: jsonEncode(tokens.user.toJson()));
  }

  Future<void> clear() async {
    accessToken = null;
    await _storage.delete(key: _refreshKey);
    await _storage.delete(key: _userKey);
  }
}
