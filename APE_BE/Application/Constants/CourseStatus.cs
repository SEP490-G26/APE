namespace Application.Constants;

public static class CourseStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Deleted = "Deleted";

    public static readonly HashSet<string> All =
    [
        Active,
        Inactive,
        Deleted
    ];

    public static readonly HashSet<string> Editable =
    [
        Active,
        Inactive
    ];
}