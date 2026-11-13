# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) esaslıdır; sürümleme [SemVer](https://semver.org/lang/tr/).

## [0.2.0] — 2026-11-13 — "RadioFlow 2.0" görsel ve UX güncellemesi

Mevcut, kullanıcının makinesinde gerçek derleyiciyle derlenip çalıştırıldığı doğrulanan (bkz. 0.1.0 notu)
uygulama üzerine; oynatma, favoriler, ayarlar ve Windows entegrasyonu bozulmadan geliştirildi.

### Eklendi
- Mini widget için üç ayrı görünüm modu: Kompakt (yalnızca logo/ad/canlı gösterge/oynat), Normal (mevcut
  düzen) ve Genişletilmiş (daha büyük yazı tipleri ve alan); sağ tık menüsünden seçilir, ayarlarda kalıcıdır.
- Otomatik gizlenen kontroller: açıldığında widget'ta yalnızca logo, isim, canlı yayın göstergesi ve
  oynat/duraklat her zaman görünür; önceki/sonraki, durdur, favori, ses ve "büyük pencereyi aç" düğmeleri
  yalnızca fare widget üzerindeyken yumuşak bir solma animasyonuyla belirir. Varsayılan olarak kapalıdır
  (`AppSettings.AutoHideWidgetControls`); klavye odağı ve erişilebilirlik etkilenmez.
- Yeni `LiveIndicator` kontrolü: oynatırken nefes alır gibi yumuşak opaklık animasyonu (mor), bağlanırken/
  arabelleğe alırken dönen halka, duraklatıldığında sabit soluk nokta, hata durumunda sabit kırmızı nokta.
  Animasyonlar yalnızca ilgili durumda çalışır; boştayken CPU harcamaz.
- Mini widget üzerinde istasyon adına tıklanınca açılan favori hızlı değiştirme paneli (popup); listeden
  seçilen istasyon doğrudan oynatılır.
- Ayarlar ekranına "Kontrolleri otomatik gizle" seçeneği eklendi; widget opaklığı aralığı isteğe uygun
  olarak %50–%100'e çekildi (önceki %30–%100).
- Widget boyutu etiketi "Büyük" yerine "Genişletilmiş" olarak güncellendi (bağlam menüsüyle tutarlı).
- 2 yeni otomatik test: oto-gizle ayarının kalıcılığı ve yeni opaklık aralığının sıkıştırması (toplam 76 test).

### Değiştirilmedi / korunmuştur
- Oynatma, durdurma, ses kontrolü, favori ekleme/çıkarma, favoriler arası geçiş, geçmiş, Radio Browser
  araması, sistem tepsisi (oynat/duraklat/önceki/sonraki zaten mevcuttu), başlangıçta çalıştırma, pencere
  konumu/DPI/çoklu monitör ve temiz kapatma — bunlara dokunulmadı.

### Ertelendi (dürüstçe bildirilir)
- **Ses görselleştirici** (Genişletilmiş modda gerçek ses seviyesine dayalı çubuklar) bu sürümde
  **eklenmedi**. LibVLC'de gerçek PCM/ses seviyesi okumak, `MediaPlayer.SetAudioCallbacks` ile ses
  çıkışının LibVLC'nin kendi aygıt çıkışının yerine geçirilmesini gerektirir; bu, çalışan sesi bozma
  riskini doğrudan taşır ve bu ortamda gerçek Windows ses aygıtıyla test edilemez. Görevdeki açık kural
  gereği ("gerçek ses analizi mümkün değilse rastgele çubukları gerçekmiş gibi sunma"), sahte/rastgele bir
  görselleştirici eklemek yerine özellik tamamen bu sürüme bırakıldı. Bkz. ROADMAP.md.

## [0.1.0] — 2026-11-13

İlk sürüm. **Not (0.2.0 notu):** `E:\RadioFlow\artifacts\publish\win-x64\RadioFlow.exe` içeriğinden,
kullanıcının bu sürümü kendi Windows 11 makinesinde `build.ps1` ile gerçek derleyiciyle derlediği ve
yayınladığı doğrulanmıştır (dosya zaman damgaları, satır içi geliştirme sırasında Linux ortamında
yapılabilenin ötesine geçer). Bkz. ROADMAP.md.

### Eklendi
- Katmanlı çözüm: `RadioFlow.Core`, `RadioFlow.Infrastructure`, `RadioFlow.App` (WPF) ve `RadioFlow.Tests`.
- Merkezi `RadioPlayerService`: durum makinesi (Stopped, Connecting, Buffering, Playing, Paused, Reconnecting, Error),
  artan gecikmeli ve sınırlı yeniden bağlanma, istasyon değişiminde iptal, eşzamanlı Play koruması
  ve canlı yayında duraklatma davranışı.
- `LibVlcAudioEngine`: tek LibVLC/MediaPlayer örneği, sıralı olay kuyruğu ve ICY metadata okuma.
- M3U/PLS çözümleyici: boyut ve derinlik sınırı, güvensiz girdileri eler.
- Radio Browser istemcisi: DNS ile sunucu keşfi, yedek sunucuya geçiş, zaman aşımı, Türkçe karakterli arama,
  ülke ve tür listeleri, tıklama bildirimi.
- SQLite şeması (sürümlü geçiş): RadioStations, Favorites, ListeningHistory, Settings ve Türkçe duyarlı önbellek araması.
- Ayar servisi (anahtar başına JSON, bozuk değerde anahtar bazında varsayılan) ve dinleme geçmişi takibi.
- Logo önbelleği: http/https, 512 KB sınırı, görüntü imzası denetimi, eşzamanlı indirme sınırı.
- URL doğrulayıcı: yalnızca http/https; katalog verisinde yerel ağ adresleri reddedilir.
- WPF arayüzü: mini widget, büyük panel, ayarlar penceresi, koyu/açık tema, Acrylic/Mica,
  sistem tepsisi, SMTC medya kontrolleri, başlangıç entegrasyonu ve tek örnek koruması.
- Serilog dönen dosya logu, global hata işleyicileri.
- 74 otomatik test (gerçek internet veya yayın gerektirmez).
- `build.ps1`, MSIX manifest şablonu ve `packaging/build-msix.ps1`.
