# Doğrulama Raporu (v0.1.0, v0.2.0 eki ile)

Bu belge **neyin nasıl doğrulandığını** ve **neyin doğrulanmadığını** açıkça kayda geçirir.

## v0.2.0 eki — "RadioFlow 2.0" (bu teslimat)

- Değişen/eklenen tüm `.cs` dosyaları Roslyn ile sözdizimi açısından tarandı: **0 hata**.
- Değişen/eklenen tüm `.xaml` dosyaları XML iyi-biçimliliği açısından ayrıştırıldı: **0 hata**. Bu, yalnızca
  belgenin geçerli XML olduğunu kanıtlar; XAML'a özgü anlamsal hataları (yanlış özellik adı, eksik
  `TargetType`, vb.) yakalamaz — bunlar yalnızca gerçek WPF/XAML derleyicisiyle kesinleşir.
- `RadioFlow.Core` ve `RadioFlow.Infrastructure`, güncellenmiş `AppSettings.cs` dahil, gerçek kaynaklarla
  yeniden derlendi: **0 hata**.
- Tüm test paketi yeniden çalıştırıldı: **76/76 geçti** (74 mevcut + 2 yeni: oto-gizle ayarının kalıcılığı,
  opaklık aralığının %50–%100'e sıkıştırılması).
- Kullanıcının kendi makinesinde `E:\RadioFlow\artifacts\publish\win-x64\RadioFlow.exe` dosyasının mevcut
  olması, **v0.1.0'ın gerçek derleyiciyle başarıyla derlendiğini ve yayınlandığını** doğrular — bu, önceki
  "WPF Linux'ta derlenemedi" kısıtının artık yalnızca bu oturum (bu teslimat) için geçerli olduğu anlamına
  gelir; yeni/değişen WPF kodu (mini widget, yeni kontroller, ayarlar ekranı) henüz gerçek derleyiciden
  geçmedi. Kullanıcının `.\build.ps1` çalıştırması bunu kesinleştirecektir.
- **Doğrulanmayan:** yeni görünüm modlarının gerçek pencerede doğru boyutlanması, oto-gizle animasyonunun
  gerçek fare olaylarıyla davranışı, favori hızlı değiştirme popup'ının gerçek ekranda konumlanması,
  `LiveIndicator` animasyonlarının gerçek GPU/WPF render hattında çalışması. Bunlar yalnızca gerçek
  Windows 11 + WPF derleyicisiyle doğrulanabilir; bkz. [MANUAL_TEST.md](MANUAL_TEST.md) §6b.
- **Bilinçli olarak eklenmedi:** ses görselleştirici (gerçek PCM seviyesi gerektirir, bkz. CHANGELOG.md).

## Ortam kısıtları

- Kod, bulut Linux sandbox'ta (Ubuntu 24.04, .NET SDK 10.0.115) yazıldı.
- Kullanıcının Windows makinesinde komut çalıştırma imkânı yoktu. Yalnızca dosya yazılabildi.
  Bu yüzden `dotnet --info`, Windows sürümü ve Git durumu hedef makinede kontrol **edilemedi**.
- Sandbox'tan `api.nuget.org` erişimi **engelliydi**. NuGet paketleri geri yüklenemedi.
- WPF projesi Linux'ta derlenemez (Microsoft.WindowsDesktop.App.Ref paketi NuGet'ten iner).

## Yapılan doğrulamalar

