namespace Notification.Worker.Notifications;

public static class AnnouncementAudience
{
    public const string AllStudents = "ALL_STUDENTS";
    public const string StudentsWithoutTeam = "STUDENTS_WITHOUT_TEAM";
    public const string AllLecturers = "ALL_LECTURERS";
    public const string AllMentors = "ALL_MENTORS";
    public const string TeamsWithoutMentor = "TEAMS_WITHOUT_MENTOR";
    public const string Everyone = "EVERYONE";

    public static readonly HashSet<string> All =
    [
        AllStudents, StudentsWithoutTeam, AllLecturers, AllMentors, TeamsWithoutMentor, Everyone
    ];
}

public sealed record AnnouncementRequest(
    string Audience,
    string Title,
    string Content,
    string? CourseCode = null,
    bool SendEmail = true);

public sealed record AnnouncementResponse(int RecipientCount);
