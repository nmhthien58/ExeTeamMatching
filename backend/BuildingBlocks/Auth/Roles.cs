namespace BuildingBlocks.Auth;

/// <summary>Tên vai trò trong claim <c>roles</c> của JWT (docs/contracts/README.md).</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Mentor = "Mentor";
    public const string Admin = "Admin";

    /// <summary>Token dịch vụ dùng cho endpoint <c>/internal/*</c> (Job → Team, Team → Academic…).</summary>
    public const string Service = "Service";
}

/// <summary>Tên claim do Identity phát.</summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Roles = "roles";
    public const string StudentId = "student_id";
    public const string LecturerId = "lecturer_id";
    public const string MentorId = "mentor_id";
}

/// <summary>Tên policy dùng chung, đăng ký trong <see cref="AuthExtensions.AddJwtAuth"/>.</summary>
public static class Policies
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Mentor = "Mentor";
    public const string Admin = "Admin";

    /// <summary>Giảng viên hoặc mentor (cố vấn của workspace).</summary>
    public const string Advisor = "Advisor";

    /// <summary>Chỉ token dịch vụ – cho endpoint nội bộ.</summary>
    public const string Internal = "Internal";
}
