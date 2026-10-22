// Model của Collaboration Service (docs/contracts/openapi/collab.yaml) – chỉ các trường màn hình C01, C04–C06 dùng.

DateTime? _date(Object? v) => v == null ? null : DateTime.tryParse('$v');

/// Vai trò trong workspace (RoleChip).
enum WorkspaceRole { leader, member, instructor, mentor, admin }

WorkspaceRole? _role(Object? v) => switch (v) {
      'LEADER' => WorkspaceRole.leader,
      'MEMBER' => WorkspaceRole.member,
      'INSTRUCTOR' => WorkspaceRole.instructor,
      'MENTOR' => WorkspaceRole.mentor,
      'ADMIN' => WorkspaceRole.admin,
      _ => null,
    };

class PersonRef {
  const PersonRef({required this.personType, required this.personId, required this.displayName, this.role});

  final String personType;
  final String personId;
  final String displayName;
  final WorkspaceRole? role;

  factory PersonRef.fromJson(Map<String, dynamic> j) => PersonRef(
        personType: j['personType'] as String? ?? 'STUDENT',
        personId: j['personId'] as String,
        displayName: j['displayName'] as String? ?? '',
        role: _role(j['role']),
      );
}

class RecentActivity {
  const RecentActivity({required this.kind, required this.title, required this.at});

  final String kind;
  final String title;
  final DateTime at;

  factory RecentActivity.fromJson(Map<String, dynamic> j) =>
      RecentActivity(kind: j['kind'] as String, title: j['title'] as String, at: DateTime.parse(j['at'] as String));
}

/// WorkspaceSummary / WorkspaceOverview – số liệu của module chưa xong thì để trống.
class WorkspaceOverview {
  const WorkspaceOverview({
    required this.id,
    required this.teamName,
    required this.courseCode,
    required this.myRole,
    this.projectName,
    this.previousWorkspaceId,
    this.openQuestionCount = 0,
    this.upcomingMeetingCount = 0,
    this.mentorSessionsDone = 0,
    this.expectedMentorSessions = 3,
    this.people = const [],
    this.recentActivity = const [],
  });

  final String id;
  final String teamName;
  final String courseCode;
  final WorkspaceRole? myRole;
  final String? projectName;
  final String? previousWorkspaceId;
  final int openQuestionCount;
  final int upcomingMeetingCount;
  final int mentorSessionsDone;
  final int expectedMentorSessions;
  final List<PersonRef> people;
  final List<RecentActivity> recentActivity;

  /// Sinh viên của nhóm tạo / sửa việc, tải file (quy tắc 23, 24); cố vấn chỉ xem và bình luận.
  bool get canEdit => myRole == WorkspaceRole.leader || myRole == WorkspaceRole.member;

  factory WorkspaceOverview.fromJson(Map<String, dynamic> j) => WorkspaceOverview(
        id: j['id'] as String,
        teamName: j['teamName'] as String? ?? '',
        courseCode: j['courseCode'] as String? ?? 'EXE101',
        myRole: _role(j['myRole']),
        projectName: j['projectName'] as String?,
        previousWorkspaceId: j['previousWorkspaceId'] as String?,
        openQuestionCount: j['openQuestionCount'] as int? ?? 0,
        upcomingMeetingCount: (j['upcomingMeetings'] as List?)?.length ?? (j['nextMeetingAt'] == null ? 0 : 1),
        mentorSessionsDone: j['mentorSessionsDone'] as int? ?? 0,
        expectedMentorSessions: j['expectedMentorSessions'] as int? ?? 3,
        people: [for (final p in (j['people'] as List? ?? const [])) PersonRef.fromJson(Map<String, dynamic>.from(p as Map))],
        recentActivity: [
          for (final a in (j['recentActivity'] as List? ?? const [])) RecentActivity.fromJson(Map<String, dynamic>.from(a as Map)),
        ],
      );
}

enum TaskStatus {
  todo('TODO', 'Cần làm'),
  doing('DOING', 'Đang làm'),
  done('DONE', 'Xong');

  const TaskStatus(this.code, this.label);

  final String code;
  final String label;

  static TaskStatus parse(Object? v) => values.firstWhere((s) => s.code == v, orElse: () => todo);
}

