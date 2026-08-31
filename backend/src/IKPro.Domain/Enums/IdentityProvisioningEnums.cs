namespace IKPro.Domain.Enums;

public enum IdentityProvisioningOperationType
{
    EmployeeLogin,
    GrantManagerRole,
    RevokeManagerRole,
    EnableEmployeeAccess,
    DisableEmployeeAccess,
}

public enum IdentityProvisioningStatus
{
    Pending,
    Completed,
    Failed,
}
