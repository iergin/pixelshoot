# Facebook SDK Kurulum Kılavuzu — PixelShoot

> Facebook SDK Unity Package Manager üzerinden gelmiyor. Bu yüzden manuel
> indirip içe aktarman gerek. Tüm kod tarafı hazır — `PIXELSHOOT_FACEBOOK`
> define'ını ekleyince devreye girer.

## 1. Facebook Developer Console'da Uygulama Oluştur

1. https://developers.facebook.com/apps adresine git.
2. **Create App** → "Business" veya "None" seç → ad ve email gir.
3. Uygulama oluştuktan sonra **Dashboard**'da **App ID** ve **App Secret** görünür. App ID'yi not al, lazım olacak.
4. Sol menüden **Add Product** → **Facebook Login for Gaming** veya **Facebook Login** (mobile için "for Gaming" tavsiye edilir).
5. **App Settings ▶ Basic**'te platform ekle:
   - **Android** → package name (`com.yourcompany.pixelshoot`) ve key hash gir.
   - **iOS** → bundle id.
6. **App Settings ▶ Advanced** → **Native or desktop app?** = Yes.

### Android key hash al

Debug için:
```bash
keytool -exportcert -alias androiddebugkey -keystore ~/.android/debug.keystore | openssl sha1 -binary | openssl base64
```
Şifre: `android`. Çıkan base64 string'i Facebook Developer Console'a yapıştır.

## 2. SDK'yı İndir ve Unity'ye Import Et

1. https://developers.facebook.com/docs/unity/downloads/ → en son `facebook-unity-sdk-X.X.X.zip`'i indir.
2. Zip'i aç → içinde `FacebookSDK.unitypackage` var.
3. Unity'de **Assets ▶ Import Package ▶ Custom Package** → seç → tüm dosyaları import et.
4. Import bittiğinde menüde **Facebook** sekmesi gelir.

## 3. Unity Tarafı Konfigürasyon

1. **Facebook ▶ Edit Settings**'i aç.
2. **App Name**: PixelShoot.
3. **App Id**: Console'dan aldığın app ID'yi yapıştır.
4. **Android Settings**:
   - Package Name (`com.yourcompany.pixelshoot`)
   - Class Name (varsayılan: `com.facebook.unity.FBUnityPlayerActivity`)
5. **iOS Settings**: bundle id (Player Settings ile aynı olmalı).

## 4. Scripting Define Symbol Ekle

**Edit ▶ Project Settings ▶ Player ▶ Other Settings ▶ Scripting Define Symbols**

Şu define'ı ekle (her platform için ayrı ayrı): `PIXELSHOOT_FACEBOOK`

Bu olmadan `FacebookInitializer` çağrıları no-op'tur.

## 5. AndroidManifest (genelde otomatik halleder)

Facebook SDK Unity import sırasında manifest'e gerekli içerikleri ekler. Tamamlandığında **Facebook ▶ Build Helper**'dan manifest'in geçerliliğini doğrulayabilirsin.

## 6. Init Scripti Zaten Hazır

`Assets/Scripts/Facebook/FacebookInitializer.cs` — `RuntimeInitializeOnLoad` ile sahne yüklenmeden önce kendini bootstrap'liyor. Yani hiçbir GameObject'e bağlamana gerek yok.

Yaptıkları:
- `FB.Init()` çağırır, hazır olduğunda `FB.ActivateApp()` ile session başlatır.
- App background'dan dönünce tekrar `ActivateApp` (session tracking için kritik).
- FB diyaloğu açıldığında `Time.timeScale = 0` ile oyunu pause eder.
- `IsInitialized` static property'si ile durumu sorgulayabilirsin.

## 7. Event Loglama (zaten hazır helper'lar)

```csharp
using PixelShoot.FacebookIntegration;

// Level bittiğinde
FacebookInitializer.LogLevelCompleted(PlayerProgress.DisplayLevel);

// Purchase olduğunda (Unity IAP callback'inde)
FacebookInitializer.LogPurchase(0.99m, "USD", "com.pixelshoot.basic");

// Custom event
FacebookInitializer.LogCustomEvent("rewarded_watched", new Dictionary<string, object> {
    { "level", PlayerProgress.DisplayLevel }
});
```

Tüm helper'lar **SDK kurulu olmasa bile compile eder** — `#if PIXELSHOOT_FACEBOOK` ile sarılı oldukları için kuruluysa gerçekten çalışır, kurulu değilse no-op.

## 8. Test Sırası

1. SDK'yı import et.
2. Edit Settings → App ID gir.
3. `PIXELSHOOT_FACEBOOK` define ekle.
4. Play moduna gir → console'da `[Facebook] FB.Init complete + ActivateApp.` görmeli.
5. Build alıp telefonda dene → Facebook Events Manager (Console ▶ Analytics ▶ Events) sayfasında olayların düşmesi 5-15 dakika sürebilir.

## 9. Sıkça Yaşanan Hatalar

| Hata | Sebep | Çözüm |
|---|---|---|
| `FB.IsInitialized = false` Play modunda | App ID girilmemiş ya da PIXELSHOOT_FACEBOOK eksik | Edit Settings'ten App ID kontrol et |
| Android build hatası: `merge AndroidManifest.xml` | İki paketin manifest'i çakışıyor (örn. AdMob ile birlikte) | `mainTemplate.gradle` ve `AndroidManifest.xml` template'larını manuel düzenle |
| `Could not find dependency...` | External Dependency Manager eksik | Google EDM4U import et (AdMob için zaten gerekli) |
| iOS build sonrası app crashleniyor | LSApplicationQueriesSchemes eksik | Info.plist'e `fbauth2`, `fb-messenger-share-api` schemes ekle |

## Özet

✅ Zaten yapıldı:
- `Assets/Scripts/Facebook/FacebookInitializer.cs` (gated, auto-bootstrap, helper API'leri).

🟡 Senin yapacakların:
1. Facebook Developer Console'da app oluştur, App ID al.
2. `facebook-unity-sdk.unitypackage` indir + import et.
3. `Facebook ▶ Edit Settings` → App ID gir.
4. Player Settings → `PIXELSHOOT_FACEBOOK` define ekle.
5. Build alıp test et.
