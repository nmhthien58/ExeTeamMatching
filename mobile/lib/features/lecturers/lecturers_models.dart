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
      id: (j['id'] ?? j['lecturerId'] ?? '') as String,
      fullName: (j['fullName'] ?? '') as String,
      email: (j['email'] ?? '') as String,
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

class LecturerRef {
  const LecturerRef({
    required this.lecturerId,
    required this.fullName,
    this.domains = const [],
  });

  final String lecturerId;
  final String fullName;
  final List<CatalogRef> domains;

  factory LecturerRef.fromJson(Map<String, dynamic> j) {
    return LecturerRef(
      lecturerId: (j['lecturerId'] ?? j['id'] ?? '') as String,
      fullName: (j['fullName'] ?? '') as String,
      domains: [
        for (final d in j['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
    );
  }
}

class LecturerPreferenceItem {
  const LecturerPreferenceItem({
    required this.lecturerId,
    required this.fullName,
    this.domains = const [],
    required this.rank,
    this.isInterested = false,
  });

  final String lecturerId;
  final String fullName;
  final List<CatalogRef> domains;
  final int rank;
  final bool isInterested;

  factory LecturerPreferenceItem.fromJson(Map<String, dynamic> j) {
    return LecturerPreferenceItem(
      lecturerId: (j['lecturerId'] ?? j['id'] ?? '') as String,
      fullName: (j['fullName'] ?? '') as String,
      domains: [
        for (final d in j['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      rank: j['rank'] as int? ?? 1,
      isInterested: j['isInterested'] as bool? ?? false,
    );
  }
}

class LecturerPreferenceListResponse {
  const LecturerPreferenceListResponse({
    this.maxPreferences = 3,
    this.locked = false,
    this.lockAt,
    this.preferences = const [],
    this.interestedLecturers = const [],
    this.previousInstructor,
  });

  final int maxPreferences;
  final bool locked;
  final DateTime? lockAt;
  final List<LecturerPreferenceItem> preferences;
  final List<LecturerRef> interestedLecturers;
  final LecturerRef? previousInstructor;

  factory LecturerPreferenceListResponse.fromJson(Map<String, dynamic> j) {
    return LecturerPreferenceListResponse(
      maxPreferences: j['maxPreferences'] as int? ?? 3,
      locked: j['locked'] as bool? ?? false,
      lockAt: j['lockAt'] != null ? DateTime.tryParse(j['lockAt'] as String) : null,
      preferences: [
        for (final p in j['preferences'] ?? const [])
          LecturerPreferenceItem.fromJson(_map(p)),
      ],
      interestedLecturers: [
        for (final l in j['interestedLecturers'] ?? const [])
          LecturerRef.fromJson(_map(l)),
      ],
      previousInstructor: j['previousInstructor'] != null
          ? LecturerRef.fromJson(_map(j['previousInstructor']))
          : null,
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
      id: (j['id'] ?? '') as String,
      teamId: (j['teamId'] ?? '') as String,
      advisorId: (j['advisorId'] ?? advisor['id'] ?? '') as String,
      advisorName: (j['advisorName'] ?? advisor['fullName'] ?? '') as String,
      advisorEmail: (j['advisorEmail'] ?? advisor['email'] ?? '') as String,
      advisorRole: (j['advisorKind'] ?? advisor['role'] ?? 'LECTURER') as String,
      domains: [
        for (final d in j['domains'] ?? advisor['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      status: (j['status'] ?? 'OPEN') as String,
    );
  }
}
