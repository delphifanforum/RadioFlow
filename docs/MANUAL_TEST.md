# Manuel Test Listesi

GUI etkileşimleri ve gerçek ses çıkışı otomatik test edilmedi. Windows 11 üzerinde aşağıdaki adımları uygulayın
ve sonuçları işaretleyin. Sorun olursa `%LOCALAPPDATA%\RadioFlow\Logs` altındaki son log dosyasını ekleyin.

## 0. Derleme
- [ ] `.\build.ps1` hatasız tamamlanıyor (restore, build, test, publish, LibVLC dosya denetimi)
- [ ] `artifacts\publish\win-x64\RadioFlow.exe` çift tıklayınca açılıyor

## 1. İlk açılış
- [ ] Mini widget ekranın sağ altında açılıyor (360×150), sistem tepsisinde simge var
- [ ] Windows 11'de widget arka planı bulanık (Acrylic), köşeler yuvarlak
- [ ] `%LOCALAPPDATA%\RadioFlow\radioflow.db` ve `Logs\radioflow-YYYYMMDD.log` oluştu
- [ ] Uygulamayı ikinci kez başlatınca yeni pencere açılmıyor, mevcut widget öne geliyor

## 2. Oynatma
- [ ] Genişlet ile büyük panel açılıyor; Ana Sayfa'da Türkiye'nin popüler istasyonları listeleniyor (logolar yükleniyor)
- [ ] Karttaki ▶ ile ses geliyor; durum "Bağlanıyor… → Canlı yayın"
- [ ] ICY metadata gönderen bir istasyonda şarkı adı görünüyor ve şarkı değişince güncelleniyor
- [ ] Metadata göndermeyen istasyonda sahte şarkı adı yok, yalnızca durum metni görünüyor
- [ ] Ses kaydırıcısı ve sessiz düğmesi çalışıyor
- [ ] Duraklat → devam (30 sn içinde) kaldığı yerden; 30 sn sonra canlı yayına yeniden bağlanıyor
- [ ] Durdur, sesi kesiyor ve durumu "Durduruldu" yapıyor
- [ ] Çalarken büyük panel ↔ mini widget geçişinde ses KESİLMİYOR
- [ ] Hızlıca 5 farklı istasyona art arda tıklayınca yalnızca sonuncusu çalıyor

## 3. Bağlantı sorunları
- [ ] Çalarken interneti kesin: "Bağlantı koptu, yeniden bağlanılıyor (1/5)…" görünüyor
- [ ] İnternet geri gelince yayın kendiliğinden devam ediyor
- [ ] İnternet kapalı kalırsa 5 denemeden sonra Türkçe hata mesajı çıkıyor (stack trace yok)
- [ ] Yeniden bağlanırken Durdur'a basınca denemeler bitiyor
- [ ] İnternet kapalıyken arama yapınca önbellekteki istasyonlar ve uyarı bandı görünüyor

## 4. Arama ve kategoriler
- [ ] "kral", "Türkü", "İstanbul", "ISTANBUL" aramaları sonuç veriyor
- [ ] Ülkelere Göre → Türkiye → istasyonlar; Geri düğmesi listeye dönüyor
- [ ] Türlere Göre → filtre kutusu çalışıyor → tür seçimi istasyonları getiriyor

## 5. Favoriler ve geçmiş
- [ ] ☆ ile favori ekleme/kaldırma; widget ve kart simgeleri eş zamanlı güncelleniyor
- [ ] Favoriler sayfasında yukarı/aşağı sıralama kalıcı (uygulama yeniden açıldıktan sonra da)
- [ ] Widget'taki önceki/sonraki favoriler arasında sırayla geçiyor (sondan başa sarıyor)
- [ ] Son Dinlenenler sayfası dinlenen istasyonları yeniden eskiye listeliyor
- [ ] İstasyon Ekle: `file:///…` adresi Türkçe hata ile reddediliyor; geçerli http adresi ekleniyor ve çalıyor
- [ ] Özel istasyon silme onay soruyor ve favorilerden de kalkıyor

