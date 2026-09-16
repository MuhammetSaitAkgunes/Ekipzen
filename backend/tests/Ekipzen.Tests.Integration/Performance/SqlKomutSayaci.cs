using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;

namespace Ekipzen.Tests.Integration.Performance;

/// <summary>
/// EF Core'un çalıştırdığı SQL komutlarını sayar. DiagnosticListener'a abone olur;
/// uygulama testle AYNI süreçte koştuğu için (WebApplicationFactory) HTTP isteği
/// sırasında üretilen sorgular da sayılır.
///
/// Neden DI'a interceptor eklemiyoruz: fabrika tüm test sınıflarınca paylaşılan tek
/// host'tur; ikinci bir host kurmak yerine süreç içi tanılama akışına bağlanmak hem
/// üretim DI'ını hem de fabrikayı olduğu gibi bırakır.
/// </summary>
public sealed class SqlKomutSayaci : IObserver<DiagnosticListener>, IDisposable
{
    private readonly List<IDisposable> _abonelikler = [];
    private int _sayac;

    public SqlKomutSayaci() => _abonelikler.Add(DiagnosticListener.AllListeners.Subscribe(this));

    /// <summary>Sayacı sıfırlar ve o andan itibaren çalışan komutları saymaya başlar.</summary>
    public void Sifirla() => Interlocked.Exchange(ref _sayac, 0);

    public int Sayi => Volatile.Read(ref _sayac);

    /// <summary>Verilen işi koşar ve bu sırada çalışan SQL komutu sayısını döner.</summary>
    public async Task<int> OlcAsync(Func<Task> is_)
    {
        Sifirla();
        await is_();
        return Sayi;
    }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == DbLoggerCategory.Name)
        {
            _abonelikler.Add(listener.Subscribe(new KomutGozlemcisi(this)));
        }
    }

    void IObserver<DiagnosticListener>.OnCompleted() { }
    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    private sealed class KomutGozlemcisi(SqlKomutSayaci sahip) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> olay)
        {
            if (olay.Key == RelationalEventId.CommandExecuting.Name)
            {
                Interlocked.Increment(ref sahip._sayac);
            }
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    public void Dispose()
    {
        foreach (var abonelik in _abonelikler)
        {
            abonelik.Dispose();
        }

        _abonelikler.Clear();
    }
}
