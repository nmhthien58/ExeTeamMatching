import '../suggestions/suggestions_api.dart' show CatalogRef;

Map<String, dynamic> _map(dynamic v) => Map<String, dynamic>.from(v as Map);

class LecturerSummary {
  const LecturerSummary({
    required this.id,
    required this.fullName,
    required this.email,
    this.domains = const [],
    this.maxTeamsExe101,
    this.maxTeamsExe201,
    this.isActive = true,
    this.currentAssignedTeamCount,
    this.maxTeams,
    this.isFull = false,
  });

  final String id;
  final String fullName;
  final String email;
  final List<CatalogRef> domains;
  final int? maxTeamsExe101;
  final int? maxTeamsExe201;
  final bool isActive;
  final int? currentAssignedTeamCount;
  final int? maxTeams;
  final bool isFull;

  factory LecturerSummary.fromJson(Map<String, dynamic> j) {
    return LecturerSummary(
      id: j['id'] as String,
      fullName: j['fullName'] as String? ?? '',
      email: j['email'] as String? ?? '',
      domains: [
        for (final d in j['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      maxTeamsExe101: j['maxTeamsExe101'] as int?,
      maxTeamsExe201: j['maxTeamsExe201'] as int?,
      isActive: j['isActive'] as bool? ?? true,
      currentAssignedTeamCount: j['currentAssignedTeamCount'] as int?,
      maxTeams: j['maxTeams'] as int?,
      isFull: j['isFull'] as bool? ?? false,
    );
  }
}

class LecturerPreferenceItem {
  const LecturerPreferenceItem({
    required this.priority,
    required this.lecturer,
    required this.isActive,
    required this.currentAssignedTeamCount,
    required this.maxTeams,
    required this.isFull,
  });

  final int priority;
  final LecturerSummary lecturer;
  final bool isActive;
  final int currentAssignedTeamCount;
  final int maxTeams;
  final bool isFull;

  factory LecturerPreferenceItem.fromJson(Map<String, dynamic> j) {
    return LecturerPreferenceItem(
      priority: j['priority'] as int? ?? 1,
      lecturer: LecturerSummary.fromJson(_map(j['lecturer'])),
      isActive: j['isActive'] as bool? ?? true,
      currentAssignedTeamCount: j['currentAssignedTeamCount'] as int? ?? 0,
      maxTeams: j['maxTeams'] as int? ?? 0,
      isFull: j['isFull'] as bool? ?? false,
    );
  }
}

class TopicInterestItem {
  const TopicInterestItem({
    required this.id,
    required this.teamId,
    required this.advisorId,
    required this.advisorName,
    required this.advisorEmail,
    required this.advisorRole,
    this.domains = const [],
    required this.status,
  });

  final String id;
  final String teamId;
  final String advisorId;
  final String advisorName;
  final String advisorEmail;
  final String advisorRole;
  final List<CatalogRef> domains;
  final String status;

  factory TopicInterestItem.fromJson(Map<String, dynamic> j) {
    final advisor = j['advisor'] != null ? _map(j['advisor']) : <String, dynamic>{};
    return TopicInterestItem(
      id: j['id'] as String,
      teamId: j['teamId'] as String? ?? '',
      advisorId: advisor['id'] as String? ?? '',
      advisorName: advisor['fullName'] as String? ?? '',
      advisorEmail: advisor['email'] as String? ?? '',
      advisorRole: advisor['role'] as String? ?? 'LECTURER',
      domains: [
        for (final d in advisor['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      status: j['status'] as String? ?? 'OPEN',
    );
  }
}
