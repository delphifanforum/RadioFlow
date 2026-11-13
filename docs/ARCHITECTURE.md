# Mimari

```
RadioFlow.App (WPF, MVVM)  ──►  RadioFlow.Infrastructure  ──►  RadioFlow.Core
  Views / ViewModels              LibVLC, HTTP, SQLite,          Modeller, arayüzler
  Shell, Tray, SMTC, Tema         Serilog, Registry              (bağımlılıksız)
```

- **ViewModel'ler** yalnızca `RadioFlow.Core` arayüzlerini kullanır (`IRadioPlayerService`, `ILibraryService`,
  `IRadioCatalogService`, `ISettingsService`, `ILogoCache`). Doğrudan SQL, HTTP veya LibVLC çağrısı yapmaz.
- **Tek oynatıcı:** `IRadioPlayerService` DI'da singleton'dır. Widget, panel, tepsi ve SMTC aynı `PlayerViewModel` örneğini kullanır.
  Pencere geçişleri yalnızca Show/Hide yapar, bu yüzden ses kesilmez.
- **RadioPlayerService ↔ IAudioEngine:** Durum makinesi, yeniden bağlanma, iptal ve yarış koruması platformdan bağımsız serviste durur.
  LibVLC yalnızca `IAudioEngine` arkasındadır. Testler `FakeAudioEngine` ile gerçek yayın açmadan koşar.
  - Her Play denemesine bir *nesil (generation)* numarası verilir. Eski medyadan gecikmeyle gelen olaylar yok sayılır.
  - Motor komutları `SemaphoreSlim` ile sıraya alınır. Her istasyon değişimi yeni bir `PlaybackSession` (CTS) açar ve eskisini iptal eder.
  - LibVLC geri çağrılarında LibVLC çağrılmaz. Olaylar sıralı bir `Channel` üzerinden ayrı görevde iletilir (polling yok).
- **Olay güdümlü:** Uygulamada zamanlayıcı veya polling döngüsü yoktur. Tema `SystemEvents.UserPreferenceChanged`,
  ekran değişimi `DisplaySettingsChanged`, tek örnek ise `RegisterWaitForSingleObject` ile takip edilir.
- **Kalıcılık:** `%LOCALAPPDATA%\RadioFlow\radioflow.db` (WAL), şema sürümü `PRAGMA user_version`, tüm SQL parametrelidir.
- **Ağ:** `IHttpClientFactory` adlı istemcileri, `SocketsHttpHandler` (bağlantı havuzu ömrü 5 dk, en fazla 5 yönlendirme)
  ve istek başına zaman aşımı kullanılır.
