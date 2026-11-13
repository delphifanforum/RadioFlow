# RadioFlow — Yol Haritası

İşaretler:
- ✅ **Uygulandı ve otomatik testle doğrulandı**
- 🟡 **Kodu yazıldı, henüz Windows'ta derlenip denenmedi.** Manuel test gerekiyor, bkz. [docs/MANUAL_TEST.md](docs/MANUAL_TEST.md)
- ⬜ **Bekliyor**

> Doğrulama ortamı: bulut Linux sandbox. NuGet erişimi yoktu ve WPF derlenemedi. Ayrıntılar [docs/VERIFICATION.md](docs/VERIFICATION.md) içinde.

## Faz 1 — Çalışan MVP

| Özellik | Durum |
|---|---|
| Solution (.slnx), merkezi paket yönetimi, katmanlar | 🟡 Core derlendi. Infrastructure ve testler stub'larla derlendi. Gerçek NuGet restore Windows'ta yapılacak |
| MVVM + DI altyapısı (Generic Host) | 🟡 |
| Mini widget ekranı | 🟡 |
| LibVLC oynatıcı entegrasyonu (tek merkezi servis) | 🟡 LibVlcAudioEngine. Durum makinesi ✅ testli |
| Oynatıcı durum makinesi, yeniden bağlanma, iptal, yarış koruması | ✅ |
| Manuel radyo URL ekleme (doğrulama + favorilere ekleme) | ✅ servis/test · 🟡 UI |
| Play / Stop / Pause / Volume / Mute | ✅ servis · 🟡 UI |
| Radio Browser arama, ülke/tür, popüler, sunucu yedeklemesi | ✅ (sahte HTTP ile) · gerçek API çağrısı 🟡 |
| Favoriler (ekle, kaldır, sırala, önceki/sonraki) | ✅ |
| Derleme + test | 🟡 Windows'ta `build.ps1` ile yapılmalı. 74/74 test doğrulama ortamında geçti |

## Faz 2 — Profesyonel arayüz

| Özellik | Durum |
|---|---|
| Büyük panel (sol menü, arama, Now Playing çubuğu) | 🟡 |
| İstasyon kartları (logo, ad, ülke, tür, favori, oynat) | 🟡 |
| Favoriler sayfası (sıralama) / Son dinlenenler / Ülkeler / Türler | 🟡 UI · ✅ veri katmanı |
| Glassmorphism: Acrylic (widget) / Mica (panel), düz renk yedeği | 🟡 |
| Koyu / açık / sistem teması | 🟡 |
| Kısa animasyonlar (sayfa girişi, düğme basma); sürekli animasyon yok | 🟡 |
| Gelişmiş widget ayarları (şeffaflık, boyut, sabitleme, üstte) | 🟡 UI · ✅ ayar kalıcılığı |
| Dil altyapısı (kaynak sözlüğü) | 🟡 Kısmi: menü ve araç ipuçları sözlükten okunuyor. Diğer metinler XAML/VM içinde Türkçe. İkinci dil ⬜ |

## Faz 3 — Windows entegrasyonu

| Özellik | Durum |
|---|---|
| Sistem tepsisi + menü (Aç, Oynat/Duraklat, Önceki, Sonraki, Sessiz, Çıkış) | 🟡 |
| Başlangıçta çalıştırma (EXE: HKCU Run, MSIX: StartupTask) | 🟡 |
| Medya tuşları + kilit ekranı bilgisi (SMTC) | 🟡 Hata olursa kendini kapatır, oynatmayı etkilemez |
| Bildirimler (tepsi balonu) | 🟡 |
| Pencere konumu saklama + ekran dışı düzeltme | 🟡 UI · ✅ ayar kalıcılığı |
| Tek örnek koruması | 🟡 |
| Hata yönetimi + Serilog dosya logu | 🟡 Global işleyiciler. Kullanıcıya stack trace gösterilmez |

## Faz 4 — Dağıtım

| Özellik | Durum |
|---|---|
| Release build + win-x64 self-contained publish betiği (LibVLC dosya denetimi dahil) | 🟡 `build.ps1` |
| Uygulama simgesi (.ico + PNG), sürüm bilgisi (Directory.Build.props) | ✅ dosyalar üretildi |
| MSIX manifest şablonu + görseller + `packaging/build-msix.ps1` | 🟡 Denenmedi |
| İmzalama sertifikası, Microsoft Store kimlik bilgileri | ⬜ |
| Kurulum/kaldırma testleri | ⬜ |
| Uzun süreli (24 saat) CPU/bellek ölçümü | ⬜ Hedefler henüz ölçülmedi |

## Sonraki adımlar (öneri)

1. Windows'ta `.\build.ps1` çalıştırıp derleme hatalarını düzeltmek. WPF/XAML kodu ilk kez gerçek derleyiciden geçecek.
2. [docs/MANUAL_TEST.md](docs/MANUAL_TEST.md) listesini uygulamak.
3. Uzun süreli kararlılık ölçümü yapmak (Görev Yöneticisi / `dotnet-counters`).
4. Kart listesi için sanallaştırılmış bir WrapPanel (şu an sayfa başına en fazla 150 kart).
5. İngilizce kaynak sözlüğü ve tüm metinlerin sözlüğe taşınması.
6. LibVLC'nin kendi içinde takip ettiği HTTP yönlendirmelerini kısıtlamak (şu an yalnızca uygulamanın kendi çözdüğü çalma listeleri denetleniyor).
