using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportTemplateTests
{
    [Fact]
    public void Olustur_BasliklariVeOrnekSatiriIcerir()
    {
        var bytes = EmployeeImportTemplate.Olustur();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        // 1. satır başlık, 2. satır örnek.
        var basliklar = EmployeeImportTemplate.SutunBasliklari
            .Select((_, i) => sheet.Cell(1, i + 1).GetString())
            .ToArray();

        basliklar.Should().Equal(EmployeeImportTemplate.SutunBasliklari);
        sheet.Cell(2, 1).GetString().Should().NotBeEmpty("örnek satır kullanıcıya biçimi gösterir");
    }

    [Fact]
    public void ZorunluSutunlar_BasliklarinAltKumesidir()
    {
        // Şablonda olmayan bir sütunu zorunlu saymak, doğrulamayı imkânsız kılar.
        EmployeeImportTemplate.SutunBasliklari
            .Should().Contain(EmployeeImportTemplate.ZorunluSutunlar);
    }
}
