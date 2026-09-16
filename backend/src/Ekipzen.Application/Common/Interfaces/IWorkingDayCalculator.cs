namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// İş-günü hesabı: hafta sonları ve resmi tatiller (Holidays tablosu) hariç gün sayısı.
/// Infrastructure'da dbo.fn_WorkingDays SQL fonksiyonuyla implemente edilir.
/// </summary>
public interface IWorkingDayCalculator
{
    Task<int> GetWorkingDaysAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken);
}
