# 09 — Yeni Özellik Ekleme (Adım Adım)

Bu rehber, önceki tüm rehberleri birleştirir: **uçtan uca** yeni bir özelliği
veritabanından ekrana kadar ekleriz. Örnek olarak basit bir **"Duyurular"**
modülü kullanacağız (kiracı içi duyuru listeleme + oluşturma).

> Bu bir **şablon**dur — kendi özelliğini eklerken aynı adımları izle. Kod parçaları
> projenin gerçek desenleriyle ([02](02-mimari-clean-architecture.md)) birebir uyumludur.

## Adım 0 — Dalı Aç ve Testi Düşün

```bash
git checkout -b feature/duyurular
```
Önce "ne davranış istiyorum?" sorusunu bir testle yazacağız (TDD). Ama önce iskeleti kuralım.

## Adım 1 — Domain: Varlık

`backend/src/Ekipzen.Domain/Entities/Announcements/Announcement.cs`:

```csharp
namespace Ekipzen.Domain.Entities.Announcements;

// AuditableEntity → BaseEntity → ITenantScoped: TenantId ve global filtre OTOMATİK gelir.
public class Announcement : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
}
```

> Çok kiracılılık için ekstra bir şey yapmana gerek yok — `AuditableEntity`'den
> türemek `TenantId` + otomatik filtre + audit damgası demektir.

## Adım 2 — DbContext + Migration

`AppDbContext`'e DbSet ekle (ve `IApplicationDbContext` arayüzüne):

```csharp
public DbSet<Announcement> Announcements => Set<Announcement>();
```

Migration üret ve uygula:

```bash
cd backend
dotnet ef migrations add AddAnnouncements --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.API
dotnet ef database update --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.API
```

## Adım 3 — Application: Sorgu (okuma)

`backend/src/Ekipzen.Application/Features/Announcements/GetAnnouncementsQuery.cs`:

```csharp
public sealed record AnnouncementDto(int Id, string Title, string Body, DateTime PublishedAtUtc);

public sealed record GetAnnouncementsQuery : IRequest<IReadOnlyList<AnnouncementDto>>;

public sealed class GetAnnouncementsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAnnouncementsQuery, IReadOnlyList<AnnouncementDto>>
{
    public async Task<IReadOnlyList<AnnouncementDto>> Handle(GetAnnouncementsQuery request, CancellationToken ct)
        => await context.Announcements               // global filtre: yalnız bu kiracının duyuruları
            .OrderByDescending(a => a.PublishedAtUtc)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.PublishedAtUtc))
            .ToListAsync(ct);
}
```

## Adım 4 — Application: Komut (yazma) + doğrulama

`.../Announcements/CreateAnnouncementCommand.cs`:

```csharp
public sealed record CreateAnnouncementCommand(string Title, string Body) : IRequest<int>;

public sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CreateAnnouncementCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateAnnouncementCommand, int>
{
    public async Task<int> Handle(CreateAnnouncementCommand request, CancellationToken ct)
    {
        var entity = new Announcement
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            PublishedAtUtc = DateTime.UtcNow,
            // TenantId'yi SET ETME — interceptor kaydetme anında otomatik damgalar.
        };
        context.Announcements.Add(entity);
        await context.SaveChangesAsync(ct);
        return entity.Id;
    }
}
```

> Validator'ı elle çağırmıyoruz; `ValidationBehavior` pipeline'ı otomatik çalıştırır
> ([03](03-backend-derinlemesine.md)).

## Adım 5 — API: Controller

`backend/src/Ekipzen.API/Controllers/AnnouncementsController.cs`:

```csharp
[ApiController]
[Route("api/announcements")]
public sealed class AnnouncementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize] // her rol kendi kiracısının duyurularını görür
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> Get(CancellationToken ct)
        => Ok(await sender.Send(new GetAnnouncementsQuery(), ct));

    [HttpPost]
    [Authorize(Policy = Policies.HrAdminOnly)] // yalnız hr-admin oluşturur
    public async Task<ActionResult<int>> Create(CreateAnnouncementCommand command, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await sender.Send(command, ct));
}
```

## Adım 6 — Entegrasyon Testi (önce yazılmalıydı!)

`backend/tests/Ekipzen.Tests.Integration/Announcements/AnnouncementsTests.cs`:

```csharp
[Collection(ApiCollection.Name)]
public sealed class AnnouncementsTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Create_ThenList_ReturnsOwnAnnouncement()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var create = await admin.PostAsJsonAsync("/api/announcements", new { title = "Bayram", body = "Kapalıyız" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await GetAsync<List<AnnouncementDto>>(admin, "/api/announcements");
        list.Should().Contain(a => a.Title == "Bayram");
    }
}
```

Çalıştır: `dotnet test --filter "FullyQualifiedName~Announcements"` → yeşil olmalı.

## Adım 7 — Frontend: Tip + Query + Sayfa

```bash
# 1) Backend çalışırken tipleri üret (AnnouncementDto artık schema.d.ts'te)
cd frontend && npm run gen:api
```

`frontend/src/features/announcements/queries.ts`:

```ts
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export const useAnnouncements = () =>
  useQuery({ queryKey: ["announcements"], queryFn: () => apiFetch<Announcement[]>("/announcements") });

export const useCreateAnnouncement = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { title: string; body: string }) =>
      apiFetch("/announcements", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["announcements"] }),
  });
};
```

Sonra bir sayfa bileşeni yaz (`AnnouncementsPage.tsx`), `routes.tsx`'e route ekle
(uygun `roles` ile) ve `features/shared/PageState.tsx` ile yükleniyor/hata durumlarını göster.

## Adım 8 — Doğrula, Commit, Birleştir

```bash
cd backend && dotnet test          # hepsi yeşil
cd frontend && npm test -- --run && npm run build

git add -A
git commit -m "feat(announcements): kiracı-kapsamlı duyuru listeleme + oluşturma"
```

## Kontrol Listesi (her yeni özellik için)

- [ ] Varlık `BaseEntity`'den türüyor (çok kiracılılık otomatik)
- [ ] DbSet eklendi (`AppDbContext` **ve** `IApplicationDbContext`)
- [ ] Migration üretildi ve uygulandı
- [ ] Komut/sorgu + validator + handler yazıldı
- [ ] Controller ince (yalnız `sender.Send`) ve doğru `[Authorize]` policy'si var
- [ ] Entegrasyon testi (mümkünse izolasyon dahil) yeşil
- [ ] `npm run gen:api` ile frontend tipleri güncellendi
- [ ] Frontend query katmanı + sayfa + route + rol matrisi
- [ ] Backend + frontend testleri + build yeşil

## Sonraki Adım

Takıldığında → [10 — Sözlük & SSS](10-sozluk-ve-sss.md).
