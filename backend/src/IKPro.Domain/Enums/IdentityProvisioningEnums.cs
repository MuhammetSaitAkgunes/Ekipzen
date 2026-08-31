namespace IKPro.Domain.Enums;

public enum IdentityProvisioningOperationType
{
    EmployeeLogin,
    GrantManagerRole,
}

public enum IdentityProvisioningStatus
{
    Pending,
    Completed,
    Failed,
}
