using FluentAssertions;
using IKPro.Application.Features.Employees;
using IKPro.Application.Features.Employees.Offboarding;
using System.Net;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration;

internal static class OffboardingTestHelper
{
    public static async Task<EmployeeOffboardingCaseDto> CompleteAsync(
        HttpClient client, int employeeId, DateOnly terminationDate)
    {
        var draftResponse = await client.PutAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding-draft", new
            {
                terminationDate,
                exitCode = "TEST-03",
                exitReason = "Entegrasyon testi",
                terminationReason = "employee-resignation",
                noticeHandling = "not-applicable",
                unusedLeaveDays = 0m,
                unusedLeavePayment = 0m,
                severancePayment = 0m,
                noticePayment = 0m,
                settlementReference = "Test uzman hesap belgesi",
                settlementNotes = "Otomatik mevzuat hesabı değildir.",
            });
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await draftResponse.Content
            .ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;

        var completeResponse = await client.PostAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding/{draft.Id}/complete",
            new { expectedVersion = draft.Version });
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await completeResponse.Content
            .ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
    }
}