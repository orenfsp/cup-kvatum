namespace Otklik.Application.Security;

public static class StaffRoles
{
    public const string Operator = "Operator";
    public const string Expert = "Expert";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Operator, Expert, Administrator];
}