| Kontrol | Yöntem | Sonuç |
|---|---|---|
| Paket sürümleri | nuget.org sayfalarından tek tek doğrulandı (kararlı sürümler) | ✅ |
| `RadioFlow.Core` derlemesi | Gerçek .NET 10 SDK, paket gerekmez | ✅ 0 hata |
| `RadioFlow.Infrastructure` derlemesi | Gerçek kaynak dosyalar. Microsoft.Extensions.* için SDK'daki gerçek ASP.NET Core paylaşımlı çerçevesi kullanıldı. LibVLCSharp ve Serilog için API'yi taklit eden **yalnızca-derleme stub'ları** | ✅ 0 hata (yalnızca analiz uyarıları, giderildi) |
| Testlerin derlenmesi + çalıştırılması | Gerçek test kaynakları, minimal xUnit uyumlu çalıştırıcı. SQLite testleri, sistemdeki **gerçek libsqlite3** üzerinde Microsoft.Data.Sqlite uyumlu ince bir katmanla koşturuldu (gerçek SQL çalıştı) | ✅ **74/74 geçti**, 15 ardışık koşuda kararsızlık yok |
| WPF uygulaması C# sözdizimi | Roslyn ayrıştırıcı | ✅ 0 sözdizimi hatası |
| XAML dosyaları | XML iyi biçimlilik denetimi | ✅ |
| WPF uygulamasının derlenmesi (XAML derleyici, tür denetimi, kaynak üreteçleri) | — | ❌ **Yapılamadı** |
| Uygulamanın çalıştırılması, ses çalma, GUI | — | ❌ **Yapılamadı**. Bkz. MANUAL_TEST.md |
| Gerçek Radio Browser API çağrısı | — | ❌ Yapılamadı (sahte HTTP yanıtlarıyla test edildi) |
| Publish çıktısında LibVLC yerel dosyaları | `build.ps1` bunu denetler | ❌ Henüz çalıştırılmadı |
| Performans hedefleri (CPU/bellek, 7/24) | — | ❌ Ölçülmedi. Garanti verilmez |

Stub ve shim katmanları depoya **dahil edilmedi**. Yalnızca doğrulama içindi.
Gerçek derleme Windows'ta resmî NuGet paketleriyle yapılmalıdır.

## Test kapsamı (istenen 10 senaryo)

| # | Senaryo | Test |
|---|---|---|
| 1 | Favori ekleme | `FavoritesTests.ToggleFavorite_AddsStationToFavorites`, `AddFavorite_Twice_DoesNotDuplicate` |
| 2 | Favori kaldırma | `FavoritesTests.ToggleFavorite_Twice_RemovesStationFromFavorites` |
| 3 | Ayarların kaydedilmesi | `SettingsTests.Update_PersistsValuesToSettingsTable` |
| 4 | Ayarların tekrar yüklenmesi | `SettingsTests.Load_InNewInstance_RestoresSavedValues`, `Load_WithCorruptOrOutOfRangeValues_FallsBackPerKey` |
| 5 | Radyo API JSON dönüşümü | `RadioBrowserClientTests.Search_MapsJsonToStations_AndDropsUnsafeEntries`, `GetCountriesAndTags_MapJson` |
| 6 | Hatalı stream URL reddi | `StreamUrlValidatorTests.*`, `RadioPlayerServiceTests.Play_WithInvalidUrl_IsRejectedWithoutTouchingEngine` |
| 7 | Player state geçişleri | `RadioPlayerServiceTests.Play_Pause_Resume_Stop_FollowsExpectedStateTransitions` |
| 8 | Reconnect retry sınırı | `RadioPlayerServiceTests.Play_WhenStreamAlwaysFails_StopsAfterMaxAttemptsWithError`, `Stop_DuringReconnect_CancelsRetries` |
| 9 | İstasyon değişince önceki işlemin iptali | `RadioPlayerServiceTests.Play_NewStation_CancelsPendingConnectionOfPreviousStation`, `ConcurrentPlayCalls_LastOneWins` |
| 10 | Veritabanı ilk oluşturma | `DatabaseInitializerTests.*` |

Ek testler: Türkçe arama (önbellek + URL kodlama), M3U/PLS ayrıştırma ve güvensiz girdi eleme, ICY metadata ayrıştırma,
sunucu yedeklemesi, iptal, geri çekilme gecikmesi ve logo imza denetimi.
