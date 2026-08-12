namespace IKPro.Application.Common.Exceptions;

/// <summary>Kimlik doğrulama başarısız (401). Mesaj kullanıcıya gösterilebilir.</summary>
public class UnauthorizedException(string message) : Exception(message);

/// <summary>Yetki kapsamı dışı erişim (403).</summary>
public class ForbiddenAccessException(string message = "Bu işlem için yetkiniz yok.") : Exception(message);

/// <summary>Kayıt bulunamadı (404).</summary>
public class NotFoundException(string name, object key)
    : Exception($"{name} bulunamadı (anahtar: {key}).");

/// <summary>Çakışan kayıt / iş kuralı ihlali (409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>
/// Kiracı erişime kapalı (403) — dondurulmuş, siliniyor ya da hiç yok.
/// 401 DEĞİL: kimlik bilgileri doğru, engelleyen şey kiracının durumu. 401
/// "parolan yanlış" anlamına gelir ve kullanıcıyı yanlış yönlendirir.
/// </summary>
public class TenantInaccessibleException(string message) : Exception(message);

/// <summary>
/// Kiracının veritabanı şeması, uygulamanın beklediğinin GERİSİNDE (503).
///
/// 403 DEĞİL: kimlik ve yetki doğru, kiracı da kapalı değil — eksik olan
/// dağıtımın bir adımı. 503 "şu anda değil, birazdan" der ve istemciye yeniden
/// denemesi gerektiğini söyler; 403 ise "senin hakkın yok" diyerek kullanıcıyı
/// yanlış yönlendirirdi.
///
/// <b>Neden istek reddediliyor da eski şemayla çalışılmıyor:</b> uygulama kodu
/// artık var olmayan sütunları sorguluyor. En iyi ihtimalle her istek anlaşılmaz
/// bir SQL hatasıyla düşer; daha kötüsü, kısmen çalışan bir yazma yolu veriyi
/// tutarsız bırakır. Açıkça reddetmek, sessizce bozmaktan iyidir.
/// </summary>
public class TenantSchemaOutdatedException(string message) : Exception(message);
