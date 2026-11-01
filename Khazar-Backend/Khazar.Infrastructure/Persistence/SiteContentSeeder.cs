using Khazar.Application.Content;
using Khazar.Application.DTOs.Content;
using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Khazar.Infrastructure.Persistence;

/// <summary>
/// Seeds the storefront content the site launched with (formerly src/data/editorialCMS.json and the copy written
/// into the React pages), so a new database shows the same site the admin panel then edits.
/// Sections are added per key when missing and never overwritten. Articles, looks and FAQ items are seeded only
/// into a database that has no content at all, so ones an admin deleted do not come back on the next start.
/// </summary>
public static class SiteContentSeeder
{
    private const string Catalog = "/katalog";

    public static async Task SeedAsync(KhazarDbContext context, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingKeys = await context.ContentSections.Select(s => s.Key).ToListAsync(cancellationToken);
        var freshDatabase = existingKeys.Count == 0;

        var missing = Sections.Where(section => !existingKeys.Contains(section.Key)).ToList();
        foreach (var (key, value) in missing)
            await context.ContentSections.AddAsync(new ContentSection(key, ContentSchema.Serialize(value)), cancellationToken);

        if (freshDatabase)
        {
            if (!await context.JournalArticles.AnyAsync(cancellationToken))
                await context.JournalArticles.AddRangeAsync(Articles(), cancellationToken);
            if (!await context.LookbookLooks.AnyAsync(cancellationToken))
                await context.LookbookLooks.AddRangeAsync(Looks(), cancellationToken);
            if (!await context.FaqItems.AnyAsync(cancellationToken))
                await context.FaqItems.AddRangeAsync(Faq(), cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (missing.Count > 0)
            logger?.LogInformation(
                "Seeded {SectionCount} content section(s){Collections}.",
                missing.Count,
                freshDatabase ? " with the journal, lookbook and FAQ" : string.Empty);
    }

    /// <summary>A launch photo in wwwroot/images, named after its Unsplash photo id.</summary>
    private static string Unsplash(string photoId) => $"/images/photo-{photoId}.jpg";

    private static readonly (string Key, object Value)[] Sections =
    [
        ("site", new SiteSettingsSection
        {
            HeaderLinks =
            [
                new NavigationLink { Label = "Haqqımızda", To = "/haqqimizda" },
                new NavigationLink { Label = "Əlaqə", To = "/elaqe" },
            ],
            Phone = "+994 50 555 01 44",
            Email = "hello@khazar.az",
            Address = "Ülviyyə Bünyadova 74, Bakı, Azərbaycan",
            WorkingHours = "B.e. – Şənbə, 10:00 – 20:00",
            FooterHeading = "Bizimlə *əlaqə*",
            Copyright = "© 2026 Khazar Textile. Bütün hüquqlar qorunur.",
            FooterNote = "B2B və B2C müştərilərə xidmət göstərilir",
        }),
        ("hero", new HeroSection
        {
            Kicker = "Yeni Kolleksiya — FW 2026",
            TitleTop = "KHAZAR —",
            TitleLine1 = "OVERSIZE",
            TitleLine2 = "SWEATSHIRT",
            ProductSlug = "zoho-oversize-sweatshirt",
            CtaLabel = "Ölçü Seç",
            SecondaryLinkLabel = "Bütün kolleksiya",
            SecondaryLinkTo = Catalog,
            ImageUrl = "/images/hero-main.jpg",
            ImageAlt = "Khazar FW 2026 — boz oversize sweatshirt və geniş şalvar",
            SideImageUrl = "/images/hero-side.jpg",
            SideImageAlt = "Khazar — sweatshirt yan profil",
            CaptionLeft = "Look 01",
            CaptionRight = "FW 26",
            Signature = "Khazar Studio — Bakı",
        }),
        ("newArrivals", new NewArrivalsSection
        {
            Eyebrow = "Yeni gələnlər",
            Title = "Mövsümün *ilk* parçaları",
            LinkLabel = "Hamısına bax",
            LinkTo = Catalog,
        }),
        ("manifesto", new ManifestoSection
        {
            Eyebrow = "Brend Manifesti",
            Title = "Zamansız siluetlər,\n*yüksək keyfiyyət*",
            Paragraphs =
            [
                "Khazar hər parçanı bir fikirlə qurur: geyim trendin arxasınca getməməlidir — sizi illərlə müşayiət etməlidir. Avropa pambığı, üç iplik toxuma, möhkəmləndirilmiş tikiş: detallar sadə görünür, amma hər biri sınaqdan keçib.",
                "Biz az şey istehsal edirik və daha yaxşısını edirik. Limited seriyalar, tələsik olmayan dizayn və geyindiyiniz gün deyil, illərlə xidmət edən qarderob.",
            ],
            Signature = "— Khazar Studio, Bakı",
        }),
        ("lookbook", new LookbookSection { Eyebrow = "Editorial Lookbook", Title = "FW 2026 — Kombinlər" }),
        ("slogan", new SloganSection
        {
            Lines =
            [
                "OVERSIZE SWEATSHIRT — *AVROPA PARÇASI*",
                "ÜÇ İPLİK TOXUMA — MÖHKƏM MANSET",
                "ELASTİK FORMA — *LİMİTED SAY*",
            ],
        }),
        ("featureBlocks", new FeatureBlocksSection
        {
            Shipping = new FeatureBlock
            {
                Title = "Çatdırılma",
                Text = "Bakı daxili 1-2 iş günü, regionlara 3-5 iş günü ərzində çatdırılma. 150 ₼ üzəri sifarişlərdə çatdırılma pulsuzdur.",
                LinkLabel = "Ətraflı",
                LinkTo = "/catdirilma-ve-iade",
            },
            Faq = new FeatureBlock
            {
                Title = "Suallar və cavablar",
                Text = "Ölçü cədvəli, parça qayğısı və geri qaytarma şərtləri haqqında tez-tez verilən suallar.",
                LinkLabel = "Ətraflı",
                LinkTo = "/tez-tez-verilen-suallar",
                ImageUrl = "/images/faq-topics.jpg",
            },
        }),
        ("craftsmanship", new CraftsmanshipSection
        {
            Eyebrow = "Ustalıq",
            Title = "Parça birinci dərəcəlidir",
            Text = "Yüksək qramajlı pambıq, ikiqat yaxa və sıx tikiş — hər parça istifadə tarixçəsini nəzərə alınaraq qurulur. Yaxından baxanda fərq görünür: kənarlar temiz, toxuma sıx, formasını saxlayır.",
            ImageUrl = Unsplash("1603906650843-b58e94d9df4d"),
            ImageAlt = "Khazar parça detayı — yaxın çəkiliş",
            Highlights =
            [
                new TitledText { Title = "Toxuma", Text = "220–400 q/m² qramaj, üç iplik və french terry toxuma növləri." },
                new TitledText { Title = "Davamlılıq", Text = "Möhkəmləndirilmiş yaxa və mansetlər — forma uzunömürlü qalır." },
                new TitledText { Title = "Rahatlıq", Text = "Fırçalanmış iç səth, düşük omuz — gün boyu rahat fit." },
            ],
        }),
        ("journal", new JournalSection { Eyebrow = "Jurnal", Title = "Stil qeydləri" }),
        ("catalog", new CatalogSection
        {
            Eyebrow = "Khazar — Kataloq",
            Title = "KOLLEKSİYA ’26",
            Subtitle = "Zamansız siluetlər",
            Description = "Hər parça limited seriya, hər siluet illərlə geyiləcək şəkildə qurulub — az, amma daha yaxşı.",
            CoverImageUrl = Unsplash("1759229874914-c1ffdb3ebd0c"),
            CoverImageAlt = "Bej trikotaj sviter geyinmiş model — Khazar kolleksiyası",
        }),
        ("about", new AboutSection
        {
            Header = new PageHeader
            {
                Eyebrow = "Khazar Studio",
                Title = "Az sayda, yaxşı istehsal",
                Description = "Khazar hazır geyim markası 2026-cı ildə Bakıda qurulub — zamansız siluetlər, Avropa parça texnologiyası və məsuliyyətli istehsal ilə.",
            },
            CoverImageUrl = Unsplash("1680835099030-9c7532f744f1"),
            CoverImageAlt = "Khazar atelyesi — manekenlər və tikiş masaları",
            CoverCaption = "FW 2026 — Limited seriya",
            Quote = "Biz trendlərin arxasınca getmirik. Hər parçanı geyindiyiniz gün deyil, illərlə xidmət edəcəyi şəkildə qururuq.",
            QuoteAuthor = "— Khazar Studio, Bakı",
            Pillars =
            [
                new TitledText
                {
                    Title = "Bakı × Avropa sentezi",
                    Text = "Bakının şərq-qərb estetikasını Avropa parça texnologiyası ilə birləşdiririk: üç iplik toxuma, french terry və yüksək qramajlı pambıq — hamısı Khazar siluetlərinə uyğunlaşdırılır.",
                },
                new TitledText
                {
                    Title = "Sürdürülebilirlik",
                    Text = "Limited seriya istehsal edirik: artıq stok yox, mövsümi yanma yox. Hər kolleksiya ölçülə bilən miqdarda toxunur və parça qayğısı üçün uzunömürlü dizayn seçirik.",
                },
                new TitledText
                {
                    Title = "Pambıq keyfiyyəti",
                    Text = "220–400 q/m² qramajlı, uzun lifli pambıq seçirik. Fırçalanmış iç səth, möhkəmləndirilmiş yaxa — parça ilk gündə deyil, illərlə keyfiyyət göstərməlidir.",
                },
            ],
            CtaEyebrow = "Kolleksiya",
            CtaTitle = "FW ’26 ilə tanış olun",
            CtaLabel = "Kataloqa bax",
            CtaLink = Catalog,
        }),
        ("faqPage", new FaqPageSection
        {
            Header = new PageHeader
            {
                Eyebrow = "FAQ",
                Title = "Tez-tez verilən suallar",
                Description = "Ödəniş, çatdırılma, ölçü seçimi və iadə haqqında ən çox verilən sualların cavabları.",
            },
        }),
        ("shipping", new ShippingSection
        {
            Header = new PageHeader
            {
                Eyebrow = "Xidmət",
                Title = "Çatdırılma və İadə",
                Description = "Bakı daxili və regionlar üzrə kurye çatdırılması, pulsuz kurye şərti və 14 günlük iadə proseduru.",
            },
            DeliveryTitle = "Çatdırılma müddətləri",
            DeliveryRows =
            [
                new DeliveryRow { Region = "Bakı daxili", Time = "1–2 iş günü", Fee = "150 ₼+ pulsuz · digər 5 ₼" },
                new DeliveryRow { Region = "Abşeron zonası", Time = "1–3 iş günü", Fee = "150 ₼+ pulsuz · digər 5 ₼" },
                new DeliveryRow { Region = "Regionlar (rayonlar)", Time = "3–5 iş günü", Fee = "150 ₼+ pulsuz · digər 5 ₼" },
                new DeliveryRow { Region = "Ekspress (Bakı)", Time = "Eyni gün", Fee = "Standart +10 ₼" },
            ],
            DeliveryNote = "150 ₼ üzəri bütün sifarişlərdə standart çatdırılma pulsuzdur. Kuryer təhvilindən əvvəl SMS ilə xəbərdarlıq olunursunuz.",
            ReturnsTitle = "İadə proseduru — 14 gün",
            ReturnSteps =
            [
                new TitledText
                {
                    Title = "Müraciət edin",
                    Text = "Təhvil aldıqdan sonra 14 gün ərzində hello@khazar.az ünvanına və ya +994 50 555 01 44 nömrəsinə sifariş nömrənizlə yazın.",
                },
                new TitledText
                {
                    Title = "Qablaşdırın",
                    Text = "Məhsulu orijinal qablaşdırması və parça etiketi ilə birlikdə qaytarın. İstifadə və yuma izi olan məhsullar iadə edilmir.",
                },
                new TitledText
                {
                    Title = "Yoxlama və ödəniş",
                    Text = "Məhsul 2 iş günü ərzində yoxlanılır və təsdiqdən sonra ödənişiniz eyni üsulla tam geri qaytarılır.",
                },
            ],
        }),
        ("contact", new ContactSection
        {
            Header = new PageHeader
            {
                Eyebrow = "Əlaqə",
                Title = "Bizimlə əlaqə saxlayın",
                Description = "Sifariş, ölçü və ya kolleksiya haqqında suallarınız üçün yazın — 1 iş günü ərzində cavablandırırıq.",
            },
            ShowroomTitle = "Showroom — Bakı",
            ShowroomNote = "Showroomda kolleksiyaya canlı baxış və ölçü seçimi mümkündür — gəlməzdən əvvəl zəng ilə təsdiq etməyiniz xahiş olunur.",
        }),
        ("privacy", new PrivacySection
        {
            Header = new PageHeader
            {
                Eyebrow = "Hüquqi məlumat",
                Title = "Gizlilik Siyasəti",
                Description = "Khazar saytından istifadə edərkən hansı məlumatları topladığımızı, onlardan necə istifadə etdiyimizi və hüquqlarınızı izah edirik.",
            },
            LastUpdated = "Son yenilənmə: 31 oktyabr 2026",
            Blocks =
            [
                new PolicyBlock
                {
                    Heading = "Topladığımız məlumatlar",
                    Paragraphs =
                    [
                        "Hesab: ad, soyad, e-poçt ünvanı və şifrə. Şifrə açıq mətn kimi deyil, geri çevrilməyən heş şəklində saxlanılır.",
                        "Sifariş: ad, soyad, e-poçt, telefon, çatdırılma ünvanı, seçilmiş məhsullar və ödəniş üsulu.",
                        "Əlaqə forması: adınız, e-poçtunuz və mesajınız. Xəbər bülleteni: e-poçt ünvanınız. Rəylər: yazdığınız rəy, qiymət və adınızın qısa forması.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Ödəniş məlumatları",
                    Paragraphs =
                    [
                        "Kart nömrəsi, CVV kodu və kartın digər məlumatları serverlərimizdə saxlanılmır. Sifarişdə yalnız seçdiyiniz ödəniş üsulu qeyd olunur.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Məlumatlardan necə istifadə edirik",
                    Paragraphs =
                    [
                        "Sifarişləri emal etmək və çatdırmaq, hesabınızı idarə etmək, müraciətlərinizə cavab vermək və sifariş ilə hesabla bağlı e-poçtlar göndərmək üçün.",
                        "Xəbər bülleteni yalnız abunə olduğunuz halda göndərilir; məktubdakı linklə istənilən vaxt abunəlikdən çıxa bilərsiniz.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Məlumatların ötürülməsi",
                    Paragraphs =
                    [
                        "Məlumatlarınızı satmırıq. Sifariş məlumatları yalnız sifarişin icrası üçün daxili uçot sistemimizə (NexusERP) və çatdırılma üçün lazım olan tərəflərə ötürülür. Qanunla tələb olunan hallarda səlahiyyətli orqanlara təqdim oluna bilər.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Brauzerdə saxlanılan məlumatlar",
                    Paragraphs =
                    [
                        "Sayt reklam və izləmə kukiləri işlətmir. Səbətiniz, istək siyahınız və giriş sessiyanız brauzerinizin yerli yaddaşında saxlanılır; onları brauzer ayarlarından silə bilərsiniz.",
                        "Şriftlər Google Fonts, şəkillər isə kənar şəkil xidmətləri vasitəsilə yüklənir; bu zaman IP ünvanınız həmin xidmətlərə görünə bilər.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Hüquqlarınız",
                    Paragraphs =
                    [
                        "Haqqınızda saxlanılan məlumatlarla tanış olmağı, onların düzəldilməsini və ya silinməsini tələb edə bilərsiniz. Bunun üçün bizə yazın: hello@khazar.az. Qanunla saxlanılması tələb olunan sifariş qeydləri silinməyə bilər.",
                    ],
                },
                new PolicyBlock
                {
                    Heading = "Dəyişikliklər",
                    Paragraphs =
                    [
                        "Bu siyasət yeniləndikdə son variant yenilənmə tarixi ilə birlikdə bu səhifədə dərc olunur.",
                    ],
                },
            ],
        }),
        ("sizeGuide", new SizeGuideSection
        {
            Eyebrow = "Ölçü bələdçisi",
            Title = "Ölçü cədvəli",
            Text = "Ölçüləri santimetr cinsində, bədəninizin ən geniş nöqtəsindən alın. Oversize modellərdə bir ölçü kiçik seçmək tövsiyə olunur.",
            Rows =
            [
                new SizeGuideRow { Size = "XS", Chest = "82–86", Waist = "66–70", Neck = "35–36" },
                new SizeGuideRow { Size = "S", Chest = "87–92", Waist = "71–76", Neck = "37–38" },
                new SizeGuideRow { Size = "M", Chest = "93–98", Waist = "77–82", Neck = "39–40" },
                new SizeGuideRow { Size = "L", Chest = "99–104", Waist = "83–88", Neck = "41–42" },
                new SizeGuideRow { Size = "XL", Chest = "105–110", Waist = "89–94", Neck = "43–44" },
                new SizeGuideRow { Size = "XXL", Chest = "111–116", Waist = "95–100", Neck = "45–46" },
            ],
            ModelSize = "M",
            ModelNote = "Model 185 sm boyundadır və M ölçü geyinib.",
        }),
        ("search", new SearchSection { TrendingSearches = ["Hoodie", "Oversize", "Pambıq", "Palto"] }),
    ];

    private static IEnumerable<LookbookLook> Looks()
    {
        static string Hotspot(string slug, double x, double y) =>
            ContentSchema.Serialize(new List<LookbookHotspotDto> { new() { ProductSlug = slug, X = x, Y = y } });

        const string cta = "Kolleksiyanı kəşf et";
        yield return new LookbookLook("Urban Minimalism", Unsplash("1602092298319-1354f9761afc"), cta, Catalog,
            Hotspot("zoho-oversize-sweatshirt", 50, 38), 10, true);
        yield return new LookbookLook("Essential Layers", Unsplash("1719565685362-4646a620d888"), cta, Catalog,
            Hotspot("merino-longsleeve", 50, 36), 20, true);
        yield return new LookbookLook("Tailored Ease", Unsplash("1646564200577-9d019b6313d2"), cta, Catalog,
            Hotspot("tailored-wool-coat", 54, 40), 30, true);
    }

    private static IEnumerable<JournalArticle> Articles()
    {
        static ArticleSectionDto Section(string heading, string[] paragraphs, string imageUrl = "", string imageAlt = "") =>
            new() { Heading = heading, Paragraphs = [.. paragraphs], ImageUrl = imageUrl, ImageAlt = imageAlt };

        yield return new JournalArticle(
            "kapsul-qarderob-telimati",
            "Kapsul qarderob təlimatı",
            "12 parça, sonsuz kombin: minimal qarderobu necə qurmalı — rəng palitrasından ölçü məntiqinə qədər.",
            Unsplash("1558769132-cb1aea458c5e"),
            "Dərgi · 5 dəqiqə",
            ContentSchema.Serialize(new List<ArticleSectionDto>
            {
                Section("", ["Kapsul qarderob az sayda, bir-biri ilə rahat birləşən parçadan ibarət qarderobdur. Məqsəd daha az almaq deyil, hər səhər daha az düşünməkdir: şkafdakı hər parça digərləri ilə uyğun gəlirsə, 12 parçadan onlarla kombin çıxır."]),
                Section("1. Rəng palitrasını daraldın",
                [
                    "İki-üç neytral əsas rəng seçin — məsələn, krem, antrasit və qəhvəyi. Bunların üzərinə bir və ya iki vurğu rəngi əlavə edin. Neytral baza nə qədər sabit olsa, vurğu rəngləri bir o qədər asan dəyişdirilir.",
                    "Qayda sadədir: yeni parça mövcud parçalardan ən azı üçü ilə birləşmirsə, kapsula daxil deyil.",
                ]),
                Section("2. Əsas 12 parça",
                    ["Üst: iki sadə T-shirt, bir uzunqol, bir sweatshirt, bir köynək. Alt: bir geniş bacaqlı şalvar, bir düz kəsim şalvar, bir rahat şalvar. Üst geyim: bir yüngül gödəkcə və bir palto. Tamamlayıcı: bir trikotaj və bir lay kimi geyilən jilet və ya kardiqan."],
                    Unsplash("1603906650843-b58e94d9df4d"), "Yaxından parça toxuması"),
                Section("3. Ölçü məntiqi",
                    ["Kapsulda siluetlər bir-birini balanslaşdırmalıdır: oversize üst parçanı daha dar alt parça ilə, geniş şalvarı isə daha yığcam üst ilə geyinin. Hər kateqoriyada ən azı bir rahat və bir strukturlu parça saxlamaq kombin sayını ikiqat artırır."]),
                Section("4. Keyfiyyət sayından vacibdir",
                    ["Az parça daha çox geyinilir, ona görə də yüksək qramajlı pambıq, möhkəmləndirilmiş yaxa və sıx tikiş burada əsas şərtdir. Formasını saxlayan parça mövsümlərlə kapsulun bir hissəsi olaraq qalır."]),
            }),
            10,
            true);

        yield return new JournalArticle(
            "movsumun-esas-siluetleri",
            "Mövsümün əsas siluetləri",
            "Oversize-dən geniş bacaqlı şalvara: FW 2026-nın daşıyıcı formaları və onları gündəliyə uyğunlaşdırma qaydası.",
            Unsplash("1573545289441-827c028f7a3b"),
            "Dərgi · 7 dəqiqə",
            ContentSchema.Serialize(new List<ArticleSectionDto>
            {
                Section("", ["FW 2026 mövsümü rahatlığı strukturla birləşdirir. Formalar genişlənir, amma diqqətlə qurulur: hər siluetin bir aparıcı xətti var və qalan parçalar onu tamamlayır."]),
                Section("Oversize üst",
                    ["Düşük çiyin, geniş gövdə və bir az uzun kəsim mövsümün ən tanınan formasıdır. Gündəlik həyatda onu balanslaşdırmaq üçün alt hissədə daha düz və ya daralan xətt seçin, qolları isə bir-iki dəfə qatlayın ki, bilək görünsün."]),
                Section("Geniş bacaqlı şalvar",
                    ["Yüksək bel və yerə qədər düşən geniş bacaq ayağı uzadır. Yığcam T-shirt və ya içəri salınmış köynəklə geyinildikdə silueti ən təmiz görünür. Ayaqqabı seçərkən burnu görünən modellərə üstünlük verin."],
                    Unsplash("1666443762372-c86511e64151"), "Mövsümün siluetləri"),
                Section("Strukturlu palto",
                    ["Mövsümün üst qatı düz çiyinli, diz səviyyəsinə qədər uzanan yun paltodur. O, rahat parçaları bir araya gətirib ofisdən axşama qədər daşıyan bir çərçivə rolunu oynayır."]),
                Section("Gündəliyə uyğunlaşdırma qaydası",
                    ["Bir kombində yalnız bir həcmli parça olsun. Rəngləri tonda saxlayın ki, forma ön plana çıxsın. Toxuma fərqi — trikotaj, yun, pambıq — monoton rəng palitrasına dərinlik verir."]),
            }),
            20,
            true);
    }

    private static IEnumerable<FaqItem> Faq()
    {
        (string Question, string Answer)[] items =
        [
            ("Hansı ödəniş üsulları mövcuddur?",
                "Bank kartı (Visa / Mastercard), qapıda nağd və qapıda terminal ilə ödəyə bilərsiniz. Kart ödənişləri tam şifrələnmiş kanalla emal olunur; sifariş təsdiqi dərhal e-poçtunuza göndərilir."),
            ("Çatdırılma nə qədər vaxt aparır?",
                "Bakı daxili 1–2 iş günü, regionlara 3–5 iş günü ərzində çatdırılma edilir. 150 ₼ üzəri sifarişlərdə çatdırılma pulsuzdur; ekspress çatdırılma seçimi eyni gün təhvil verir (+10 ₼)."),
            ("Ölçü seçimində necə rəhbərliyə bilərəm?",
                "Hər məhsulun səhifəsində beden matrisi və stok göstəricisi var. Oversize modellərdə adətən bir ölçü kiçik seçilməsi tövsiyə olunur; dəqiq ölçü cədvəli üçün məhsulun \"Material\" bölməsindəki toxuma qramajına baxın."),
            ("İadə şərtləri nədir?",
                "Məhsulu təhvil aldıqdan sonra 14 gün ərzində qaytarə bilərsiniz. Parça etiketi sökülməmiş, istifadə və yuma izi olmayan məhsullar tam geri ödənilir. Ətraflı: Çatdırılma və İadə səhifəsi."),
            ("Sifarişimi necə izləyə bilərəm?",
                "Sifariş təsdiqində #KHZ- ilə başlayan nömrəniz var. Hər hansı sualda bu nömrəni göstərərək +994 50 555 01 44 nömrəsinə və ya hello@khazar.az ünvanına yaza bilərsiniz."),
            ("Məhsullar limited seriyadırmı?",
                "Bəli — hər kolleksiya məhdud sayda toxunur və bəziləri bir daha təkrarlanmır. \"Limited seriya\" etiketli məhsullar bitdikdə yenidən istehsal olunmur."),
        ];

        return items.Select((item, index) => new FaqItem(item.Question, item.Answer, (index + 1) * 10, true));
    }
}
