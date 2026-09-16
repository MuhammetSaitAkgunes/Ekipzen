using System.Text;
using System.Text.RegularExpressions;

namespace Ekipzen.Application.Features.Tenancy;

/// <summary>
/// Şirket adından URL/alt-alan dostu slug türetir: Türkçe harfleri transliterasyonla
/// ASCII'ye çevirir, kalan ASCII-dışı karakterleri tireye indirir. Saf fonksiyon
/// (self-servis kayıtta kullanılır); sonuç <c>[a-z0-9-]+</c>, ≤64, boşsa "sirket".
/// </summary>
public static class TenantSlug
{
    private const int MaxLength = 64;

    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ş'] = 's', ['Ş'] = 's', ['ı'] = 'i', ['İ'] = 'i', ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ü'] = 'u', ['Ü'] = 'u', ['ö'] = 'o', ['Ö'] = 'o', ['ç'] = 'c', ['Ç'] = 'c',
    };

    public static string From(string? companyName)
    {
        var source = companyName ?? string.Empty;
        var sb = new StringBuilder(source.Length);
        foreach (var ch in source)
        {
            var c = TurkishMap.TryGetValue(ch, out var mapped) ? mapped : char.ToLowerInvariant(ch);
            sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '-');
        }

        var slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (slug.Length > MaxLength) slug = slug[..MaxLength].Trim('-');
        return string.IsNullOrEmpty(slug) ? "sirket" : slug;
    }
}
