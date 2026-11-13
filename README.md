# RadioFlow

Windows 11 için modern, hafif ve bağımsız çalışan bir **internet radyosu widget'ı**.
Masaüstünde duran kompakt bir mini oynatıcı ile istasyon arayıp keşfedebileceğiniz büyük bir panelden oluşur.
Tüm arayüz Türkçedir.

> **Durum:** v0.1.0 (MVP + Faz 2/3'ün büyük bölümü). Neyin test edildiği ve neyin edilmediği [ROADMAP.md](ROADMAP.md)
> ve [docs/VERIFICATION.md](docs/VERIFICATION.md) içinde açıkça listelenmiştir. WPF arayüzü henüz gerçek bir Windows
> makinesinde derlenip çalıştırılmamıştır. İlk iş olarak `.\build.ps1` çalıştırın.

## Özellikler

- **Mini widget** (360×150): logo, istasyon adı, şarkı bilgisi (ICY), oynat/duraklat, durdur, önceki/sonraki favori,
  ses ve sessiz, favori, büyük pencereye geçiş, konum sabitleme, her zaman üstte, sağ tık menüsü ve 3 boyut seçeneği.
- **Büyük panel** (1050×700): Ana Sayfa, Tüm Radyolar, Favoriler, Son Dinlenenler, Ülkelere Göre ve Türlere Göre sayfaları,
  ayrıca Ayarlar. Üstte arama, altta sabit "Now Playing" çubuğu bulunur.
- **Radio Browser API**: Türkçe karakterlerle arama, ülke ve tür filtresi, popüler istasyonlar, sunucu keşfi,
  yedek sunucuya geçiş ve zaman aşımı. Çevrimdışıyken **yerel önbellekten** listeleme yapılır.
- **Tek merkezi oynatıcı (LibVLC)**: HTTP/HTTPS, MP3/AAC/OGG yayınları ile M3U/PLS çözümleme.
  Bağlantı koparsa artan gecikmelerle sınırlı sayıda yeniden bağlanılır. İstasyon değişince eski işlemler iptal edilir.
- **SQLite**: favoriler (sıralanabilir), dinleme geçmişi, istasyon önbelleği ve ayarlar.
  Veriler `%LOCALAPPDATA%\RadioFlow` altında tutulur.
- **Windows entegrasyonu**: sistem tepsisi, medya tuşları ve kilit ekranı bilgisi (SMTC), başlangıçta çalıştırma
  (EXE için HKCU Run, MSIX için StartupTask) ve tek örnek koruması.
- **Görünüm**: koyu, açık ve sistem temaları. Windows 11 Acrylic/Mica arka planı (DWM) desteklenmeyen sistemlerde düz renge döner.
  Yuvarlatılmış köşeler ve Per-Monitor V2 DPI desteği vardır.
- **Güvenlik**: yalnızca http/https adresleri kabul edilir. `file:`, `javascript:` ve benzeri şemalar reddedilir.
  Katalog verisinde yerel ağ adresleri engellenir. Logolar boyut ve imza denetimiyle indirilir.
  Yönetici yetkisi istenmez.
- **Loglama**: Serilog ile `%LOCALAPPDATA%\RadioFlow\Logs` altında günlük dönen dosyalara yazılır
  (dosya başına 10 MB, en fazla 14 dosya).

## Kullanılan teknolojiler

| Bileşen | Sürüm | Not |
|---|---|---|
| .NET / WPF | 10.0 (`net10.0-windows10.0.19041.0`) | UI |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM kaynak üreteçleri |
| Microsoft.Extensions.Hosting / Http | 10.0.15 / 10.0.14 | DI, IHttpClientFactory |
| LibVLCSharp | 3.10.2 | Ses motoru (yalnızca ses; `LibVLCSharp.WPF` video görünümü gerekmediği için **kullanılmadı**) |
| VideoLAN.LibVLC.Windows | 3.0.24 | Yerel LibVLC dosyaları (yalnızca x64 kopyalanır) |
| Microsoft.Data.Sqlite | 10.0.15 | Veritabanı |
| Serilog (+Sinks.File, +Extensions.Hosting) | 4.4.0 / 7.0.0 / 10.0.0 | Loglama |
| xunit.v3 | 4.0.3 | Testler (Microsoft Testing Platform) |

Paket sürümleri 13.11.2026'da nuget.org üzerinden doğrulandı ve `Directory.Packages.props` içinde merkezi olarak tutulur.
Tema için hazır bir kütüphane (WPF UI vb.) yerine kendi stillerimiz ve doğrudan DWM API'si kullanıldı.
Bu sayede bağımlılık sayısı düşük kaldı.

## Kurulum ve derleme

Gereksinimler: Windows 10 19041+ (önerilen: Windows 11 22H2+), [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).

```powershell
cd E:\RadioFlow
.\build.ps1                 # ortam kontrolü + restore + Release build + test + win-x64 publish + LibVLC doğrulaması
```

Elle çalıştırmak için:

```powershell
dotnet restore RadioFlow.slnx
dotnet build RadioFlow.slnx -c Release
dotnet test --solution RadioFlow.slnx -c Release
dotnet publish .\src\RadioFlow.App\RadioFlow.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64
```

> `global.json`, `dotnet test` komutunu **Microsoft Testing Platform** kipine alır (xUnit v3 için gerekli).
> **Tek dosya yayınlama kullanılmaz.** LibVLC'nin `plugins` klasörü tek dosyaya gömülemez.
> Publish çıktısı `RadioFlow.exe` ile `libvlc\win-x64\` klasörünü birlikte içermelidir. `build.ps1` bunu denetler.

## Çalıştırma

```powershell
dotnet run --project .\src\RadioFlow.App
# veya
.\artifacts\publish\win-x64\RadioFlow.exe
```

Uygulama açıldığında ekranın sağ alt köşesinde mini widget görünür. İkinci kez başlatılırsa yeni örnek açılmaz, mevcut widget öne gelir.

## Klasör yapısı

```
RadioFlow.slnx               Çözüm (src + tests)
Directory.Build.props        Ortak derleme ayarları ve sürüm
Directory.Packages.props     Merkezi paket sürümleri
global.json                  SDK + test platformu
build.ps1                    Derleme/test/publish betiği
src/
  RadioFlow.Core/            Modeller, arayüzler, enum'lar, sabitler (bağımlılıksız, net10.0)
  RadioFlow.Infrastructure/  LibVLC, Radio Browser, SQLite, ayarlar, logo önbelleği, loglama, başlangıç kaydı (net10.0)
    Audio/                   RadioPlayerService (durum makinesi), LibVlcAudioEngine, PlaylistResolver
    RadioBrowser/            API istemcisi, DTO'lar, eşleyici, sunucu keşfi
    Persistence/             Bağlantı fabrikası, şema geçişleri, depolar
    Services/                Ayarlar, kütüphane (favori/geçmiş), dinleme takibi, logo önbelleği
    Security/                URL doğrulayıcı
    WindowsIntegration/      HKCU Run başlangıç kaydı
    Logging/                 Serilog yapılandırması
  RadioFlow.App/             WPF (net10.0-windows10.0.19041.0)
    Views/                   MiniWidgetWindow, MainWindow, SettingsWindow
    ViewModels/              PlayerViewModel (paylaşılan), Widget, Main, Settings, sayfa VM'leri
    Services/                Shell, tema, DWM arka planı, tepsi, SMTC, başlangıç seçici, tek örnek
    Controls/ Converters/ Themes/ Resources/ Assets/ Interop/
tests/RadioFlow.Tests/       xUnit v3 testleri (gerçek internet veya yayın gerektirmez)
packaging/                   MSIX manifest şablonu, görseller, build-msix.ps1
docs/                        Mimari, doğrulama raporu, manuel test listesi
```

## Radyo ekleme

- **Katalogdan:** Büyük panelde arayın veya Ülkelere/Türlere Göre sayfasından seçin. Karttaki ▶ ile oynatın, ☆ ile favorilere ekleyin.
- **Kendi yayınınız:** Büyük panelde **İstasyon Ekle** düğmesine basın. Ad ve `http(s)://` yayın adresi girin
  (`.m3u` / `.pls` de olur). Logo ve tür alanları isteğe bağlıdır. Özel istasyonlar otomatik olarak favorilere eklenir.
  Yerel ağdaki adreslere (ör. `http://192.168.1.20:8000`) yalnızca elle eklenen istasyonlarda izin verilir.
- **Favori sırası:** Favoriler sayfasındaki ok düğmeleriyle değiştirilir. Widget'taki önceki/sonraki düğmeleri bu sırayı izler.

## Ayarlar

Tema (Koyu/Açık/Sistem), widget arka plan yoğunluğu, widget boyutu, her zaman üstte, büyük pencere boyutu,
Windows başlangıcında çalıştırma, açılışta widget gösterme, açılışta son radyoyu oynatma, açılış ses seviyesi,
yeniden bağlanma deneme sayısı ve gecikmesi, tepsiye küçültme, bildirimler, dil ve ana sayfa ülkesi.
Ayarlar SQLite `Settings` tablosunda `app.<Ad>` anahtarlarıyla saklanır. Bozuk bir değer yalnızca o ayarı varsayılana döndürür.

**Başlangıçta çalıştırma** hiçbir zaman kendiliğinden açılmaz:
- Bağımsız EXE için `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\RadioFlow` = `"…\RadioFlow.exe" --startup` yazılır.
- MSIX için `Windows.ApplicationModel.StartupTask` (`RadioFlowStartup`) kullanılır. Kullanıcı bunu Görev Yöneticisi'nden de kapatabilir.

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| Ses gelmiyor, "Yayına bağlanılamadı" | İstasyon kapalı olabilir. Başka bir istasyon deneyin. Log: `%LOCALAPPDATA%\RadioFlow\Logs` |
| Uygulama açılmıyor / LibVLC hatası | Publish klasöründe `libvlc\win-x64\libvlc.dll` ve `plugins` olmalı. `.\build.ps1` ile yeniden yayınlayın |
| Katalog yüklenmiyor | İnternet/proxy ayarlarını kontrol edin. Önceden görüntülenen istasyonlar önbellekten listelenir |
| Arka plan bulanık değil | Acrylic/Mica, Windows 11 22H2 (derleme 22621) ve üzeri gerektirir. Eski sürümlerde düz renk kullanılır. Ayrıca Windows'ta "Saydamlık efektleri" açık olmalı |
| Widget görünmüyor | Tepsi simgesine tıklayın. Ekran dışında kalırsa açılışta otomatik olarak görünür alana alınır |
| Medya tuşları çalışmıyor | Başka bir uygulama medya oturumunu tutuyor olabilir. Loglarda "Sistem medya kontrolleri" satırına bakın |
| Ayarları sıfırlamak | Uygulamayı kapatıp `%LOCALAPPDATA%\RadioFlow\radioflow.db` dosyasını silin (favoriler de silinir) |

## Veri ve gizlilik

RadioFlow hesap oluşturmaz ve telemetri göndermez. Ağ istekleri yalnızca Radio Browser API'sine
(arama ve Radio Browser'ın önerdiği "tıklama" bildirimi), istasyon logolarına ve seçtiğiniz yayın adresine gider.
İstasyon verileri topluluk tarafından yönetilen [Radio Browser](https://www.radio-browser.info) kataloğundan gelir.
Yayın içerikleri üçüncü taraflara aittir.
