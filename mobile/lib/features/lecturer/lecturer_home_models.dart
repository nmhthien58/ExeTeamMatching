import '../suggestions/suggestions_api.dart' show CatalogRef;

Map<String, dynamic> _map(dynamic v) => Map<String, dynamic>.from(v as Map);

class TopicInterestItem {
  const TopicInterestItem({
    required this.id,
    required this.teamId,
    required this.advisorId,
    required this.advisorName,
    this.advisorEmail = '',
    this.advisorRole = 'LECTURER',
    this.domains = const [],
    required this.status,
    this.wasPreviousInstructor = false,
  });

  final String id;
  final String teamId;
  final String advisorId;
  final String advisorName;
  final String advisorEmail;
  final String advisorRole;
  final List<CatalogRef> domains;
  final String status;
  final bool wasPreviousInstructor;

  factory TopicInterestItem.fromJson(Map<String, dynamic> j) {
    final advisor = j['advisor'] != null
        ? _map(j['advisor'])
        : <String, dynamic>{};
    return TopicInterestItem(
      id: (j['id'] ?? '') as String,
      teamId:
          (j['teamId'] ?? (j['team'] != null ? _map(j['team'])['id'] : ''))
              as String,
      advisorId: (j['advisorId'] ?? advisor['id'] ?? '') as String,
      advisorName:
          (j['advisorName'] ??
                  advisor['fullName'] ??
                  (j['team'] != null ? _map(j['team'])['name'] : ''))
              as String,
      advisorEmail: (j['advisorEmail'] ?? advisor['email'] ?? '') as String,
      advisorRole:
          (j['advisorKind'] ?? advisor['role'] ?? 'LECTURER') as String,
      domains: [
        for (final d in advisor['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      status: (j['status'] ?? 'OPEN') as String,
      wasPreviousInstructor: j['wasPreviousInstructor'] as bool? ?? false,
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
    this.wasMyEXE101Team = false,
  });

  final String teamId;
  final String teamName;
  final String courseCode;
  final int memberCount;
  final String? projectName;
  final List<CatalogRef> domains;
  final int rank;
  final bool iAmInterested;
  final bool wasMyEXE101Team;

  factory LecturerPreferenceMe.fromJson(Map<String, dynamic> j) {
    final team = j['team'] != null ? _map(j['team']) : j;
    return LecturerPreferenceMe(
      teamId: (team['id'] ?? '') as String,
      teamName: (team['name'] ?? '') as String,
      courseCode: (team['courseCode'] ?? 'EXE101') as String,
      memberCount: team['memberCount'] as int? ?? 0,
      projectName: team['projectName'] as String?,
      domains: [
        for (final d in team['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      rank: j['rank'] as int? ?? 1,
      iAmInterested: j['iAmInterested'] as bool? ?? false,
      wasMyEXE101Team: (j['wasMyEXE101Team'] ??
          j['wasMyExe101Team'] ??
          team['wasMyEXE101Team'] ??
          team['wasMyExe101Team'] ??
          team['isProjectContinued']) as bool? ?? false,
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
    this.maxMemberCount,
    this.matchingDomains = const [],
    this.otherInterestedCount = 0,
    this.myPreferenceRank,
    this.myInterestId,
    this.isInterested = false,
    this.wasMyEXE101Team = false,
    this.hasAdvisor = false,
  });

  final String teamId;
  final String teamName;
  final String courseCode;
  final String? projectName;
  final String? projectDescription;
  final List<CatalogRef> domains;
  final int memberCount;
  final int? maxMemberCount;
  final List<CatalogRef> matchingDomains;
  final int otherInterestedCount;
  final int? myPreferenceRank;
  final String? myInterestId;
  final bool isInterested;
  final bool wasMyEXE101Team;
  final bool hasAdvisor;

  factory TopicRecommendationItem.fromJson(Map<String, dynamic> j) {
    final team = j['team'] != null ? _map(j['team']) : j;
    final myInterest = j['myInterest'] != null ? _map(j['myInterest']) : null;
    return TopicRecommendationItem(
      teamId: (team['id'] ?? '') as String,
      teamName: (team['name'] ?? '') as String,
      courseCode: (team['courseCode'] ?? 'EXE101') as String,
      projectName: team['projectName'] as String?,
      projectDescription: team['projectDescription'] as String?,
      domains: [
        for (final d in team['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      memberCount: team['memberCount'] as int? ?? 0,
      maxMemberCount: team['requirement'] is Map
          ? _map(team['requirement'])['maxSize'] as int?
          : null,
      matchingDomains: [
        for (final d in j['matchedDomains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
      otherInterestedCount: j['otherInterestedCount'] as int? ?? 0,
      myPreferenceRank: j['myPreferenceRank'] as int?,
      myInterestId: myInterest?['id'] as String?,
      isInterested: myInterest != null && myInterest['status'] == 'OPEN',
      wasMyEXE101Team: (j['wasMyEXE101Team'] ??
          j['wasMyExe101Team'] ??
          j['wasLecturerExe101Team'] ??
          team['wasMyEXE101Team'] ??
          team['wasMyExe101Team'] ??
          team['isProjectContinued'] ??
          myInterest?['wasPreviousInstructor']) as bool? ?? false,
      hasAdvisor: j['hasAdvisor'] as bool? ?? false,
    );
  }
}

class TopicRecommendationsResponse {
  const TopicRecommendationsResponse({
    this.openInterestCount = 0,
    this.interestLimit = 10,
    this.remainingCapacity,
    this.interestOpen = true,
    this.matchingAt,
    this.items = const [],
  });

  final int openInterestCount;
  final int interestLimit;
  final int? remainingCapacity;
  final bool interestOpen;
  final DateTime? matchingAt;
  final List<TopicRecommendationItem> items;

  factory TopicRecommendationsResponse.fromJson(Map<String, dynamic> j) {
    return TopicRecommendationsResponse(
      openInterestCount: j['openInterestCount'] as int? ?? 0,
      interestLimit: j['interestLimit'] as int? ?? 10,
      remainingCapacity: j['remainingCapacity'] as int?,
      interestOpen: j['interestOpen'] as bool? ?? true,
      matchingAt: j['matchingAt'] != null
          ? DateTime.tryParse(j['matchingAt'] as String)
          : null,
      items: [
        for (final item in j['items'] ?? const [])
          TopicRecommendationItem.fromJson(_map(item)),
      ],
    );
  }
}

class AdvisedTeamItem {
  const AdvisedTeamItem({
    required this.teamId,
    required this.teamName,
    required this.courseCode,
    this.projectName,
    required this.memberCount,
    required this.reason,
    this.preferenceRank,
    this.domains = const [],
  });

  final String teamId;
  final String teamName;
  final String courseCode;
  final String? projectName;
  final int memberCount;
  final String reason;
  final int? preferenceRank;
  final List<CatalogRef> domains;

  factory AdvisedTeamItem.fromJson(Map<String, dynamic> j) {
    final team = j['team'] != null ? _map(j['team']) : j;
    return AdvisedTeamItem(
      teamId: (team['id'] ?? '') as String,
      teamName: (team['name'] ?? '') as String,
      courseCode: (team['courseCode'] ?? 'EXE101') as String,
      projectName: team['projectName'] as String?,
      memberCount: team['memberCount'] as int? ?? 0,
      reason: (j['reason'] ?? '') as String,
      preferenceRank: j['preferenceRank'] as int?,
      domains: [
        for (final d in team['domains'] ?? const [])
          CatalogRef.fromJson(_map(d)),
      ],
    );
  }
}

class LecturerTeamsMeResponse {
  const LecturerTeamsMeResponse({
    this.maxTeams,
    this.instructing = const [],
    this.mentoring = const [],
  });

  final int? maxTeams;
  final List<AdvisedTeamItem> instructing;
  final List<AdvisedTeamItem> mentoring;

  factory LecturerTeamsMeResponse.fromJson(Map<String, dynamic> j) {
    return LecturerTeamsMeResponse(
      maxTeams: j['maxTeams'] as int?,
      instructing: [
        for (final item in j['instructing'] ?? const [])
          AdvisedTeamItem.fromJson(_map(item)),
      ],
      mentoring: [
        for (final item in j['mentoring'] ?? const [])
          AdvisedTeamItem.fromJson(_map(item)),
      ],
    );
  }
}

class LecturerSemesterStats {
  const LecturerSemesterStats({
    this.needsReplyCount = 0,
    this.overdueSubmissionsCount = 0,
    this.meetingsTodayCount,
    this.missingCheckpointsCount = 0,
    this.hasPostedWeeklySchedule = true,
  });

  final int needsReplyCount;
  final int overdueSubmissionsCount;
  final int? meetingsTodayCount;
  final int missingCheckpointsCount;
  final bool hasPostedWeeklySchedule;
}