/// Trạng thái nộp của bài nộp; việc thường là null.
enum SubmissionStatus {
  notSubmitted('NOT_SUBMITTED', 'Chưa nộp'),
  pending('PENDING', 'Chờ duyệt'),
  accepted('ACCEPTED', 'Đạt'),
  needsRevision('NEEDS_REVISION', 'Cần sửa');

  const SubmissionStatus(this.code, this.label);

  final String code;
  final String label;

  static SubmissionStatus? parse(Object? v) => values.where((s) => s.code == v).firstOrNull;
}

class TaskCard {
  const TaskCard({
    required this.id,
    required this.workspaceId,
    required this.isDeliverable,
    required this.title,
    required this.status,
    required this.position,
    this.assignees = const [],
    this.dueAt,
    this.isOverdue = false,
    this.submissionStatus,
    this.commentCount = 0,
    this.isArchive = false,
  });

  final String id;
  final String workspaceId;
  final bool isDeliverable;
  final String title;
  final TaskStatus status;
  final int position;
  final List<PersonRef> assignees;
  final DateTime? dueAt;
  final bool isOverdue;
  final SubmissionStatus? submissionStatus;
  final int commentCount;
  final bool isArchive;

  TaskCard copyWith({TaskStatus? status}) => TaskCard(
        id: id,
        workspaceId: workspaceId,
        isDeliverable: isDeliverable,
        title: title,
        status: status ?? this.status,
        position: position,
        assignees: assignees,
        dueAt: dueAt,
        isOverdue: isOverdue,
        submissionStatus: submissionStatus,
        commentCount: commentCount,
        isArchive: isArchive,
      );

  factory TaskCard.fromJson(Map<String, dynamic> j) => TaskCard(
        id: j['id'] as String,
        workspaceId: j['workspaceId'] as String? ?? '',
        isDeliverable: j['kind'] == 'DELIVERABLE',
        title: j['title'] as String,
        status: TaskStatus.parse(j['status']),
        position: j['position'] as int? ?? 0,
        assignees: [for (final a in (j['assignees'] as List? ?? const [])) PersonRef.fromJson(Map<String, dynamic>.from(a as Map))],
        dueAt: _date(j['dueAt']),
        isOverdue: j['isOverdue'] as bool? ?? false,
        submissionStatus: SubmissionStatus.parse(j['submissionStatus']),
        commentCount: j['commentCount'] as int? ?? 0,
        isArchive: j['isArchive'] as bool? ?? false,
      );
}

class Comment {
  const Comment({required this.id, required this.author, required this.body, required this.createdAt, this.version});

  final String id;
  final PersonRef author;
  final String body;
  final DateTime createdAt;
  final int? version;

  factory Comment.fromJson(Map<String, dynamic> j) => Comment(
        id: j['id'] as String,
        author: PersonRef.fromJson(Map<String, dynamic>.from(j['author'] as Map)),
        body: j['body'] as String,
        createdAt: DateTime.parse(j['createdAt'] as String),
        version: j['version'] as int?,
      );
}

class Submission {
  const Submission({
    required this.id,
    required this.fileId,
    required this.fileName,
    required this.version,
    required this.submittedBy,
    required this.submittedAt,
    required this.isLate,
    required this.reviewStatus,
    this.reviewComment,
  });

  final String id;
  final String fileId;
  final String fileName;
  final int version;
  final PersonRef submittedBy;
  final DateTime submittedAt;
  final bool isLate;
  final SubmissionStatus reviewStatus;
  final String? reviewComment;

  factory Submission.fromJson(Map<String, dynamic> j) {
    final file = Map<String, dynamic>.from(j['file'] as Map);
    return Submission(
      id: j['id'] as String,
      fileId: file['id'] as String,
      fileName: file['name'] as String,
      version: j['version'] as int,
      submittedBy: PersonRef.fromJson(Map<String, dynamic>.from(j['submittedBy'] as Map)),
      submittedAt: DateTime.parse(j['submittedAt'] as String),
      isLate: j['isLate'] as bool? ?? false,
      reviewStatus: SubmissionStatus.parse(j['reviewStatus']) ?? SubmissionStatus.pending,
      reviewComment: j['reviewComment'] as String?,
    );
  }
}

class TaskDetail {
  const TaskDetail({
    required this.card,
    required this.createdBy,
    this.description,
    this.fromMeeting = false,
    this.submissions = const [],
    this.comments = const [],
  });

  final TaskCard card;
  final PersonRef createdBy;
  final String? description;
  final bool fromMeeting;
  final List<Submission> submissions;
  final List<Comment> comments;

