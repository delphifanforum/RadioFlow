# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) esaslıdır; sürümleme [SemVer](https://semver.org/lang/tr/).

## [0.1.0] — 2026-11-13

İlk sürüm (geliştirme önizlemesi). Henüz Windows'ta derlenip çalıştırılmadı. Bkz. ROADMAP.md.

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
