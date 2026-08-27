namespace BE_IntegrationTests.Infrastructure;

public sealed record TestIdentity(
    string UserId,
    string Role,
    string Email,
    string FullName)
{
    public static readonly TestIdentity Admin =
        new(
            "64b0000000000000000000a1",
            "Admin",
            "integration-admin@ape.local",
            "Integration Admin");

    public static readonly TestIdentity StudentA =
        new(
            "64b0000000000000000000a2",
            "Student",
            "integration-student-a@ape.local",
            "Integration Student A");

    public static readonly TestIdentity StudentB =
        new(
            "64b0000000000000000000a3",
            "Student",
            "integration-student-b@ape.local",
            "Integration Student B");
}