  factory TaskDetail.fromJson(Map<String, dynamic> j) => TaskDetail(
        card: TaskCard.fromJson(j),
        createdBy: PersonRef.fromJson(Map<String, dynamic>.from(j['createdBy'] as Map)),
        description: j['description'] as String?,
        fromMeeting: j['sourceMeetingId'] != null,
        submissions: [for (final s in (j['submissions'] as List? ?? const [])) Submission.fromJson(Map<String, dynamic>.from(s as Map))],
        comments: [for (final c in (j['comments'] as List? ?? const [])) Comment.fromJson(Map<String, dynamic>.from(c as Map))],
      );
}

class Folder {
  const Folder({required this.id, required this.name, required this.isArchive, required this.itemCount, this.parentId});

  final String id;
  final String name;
  final bool isArchive;
  final int itemCount;
  final String? parentId;

  factory Folder.fromJson(Map<String, dynamic> j) => Folder(
        id: j['id'] as String,
        name: j['name'] as String,
        isArchive: j['isArchive'] as bool? ?? false,
        itemCount: j['itemCount'] as int? ?? 0,
        parentId: j['parentId'] as String?,
      );
}

class ProjectFile {
  const ProjectFile({
    required this.id,
    required this.name,
    required this.currentVersion,
    required this.sizeBytes,
    required this.updatedByName,
    required this.updatedAt,
    this.isArchive = false,
  });

  final String id;
  final String name;
  final int currentVersion;
  final int sizeBytes;
  final String updatedByName;
  final DateTime updatedAt;
  final bool isArchive;

  factory ProjectFile.fromJson(Map<String, dynamic> j) => ProjectFile(
        id: j['id'] as String,
        name: j['name'] as String,
        currentVersion: j['currentVersion'] as int? ?? 1,
        sizeBytes: (j['sizeBytes'] as num?)?.toInt() ?? 0,
        updatedByName: j['updatedByName'] as String? ?? '',
        updatedAt: DateTime.parse(j['updatedAt'] as String),
        isArchive: j['isArchive'] as bool? ?? false,
      );
}

class FileVersion {
  const FileVersion({required this.id, required this.version, required this.sizeBytes, required this.uploadedByName, required this.createdAt});

  final String id;
  final int version;
  final int sizeBytes;
  final String uploadedByName;
  final DateTime createdAt;

  factory FileVersion.fromJson(Map<String, dynamic> j) => FileVersion(
        id: j['id'] as String,
        version: j['version'] as int,
        sizeBytes: (j['sizeBytes'] as num?)?.toInt() ?? 0,
        uploadedByName: j['uploadedByName'] as String? ?? '',
        createdAt: DateTime.parse(j['createdAt'] as String),
      );
}

class FileDetail {
  const FileDetail({required this.file, required this.versions, required this.comments});

  final ProjectFile file;
  final List<FileVersion> versions;
  final List<Comment> comments;

  factory FileDetail.fromJson(Map<String, dynamic> j) => FileDetail(
        file: ProjectFile.fromJson(j),
        versions: [for (final v in (j['versions'] as List? ?? const [])) FileVersion.fromJson(Map<String, dynamic>.from(v as Map))],
        comments: [for (final c in (j['comments'] as List? ?? const [])) Comment.fromJson(Map<String, dynamic>.from(c as Map))],
      );
}

class FolderContent {
  const FolderContent({this.folder, this.breadcrumb = const [], this.folders = const [], this.files = const [], this.canUpload = false});

  final Folder? folder;
  final List<Folder> breadcrumb;
  final List<Folder> folders;
  final List<ProjectFile> files;
  final bool canUpload;

  factory FolderContent.fromJson(Map<String, dynamic> j) => FolderContent(
        folder: j['folder'] == null ? null : Folder.fromJson(Map<String, dynamic>.from(j['folder'] as Map)),
        breadcrumb: [for (final f in (j['breadcrumb'] as List? ?? const [])) Folder.fromJson(Map<String, dynamic>.from(f as Map))],
        folders: [for (final f in (j['folders'] as List? ?? const [])) Folder.fromJson(Map<String, dynamic>.from(f as Map))],
        files: [for (final f in (j['files'] as List? ?? const [])) ProjectFile.fromJson(Map<String, dynamic>.from(f as Map))],
        canUpload: j['canUpload'] as bool? ?? false,
      );
}
