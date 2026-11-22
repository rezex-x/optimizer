namespace Tweaker.Services;

public static class OwnerNames
{
    public static string Display(string? account, bool isCurrentUser)
    {
        if (isCurrentUser) return "User";
        if (string.IsNullOrWhiteSpace(account)) return "—";
        int slash = account.LastIndexOf('\\');
        return slash >= 0 ? account[(slash + 1)..] : account;
    }
}
