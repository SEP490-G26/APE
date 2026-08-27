namespace Application.Common;

public static class AISubjectDomainMapper
{
    public const string C = "C";
    public const string JavaOop = "JAVA_OOP";
    public const string DsaJava = "DSA_JAVA";

    public static string NormalizeSubjectOrCourseCode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "" => string.Empty,
            "PRF192" => C,
            "PRO192" => JavaOop,
            "CSD201" => DsaJava,
            "JAVA" => JavaOop,
            "DSA" => DsaJava,
            _ => normalized
        };
    }

    public static string ResolveFromCourseCodeOrFallback(string? courseCode, string? fallback = null)
    {
        var resolved = NormalizeSubjectOrCourseCode(courseCode);
        if (!string.IsNullOrWhiteSpace(resolved) &&
            (resolved == C || resolved == JavaOop || resolved == DsaJava))
        {
            return resolved;
        }

        return NormalizeSubjectOrCourseCode(fallback);
    }
}
