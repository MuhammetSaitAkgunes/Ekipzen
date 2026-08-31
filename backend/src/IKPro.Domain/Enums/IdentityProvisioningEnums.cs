namespace IKPro.Domain.Enums;

public enum IdentityProvisioningOperationType
{
    EmployeeLogin,
    GrantManagerRole,
    RevokeManagerRole,
}

public enum IdentityProvisioningStatus
{
    Pending,
    Completed,
    Failed,
}
