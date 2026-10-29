import '../suggestions/suggestions_api.dart' show CatalogRef;

Map<String, dynamic> _map(dynamic v) => Map<String, dynamic>.from(v as Map);

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
      id: j['id'] as String? ?? '',
      teamId: j['teamId'] as String? ?? (j['team'] != null ? _map(j['team'])['id'] as String : ''),
      advisorId: advisor['id'] as String? ?? '',
      advisorName: advisor['fullName'] as String? ?? (j['team'] != null ? _map(j['team'])['name'] as String : ''),
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

class LecturerPreferenceMe {
  const LecturerPreferenceMe({
    required this.teamId,
    required this.teamName,
    required this.courseCode,
    required this.memberCount,
    this.projectName,
    this.domains = const [],
    required this.rank,
    required this.iAmInterested,
  });

  final String teamId;
  final String teamName;
  final String courseCode;
  final int memberCount;
  final String? projectName;
  final List<CatalogRef> domains;
  final int rank;
  final bool iAmInterested;

  factory LecturerPreferenceMe.fromJson(Map<String, dynamic> j) {
    final team = j['team'] != null ? _map(j['team']) : j;
    return LecturerPreferenceMe(
      teamId: team['id'] as String? ?? '',
      teamName: team['name'] as String? ?? '',
      courseCode: team['courseCode'] as String? ?? 'EXE101',
      memberCount: team['memberCount'] as int? ?? 0,
      projectName: team['projectName'] as String?,
      domains: [
        for (final d in team['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      rank: j['rank'] as int? ?? 1,
      iAmInterested: j['iAmInterested'] as bool? ?? false,
    );
  }
}

class TopicRecommendationItem {
  const TopicRecommendationItem({
    required this.teamId,
    required this.teamName,
    required this.courseCode,
    this.projectName,
    this.projectDescription,
    this.domains = const [],
    required this.memberCount,
    required this.matchingDomainCount,
    this.isInheritedFromExe101 = false,
  });

  final String teamId;
  final String teamName;
  final String courseCode;
  final String? projectName;
  final String? projectDescription;
  final List<CatalogRef> domains;
  final int memberCount;
  final int matchingDomainCount;
  final bool isInheritedFromExe101;

  factory TopicRecommendationItem.fromJson(Map<String, dynamic> j) {
    return TopicRecommendationItem(
      teamId: j['teamId'] as String? ?? (j['team'] != null ? _map(j['team'])['id'] as String : ''),
      teamName: j['teamName'] as String? ?? (j['team'] != null ? _map(j['team'])['name'] as String : ''),
      courseCode: j['courseCode'] as String? ?? 'EXE101',
      projectName: j['projectName'] as String?,
      projectDescription: j['projectDescription'] as String?,
      domains: [
        for (final d in j['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      memberCount: j['memberCount'] as int? ?? 0,
      matchingDomainCount: j['matchingDomainCount'] as int? ?? 0,
      isInheritedFromExe101: j['isInheritedFromExe101'] as bool? ?? false,
    );
  }
}

class LecturerSemesterStats {
  const LecturerSemesterStats({
    this.needsReplyCount = 0,
    this.overdueSubmissionsCount = 0,
    this.meetingsTodayCount = 0,
    this.missingCheckpointsCount = 0,
    this.hasPostedWeeklySchedule = true,
    this.assignedTeams = const [],
  });

  final int needsReplyCount;
  final int overdueSubmissionsCount;
  final int meetingsTodayCount;
  final int missingCheckpointsCount;
  final bool hasPostedWeeklySchedule;
  final List<LecturerAssignedTeam> assignedTeams;
}

class LecturerAssignedTeam {
  const LecturerAssignedTeam({
    required this.id,
    required this.name,
    required this.courseCode,
    this.projectName,
    required this.memberCount,
    this.latestCheckpoint,
    this.workspaceId,
  });

  final String id;
  final String name;
  final String courseCode;
  final String? projectName;
  final int memberCount;
  final String? latestCheckpoint;
  final String? workspaceId;

  factory LecturerAssignedTeam.fromJson(Map<String, dynamic> j) {
    return LecturerAssignedTeam(
      id: j['id'] as String,
      name: j['name'] as String? ?? '',
      courseCode: j['courseCode'] as String? ?? 'EXE101',
      projectName: j['projectName'] as String?,
      memberCount: j['memberCount'] as int? ?? 0,
      latestCheckpoint: j['latestCheckpoint'] as String?,
      workspaceId: j['workspaceId'] as String?,
    );
  }
}
