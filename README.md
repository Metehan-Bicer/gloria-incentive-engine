# Gloria Personel Prim Sistemi

Gloria Hotels & Resorts case çalışması için geliştirilmiş personel prim hesaplama uygulaması. PMS (Fidelio), POS (Flyby) ve ERP (Oracle JDE) kaynaklarından gelen satış verisini tek bir tabloda toplar, veritabanında tanımlı prim kurallarını uygular ve her personel için adım adım izlenebilir bir hesaplama sonucu üretir.

Mimari tasarım (ETL yaklaşımı, kural motoru, güvenlik ve denetim) için: [docs/mimari.md](docs/mimari.md)

## İçindekiler

- [Teknolojiler](#teknolojiler)
- [Çalıştırma](#çalıştırma)
- [Roller ve kimlik](#roller-ve-kimlik)
- [Uygulama akışı](#uygulama-akışı)
- [API](#api)
- [Kural motoru](#kural-motoru)
- [Veri aktarımı](#veri-aktarımı)
- [Testler](#testler)
- [Teknik kararlar ve gerekçeleri](#teknik-kararlar-ve-gerekçeleri)
- [Proje yapısı](#proje-yapısı)

## Teknolojiler

| Katman | Seçim |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10), Entity Framework Core 10, SQLite |
| CSV işleme | CsvHelper |
| API dokümantasyonu | Swagger (Swashbuckle) |
| Frontend | React 19, Vite, TypeScript, React Router |
| Test | xUnit |
| Çalıştırma | Docker Compose, GitHub Actions |

## Çalıştırma

### Docker ile (önerilen)

Tek komut:

```bash
docker compose up --build
```

Ayağa kalktığında:

| Adres | Açıklama |
|---|---|
| http://localhost:3000 | Web arayüzü |
| http://localhost:3000/swagger | API dokümantasyonu (nginx üzerinden) |
| http://localhost:8080 | API doğrudan |

İlk açılışta `data/` klasöründeki `personel.csv` dosyasından personel ve departmanlar, ardından üç kaynak CSV'si otomatik olarak yüklenir (`Import__AutoImportSamples=true`). Böylece ekranlar boş gelmez; Ağustos 2026 dönemi hesaplanmaya hazırdır. Veritabanı `incentive-db` adlı Docker volume'unda tutulur; sıfırdan başlamak için:

```bash
docker compose down -v
```

### Docker olmadan

Gereksinimler: .NET SDK 10, Node.js 22+.

Backend:

```bash
cd backend
dotnet run --project src/Gloria.Incentive.Api
```

API `http://localhost:5000` adresinde, Swagger `http://localhost:5000/swagger` altında çalışır. Development ortamında örnek CSV'ler otomatik yüklenir (`appsettings.Development.json`). SQLite dosyası `backend/src/Gloria.Incentive.Api/incentive.db` olarak oluşur; migration'lar açılışta uygulanır.

Frontend:

```bash
cd frontend
npm install
npm run dev
```

Arayüz `http://localhost:5173` adresinde açılır, `/api` istekleri Vite proxy ile 5000 portuna yönlendirilir.

## Roller ve kimlik

Case gereği gerçek kimlik doğrulama yerine HTTP header ile rol okunur. Her istekte:

| Header | Değer |
|---|---|
| `X-Role` | `Admin`, `Muhasebe` veya `Personel` |
| `X-Employee-No` | Personel sicil numarası (örn. `P1001`). `Personel` rolü için zorunlu. |

| Yetki | Admin | Muhasebe | Personel |
|---|---|---|---|
| Kural tanımlama/düzenleme/silme | ✓ | | |
| Kuralları görüntüleme | ✓ | ✓ | |
| CSV aktarımı | ✓ | ✓ | |
| Tüm personelin primini görme, toplu hesaplama | ✓ | ✓ | |
| Dönem kapatma | ✓ | ✓ | |
| Satış kaydı düzenleme | ✓ | ✓ | |
| Audit log görüntüleme | ✓ | ✓ | |
| Kendi primini görme | ✓ | ✓ | ✓ (yalnızca kendisi) |

Web arayüzünde üst çubuktaki rol ve personel seçicisi bu header'ları belirler. `Personel` rolüyle başka bir sicilin hesabı istendiğinde API 403 döner.

## Uygulama akışı

1. **Veri Aktarımı** ekranından (ya da açılıştaki otomatik yükleme ile) üç CSV içeri alınır. Her aktarım için okunan / aktarılan / mükerrer / hatalı satır sayıları ve reddedilen satırların ham hali ile sebebi görüntülenir.
2. **Prim Kuralları** ekranında kurallar listelenir, Admin yeni kural ekleyebilir. Kural tipine göre form değişir: yüzde alanı, işlem başına tutar ya da barem dilim tablosu. Her kuralın değişiklik geçmişi (kim, ne zaman, eski/yeni değer) aynı ekranda görünür.
3. **Prim Hesabı** ekranında personel ve ay seçilir. Brüt satış, iade, net satış ve hak edilen prim özetlenir; altında kural bazında gruplanmış hesaplama adımları listelenir: hangi belge, hangi kural, hangi oran, ara toplamlar, hesaba dahil edilmeyen kayıtlar ve sebepleri.
4. **Dönemler** ekranında tüm personel için toplu hesaplama çalıştırılır ve dönem kapatılır. Kapatılan dönemde satış kaydı düzenlenemez, o aya import yapılamaz, yeniden hesaplama çalıştırılamaz; personel ekranı dondurulmuş sonucu gösterir.

### Kod değişikliği olmadan yeni prim kalemi

PMS verisinde `GLF_LSN` kodlu bir "Golf Dersi" satışı var (P1007, 3.500 TL) ve başlangıçta bu ürüne uyan kural olmadığından hesaplamada "uygun prim kuralı yok" olarak listelenir. Admin rolüyle Prim Kuralları ekranından şu kural eklendiğinde:

- Tip: Sabit yüzde, Yüzde: 7, Kategori: GOLF

P1007'nin Ağustos 2026 hesabında "Golf dersi satışı %7" satırı 245,00 TL ile belirir. Uygulama yeniden başlatılmaz, kod değişmez. Aynı işlem API ile:

```bash
curl -X POST http://localhost:8080/api/rules \
  -H "X-Role: Admin" -H "Content-Type: application/json" \
  -d '{"name":"Golf dersi satışı %7","ruleType":"FixedPercentage","parametersJson":"{\"percentage\":7}","productCategory":"GOLF","priority":70,"validFrom":"2026-01-01","isActive":true}'
```

## API

Tüm uçlar `/api` altındadır; ayrıntılar Swagger'da.

| Uç | Yetki | Açıklama |
|---|---|---|
| `GET /employees` | Tümü | Personel listesi (Personel rolü yalnızca kendini görür) |
| `GET /employees/{no}/commissions/{yıl}/{ay}` | Tümü | Dönem açıksa hesaplayıp kaydeder ve adımlarıyla döner; kapalıysa dondurulmuş sonucu döner |
| `GET /rules`, `GET /rules/{id}`, `GET /rules/options` | Admin, Muhasebe | Kurallar ve form seçenekleri (tipler, kategoriler, departmanlar) |
| `POST /rules`, `PUT /rules/{id}`, `DELETE /rules/{id}` | Admin | Kural yönetimi; parametre JSON'u tipe göre doğrulanır |
| `POST /calculations/run?year=&month=` | Admin, Muhasebe | Tüm personel için hesaplama |
| `GET /calculations/{yıl}/{ay}` | Admin, Muhasebe | Dönem özeti, personel bazında toplamlar |
| `GET /periods` | Admin, Muhasebe | Dönem listesi |
| `POST /periods/{yıl}/{ay}/close` | Admin, Muhasebe | Hesaplamayı çalıştırır, sonuçları kesinleştirir, dönemi kapatır |
| `POST /import/{pms\|pos\|erp}` | Admin, Muhasebe | Multipart CSV yükleme |
| `GET /import/batches`, `GET /import/batches/{id}/errors` | Admin, Muhasebe | Aktarım geçmişi ve reddedilen satırlar |
| `GET /sales?year=&month=&employeeNo=`, `PUT /sales/{id}`, `DELETE /sales/{id}` | Admin, Muhasebe | Satış kaydı görüntüleme ve düzenleme (kapalı dönemde 409) |
| `GET /audit-logs?entity=&entityId=` | Admin, Muhasebe | Audit kayıtları |

Örnek:

```bash
curl -H "X-Role: Personel" -H "X-Employee-No: P1002" \
  http://localhost:8080/api/employees/P1002/commissions/2026/8
```

Yanıtta `ruleSummaries` kural bazında toplamları, `lines` ise sıralı hesaplama adımlarını içerir. Her satırın `lineType` değeri `Sale`, `Refund`, `Tier`, `RuleSubtotal`, `Excluded` ya da `Total` olur; satışa bağlı satırlarda `sale` alanı kaynak sistem, belge no, ürün ve tutarı taşır.

## Kural motoru

Kurallar `CommissionRules` tablosunda tutulur. Her kuralda:

- **Tip** (`RuleType`) ve tipe özel **parametre JSON'u** (`ParametersJson`)
- **Kapsam**: ürün kategorisi, ürün kodu, kaynak sistem, departman. Boş bırakılan alan "tümü" anlamına gelir.
- **Öncelik**, **geçerlilik aralığı** (`ValidFrom` / `ValidTo`) ve **aktiflik**

Desteklenen tipler:

| Tip | Parametre | Davranış |
|---|---|---|
| `FixedPercentage` | `{"percentage": 5}` | Her satış tutarının yüzdesi. İade satırı negatif tutarla düşer. |
| `TieredRate` | `{"mode": "Marginal", "tiers": [{"threshold": 0, "rate": 2}, {"threshold": 30000, "rate": 4}]}` | Aylık net toplam (satış − iade) barem tablosuna uygulanır. `Marginal`: her dilim kendi oranıyla; `Highest`: ulaşılan en yüksek dilimin oranı tüm tutara. |
| `FixedAmountPerTransaction` | `{"amount": 50}` | İşlem başına sabit tutar, tutardan bağımsız. İade işlemi aynı tutarı düşer. |

Hesaplama sırası (`CommissionCalculator`):

1. Personelin ilgili aydaki satış ve iade kayıtları çekilir.
2. İşe giriş tarihinden önceki ya da işten ayrılış tarihinden sonraki satışlar `Excluded` satırı olarak sebebiyle listelenir, hesaba girmez.
3. Dönemde geçerli aktif kurallar öncelik sırasıyla dolaşılır. Her kural kendi kapsamına uyan satırlar üzerinde bağımsız çalışır ve satır satır adım + ara toplam üretir.
4. Hiçbir kurala uymayan satışlar `Excluded` olarak "uygun prim kuralı yok" açıklamasıyla listelenir.
5. Kural ara toplamları toplanır. Toplam negatifse (iadeler satışları aştıysa) prim sıfırlanır, bu durum toplam satırının açıklamasında belirtilir.

Yeni bir kural eklemek veritabanına satır eklemekten ibarettir. Yeni bir **kural tipi** (örneğin ekip hedefi) gerekirse `ICommissionRuleStrategy` arayüzünü uygulayan bir sınıf yazılıp DI'a kaydedilir; motor tipi `RuleStrategyResolver` üzerinden çözer, mevcut hesaplama akışı değişmez.

Varsayılan kurallar ilk açılışta yüklenir: SPA hizmetleri %5, A la Carte kademeli (0–30.000 %2, 30.000–60.000 %4, 60.000+ %6), Buggy işlem başına 50 TL, Pavillon %3, POS SPA ürün satışı %4, Bar işlem başına 25 TL.

## Veri aktarımı

`POST /api/import/{kaynak}` üç kaynağı ayrı parser'larla işler (`PmsParser`, `PosParser`, `ErpParser`). Her satır için parse → doğrulama → mükerrer kontrolü → kayıt sırası izlenir. Reddedilen satırlar `ImportErrors` tablosuna satır numarası, ham içerik ve sebeple yazılır; mükerrerler `IsDuplicate` işaretiyle ayrı sayılır.

Mükerrer anahtarı `(kaynak sistem, belge no, ürün kodu)` üzerinde unique index ile veritabanı seviyesinde de korunur. Aynı dosya ikinci kez yüklendiğinde tüm satırlar mükerrer olarak atlanır.

Örnek verideki durumlar ve uygulanan karar:

| Durum | Karar |
|---|---|
| `32/08/2026`, `08.15.2026` gibi tarihler | Reddedilir; yalnızca `yyyy-MM-dd` kabul edilir |
| `2.500,00` (Türkçe biçim) | Kabul edilir, `2500.00` ile aynı şekilde normalize edilir |
| `abc`, boş tutar, sıfır adet/tutar | Reddedilir |
| EUR / USD para birimi | Reddedilir; kur dönüşümü kapsam dışı, sebep loglanır |
| Boş belge no, boş personel, listede olmayan personel (P9999) | Reddedilir |
| PMS `REVERSAL`, POS `IadeMi=E`, ERP `RM` | İade olarak kaydedilir; tutar mutlak değerle tutulur, `IsRefund` işaretlenir. ERP'de `Reference` alanı iadenin orijinal belgesi olarak saklanır |
| ERP `UNPOSTED` | Reddedilir (muhasebeleştirilmemiş) |
| ERP `XX` belge tipi, bilinmeyen iş birimi (1400) | Reddedilir |
| ERP'de ürün kodu yok | `Description` alanı PMS ürün adlarıyla birebir aynı olduğundan ad → kod eşlemesi yapılır; iş birimi 1100/1200/1300 → GSR/GGR/GVR |
| POS'ta Eylül tarihli satır | Geçerli kayıttır, Eylül dönemine yazılır |
| Bilinmeyen ürün (`GLF_LSN`) | İçeri alınır, kategori kod önekinden türetilir (GOLF) |
| Personelin işe girişinden önceki / ayrılışından sonraki satışlar (P1012, P1013) | İçeri alınır ama hesaplamada "hariç" olarak sebebiyle gösterilir |
| Kapalı döneme ait satır | Reddedilir |

Ürün kategorisi kod önekinden türetilir: `SPA_*` → SPA, `ALC_*` → ALC, `BUG_*` → BUGGY, `PAV_*` → PAVILLON, `GLF_*` → GOLF; POS PLU kodları 5xxx → SPA_RETAIL, 6xxx → ALC_EXTRA, 7xxx → BAR.

## Testler

```bash
cd backend
dotnet test
```

44 test; kural motoru veritabanı olmadan saf C# olarak test edilir:

- **Kademeli barem**: eşik altı tek dilim, `Marginal` modda dilimlere bölme, üst dilime ulaşma, `Highest` modu, iadenin net toplamı alt dilime düşürmesi, net toplamın sıfırın altında kalması, geçersiz barem tanımları
- **İade**: yüzde kuralında negatif düşüm, sabit tutarda işlem başına düşüm, iadelerin satışı aştığı ayda toplamın sıfırlanması
- **Hesaplama servisi**: birden fazla kuralın birlikte çalışması, istihdam dönemi dışı satışların hariç tutulması, kuralsız satışların listelenmesi, kapsam filtreleri (kaynak, departman), pasif/süresi dolmuş kurallar, veritabanından gelen yeni kuralın kodsuz uygulanması
- **Import**: Türkçe sayı biçimi, geçersiz tarih/tutar/para birimi, dosya içi ve aktarımlar arası mükerrer tespiti, kapalı döneme aktarım engeli (in-memory SQLite ile)

GitHub Actions (`.github/workflows/ci.yml`) her push ve pull request'te backend build + test, frontend build ve Docker imaj derlemesini çalıştırır.

## Teknik kararlar ve gerekçeleri

**SQLite.** Case iki seçenek sunuyor (LocalDB veya SQLite). LocalDB yalnızca Windows'ta çalıştığından, değerlendiren kişinin platformundan bağımsız olarak `docker compose up` ya da `dotnet run` ile sıfır kurulumla çalışmasını öncelikledim. EF Core kullanıldığı için SQL Server'a geçiş provider ve connection string değişikliğinden ibaret; entity, migration ve hesaplama kodu aynı kalır. Üretim mimarisinde SQL Server öngörülüyor (bkz. mimari doküman).

**Header tabanlı kimlik, ASP.NET Core'un kendi auth altyapısı ile.** Rolü middleware'de elle kontrol etmek yerine `AuthenticationHandler` yazdım; böylece `[Authorize(Roles = ...)]` attribute'ları, `User.IsInRole` ve `ICurrentUser` üzerinden standart akış kullanılıyor. Gerçek kimlik sağlayıcıya (Azure AD, Keycloak vb.) geçişte yalnızca handler değişir, controller'lar değişmez.

**Kural parametreleri JSON kolonunda.** Her kural tipinin parametreleri farklı (yüzde, tutar, dilim listesi). Bunları ayrı kolonlara ya da ayrı tablolara yaymak yerine tip başına şeması belli bir JSON tutuyorum. Şema strateji sınıfında doğrulanır (`ValidateParameters`), API geçersiz parametreyi 400 ile reddeder. Yeni tip eklemek migration gerektirmez.

**Strateji deseni + resolver.** Kural tipi → strateji eşlemesi DI'dan gelir. Motor tipleri bilmez, yalnızca `Evaluate` çağırır. Kademeli barem gibi aylık toplama bağlı kurallar da, satış başına çalışan kurallar da aynı arayüzü paylaşır; fark stratejinin içinde kalır.

**Bir satış birden fazla kurala uyabilir.** Örneğin "tüm satışlara işlem başına 10 TL" ile "SPA'ya %5" birlikte tanımlanabilsin diye kurallar birbirinden bağımsız çalışır; ilk eşleşende durma yok. Kapsam alanları çakışmayı yönetmek için yeterli; ihtiyaç olursa kurala "eşleşince dur" bayrağı eklemek küçük bir değişiklik.

**Hesaplama sonucu satır satır saklanır.** Sonuç yalnızca toplam olarak değil, her adım (`CommissionCalculationLines`) ile birlikte yazılır. Dönem kapandığında bu satırlar dondurulur; kural sonradan değişse bile kapalı dönemin sonucu değişmez ve geçmişe dönük izlenebilirlik korunur. Açık dönemde her görüntüleme güncel veriyle yeniden hesaplar ve önceki taslağın yerine yazar.

**Audit log `SaveChangesInterceptor` ile.** Kural, satış kaydı ve dönem değişikliklerini controller'larda elle loglamak yerine EF Core change tracker üzerinden yakalıyorum. Güncellemelerde yalnızca değişen alanların eski/yeni değeri, oluşturma ve silmede tam kayıt JSON olarak yazılır; aktör `ICurrentUser`'dan, açılıştaki otomatik import için `system` olarak gelir. Böylece hiçbir yazma yolu logu atlayamaz.

**Dönem kapama, silme yerine durum.** Kapalı dönem için satış düzenleme, import ve yeniden hesaplama 409 döner. Kapatma öncesi hesaplama bir kez daha çalıştırılır ki dondurulan sonuç son veriyi yansıtsın. Yeniden açma bilinçli olarak yok; case "kapatıldıktan sonra değiştirilememeli" diyor.

**İade tutarları pozitif, bayrakla.** İadeyi negatif tutar olarak saklamak yerine `IsRefund` ile işaretleyip tutarı mutlak tutuyorum. Raporlama ve toplamlar (brüt satış, iade toplamı) temiz kalıyor; hesaplama motoru `SignedAmount` üzerinden negatif etkisini uyguluyor.

**Negatif prim sıfırlanır, kural ara toplamı negatif kalabilir.** Önceki aya ait bir satışın iadesi bu ay geldiğinde kural ara toplamı negatife düşebilir ve diğer kurallardan gelen primi azaltır (mahsup). Ancak personelin toplam primi sıfırın altına inmez; bu, toplam satırında açıkça yazılır.

**İstihdam dönemi kontrolü.** `personel.csv`'de işe giriş/ayrılış tarihleri verildiğinden bunları anlamlı kullanıyorum: bu aralık dışındaki satışlar veriye alınır ama prim hesabında sebebiyle hariç tutulur. Böylece hem veri kaybı olmaz hem de "neden bu satış primime girmedi" sorusunun cevabı ekranda görünür.

**CSV'de yalnızca TRY.** Örnek veride EUR/USD satırlar var. Kur dönüşümü tarihli kur tablosu gerektirir ve prim politikasına bağlıdır; bu dilimde bunları hata olarak loglayıp muhasebenin görmesini sağladım. Mimari dokümanda kur tablosu ile normalize etme yaklaşımı anlatılıyor.

**Kaynaklar arası eşleştirme yapılmıyor.** ERP `Reference` alanında `PMS-xxxx` değerleri var ama örnek veride tutarlar ve personeller PMS kayıtlarıyla örtüşmüyor. Bu yüzden her kaynak bağımsız satış kaynağı olarak ele alındı; mükerrer kontrolü kaynak içinde yapılır. Gerçek entegrasyonda hangi kaynağın "otorite" olduğu iş kuralıyla belirlenip çapraz dedup eklenmelidir.

**Frontend sade tutuldu.** UI kütüphanesi yok, yalnızca React Router ve el yazımı CSS. Case'in istediği iki ekranın (kural yönetimi, personel prim görüntüleme) yanına, akışın uçtan uca gösterilebilmesi için dönem ve aktarım ekranları eklendi.

## Proje yapısı

```
backend/
  src/Gloria.Incentive.Api/
    Domain/         Entity'ler ve enum'lar
    Data/           DbContext, migration'lar, seed
    Auth/           Header tabanlı authentication handler, ICurrentUser
    Rules/          Kural stratejileri, parametre modelleri, resolver
    Calculation/    CommissionCalculator (saf hesaplama) ve servis (DB, dönem)
    Import/         CSV parser'ları, import servisi, ürün kataloğu
    Audit/          SaveChanges interceptor
    Periods/        Dönem servisi
    Controllers/    API uçları
  tests/Gloria.Incentive.Tests/
frontend/
  src/pages/        Prim kuralları, prim hesabı, dönemler, veri aktarımı
  src/api/          Tipler ve fetch istemcisi (rol header'larını ekler)
  src/auth/         Rol/personel oturumu (localStorage)
data/               Örnek CSV dosyaları
docs/mimari.md      Çözüm ve entegrasyon mimarisi
docker-compose.yml
.github/workflows/ci.yml
```
