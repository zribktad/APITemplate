namespace APITemplate.Application.Common.Security;

public interface IRolePermissionMap
{
    IReadOnlySet<string> GetPermissions(string role);
    bool HasPermission(string role, string permission);
}