## 6. Widget davranışı
- [ ] Sürükleyerek taşınıyor; uygulama yeniden açıldığında aynı konumda
- [ ] "Konumu sabitle" açıkken sürüklenemiyor
- [ ] Her zaman üstte açık/kapalı çalışıyor
- [ ] Boyut: Kompakt / Normal / Genişletilmiş (sağ tık menüsü ve Ayarlar) — her biri farklı içerik gösteriyor
- [ ] Kompakt modda yalnızca logo, isim, canlı gösterge ve oynat/duraklat görünüyor; içerik taşmıyor
- [ ] Genişletilmiş modda yazı tipleri ve logo büyüyor, içerik kırpılmıyor
- [ ] İkinci monitöre taşıyıp monitörü çıkarın: widget görünür alana geri geliyor
- [ ] Sağ tık menüsündeki tüm öğeler çalışıyor
- [ ] Kapat (×): "Tepsiye küçült" açıksa gizleniyor ve ses devam ediyor; kapalıysa (panel de kapalıyken) uygulama çıkıyor

### 6b. RadioFlow 2.0 — yeni widget özellikleri
- [ ] Ayarlar → "Kontrolleri otomatik gizle" kapalıyken: tüm düğmeler (önceki/sonraki/durdur/favori/ses/genişlet) her zaman görünür
- [ ] Açıkken: fare widget dışındayken yalnızca logo/isim/canlı gösterge/oynat-duraklat görünür; fare widget üzerine gelince diğer düğmeler ~150 ms içinde yumuşakça belirir, fare ayrılınca kaybolur
- [ ] Oto-gizle açıkken bile Tab ile klavye odağı tüm düğmelere ulaşabiliyor (odak görünür olduğunda düğmeler de görünür hale geliyor)
- [ ] Canlı yayın göstergesi: Oynatırken yumuşak "nefes alma" animasyonu (mor nokta); bağlanırken/arabelleğe alırken dönen halka; duraklatıldığında sabit soluk nokta; bağlantı hatasında sabit kırmızı nokta
- [ ] İstasyon adına tıklanınca favori listesi popup olarak açılıyor; listeden bir istasyona tıklayınca o istasyon çalmaya başlıyor ve popup kapanıyor; favori yoksa panel boş açılıyor (hata vermiyor)
- [ ] Widget opaklık kaydırıcısı artık %50 ile %100 arasında (önceki minimum %30 değil)

## 7. Sistem entegrasyonu
- [ ] Tepsi menüsü: Aç, Oynat/Duraklat, Önceki, Sonraki, Sessize Al, Çıkış
- [ ] Çıkış: ses duruyor, tepsi simgesi kayboluyor, işlem Görev Yöneticisi'nden de kalkıyor
- [ ] Klavye medya tuşları (Oynat/Duraklat, Sonraki, Önceki) çalışıyor
- [ ] Windows ses açılır penceresinde / kilit ekranında istasyon adı ve logo görünüyor
- [ ] Ayarlar → "Windows başlangıcında çalıştır" açılınca `HKCU\…\Run\RadioFlow` oluşuyor; kapatınca siliniyor; oturum açınca uygulama başlıyor
- [ ] İstasyon değişiminde bildirim çıkıyor; ayardan kapatılınca çıkmıyor

## 8. Görünüm
- [ ] Koyu / Açık / Sistem teması anında uygulanıyor; İptal edince eski temaya dönüyor
- [ ] Windows'un açık/koyu modunu değiştirince "Sistem" teması takip ediyor
- [ ] %100, %150, %200 ölçeklerde metinler ve simgeler net
- [ ] Windows 10'da (veya saydamlık kapalıyken) düz renk arka plan, arayüz okunaklı
- [ ] Büyük pencere: ekranı kapla / geri al / küçült / taşı / yeniden boyutlandır çalışıyor

## 9. Kararlılık (ölçüm)
- [ ] 8+ saat kesintisiz çalma: Görev Yöneticisi'nde CPU (boşta, widget gizliyken) ve bellek trendi not edildi
- [ ] 100+ istasyon değişimi sonrası bellek sürekli artmıyor
