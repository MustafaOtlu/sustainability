using System.Text;
using UnityEngine;

/// <summary>
/// 3 Katmanlı Yanıt Mimarisi: Analiz → Öneri → Uyarı
/// GDD AI-CSDD Bölüm 6: Cevap Üretim Sistemi
/// 
/// Intent ve CitySnapshot'a göre dinamik, parametrik metin üretir.
/// Ton, şehir kriz seviyesine göre otomatik ayarlanır.
/// </summary>
public static class ResponseGenerator
{
    // ─── Ton Değerlendirmesi ────────────────────────────────────
    public enum DialogueTone { Normal, Warning, Crisis }

    public static DialogueTone EvaluateTone(CitySnapshot s)
    {
        if (s.money < -5000f || s.satisfaction < 30f)
            return DialogueTone.Crisis;
        if (s.airPollution > 3f || s.satisfaction < 50f || s.isBlackout || s.isWaterShortage)
            return DialogueTone.Warning;
        return DialogueTone.Normal;
    }

    /// <summary>Intent, Entity ve şehir durumuna göre yanıt üretir.</summary>
    public static string GenerateResponse(ChatIntent intent, ChatEntity entity, CitySnapshot ctx)
    {
        StringBuilder sb = new StringBuilder();
        DialogueTone tone = EvaluateTone(ctx);

        // Ton başlığı
        string tonePrefix = tone switch
        {
            DialogueTone.Crisis => "🔴 [KRİZ MODU]",
            DialogueTone.Warning => "🟡 [UYARI MODU]",
            _ => "🟢 [DANIŞMAN]"
        };

        sb.AppendLine(tonePrefix);
        sb.AppendLine();

        switch (intent)
        {
            case ChatIntent.FINANCE_HELP:
                GenerateFinanceResponse(sb, ctx);
                break;
            case ChatIntent.ENVIRONMENT_HELP:
                GenerateEnvironmentResponse(sb, ctx);
                break;
            case ChatIntent.BUILDING_SUGGEST:
                GenerateBuildingResponse(sb, ctx, entity);
                break;
            case ChatIntent.TRAFFIC_HELP:
                GenerateTrafficResponse(sb, ctx);
                break;
            case ChatIntent.CITIZEN_HELP:
                GenerateCitizenResponse(sb, ctx);
                break;
            case ChatIntent.ENERGY_HELP:
                GenerateEnergyResponse(sb, ctx);
                break;
            case ChatIntent.WATER_HELP:
                GenerateWaterResponse(sb, ctx);
                break;
            case ChatIntent.GENERAL_STATUS:
                GenerateStatusReport(sb, ctx);
                break;
            default:
                GenerateDefaultResponse(sb, ctx);
                break;
        }

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════
    // YANIT ŞEKİLLENDİRİCİLER
    // ═══════════════════════════════════════════════════════════════

    private static void GenerateFinanceResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"📊 [ANALİZ] Şehir kasası: {ctx.money:F0}₺ (Net: {ctx.netIncome:+0;-0}/gün)");

        if (ctx.money < 0)
        {
            sb.AppendLine("❗ [ÖNERİ] Bütçe negatif! Şunları dene:");
            sb.AppendLine("   • Bakım maliyeti yüksek binaları geçici olarak kapat.");
            sb.AppendLine("   • Yeni sanayi/ticaret binası kur (vergi geliri artırır).");
            if (ctx.netIncome < 0)
                sb.AppendLine($"⚠️ [UYARI] Gün başına {ctx.netIncome:F0}₺ kayıp yaşıyorsun. Kasanız erimekte!");
        }
        else
        {
            sb.AppendLine($"✅ [ÖNERİ] Bütçe pozitif ({ctx.money:F0}₺). Yatırım yapabilirsin.");
            if (ctx.netIncome > 200)
                sb.AppendLine("   💡 Tasarruf yüksek, yeni altyapı kur veya yeşil enerji yatırımı yap.");
        }
    }

    private static void GenerateEnvironmentResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"🌫️ [ANALİZ] Hava Kirliliği: {ctx.airPollution:F1} | Kirli Alan: {ctx.pollutedAreaPct:F0}%");
        sb.AppendLine($"🏭 Karbon Salınımı: {ctx.carbonEmission:F0}");

        if (ctx.pollutedAreaPct > 50f)
        {
            sb.AppendLine("❗ [ÖNERİ] Kirlilik kritik seviyede! Acilen:");
            sb.AppendLine("   • Fabrikaların çevresine park yerleştir (kirlilik absorbe eder).");
            sb.AppendLine("   • Ağır sanayi yerine Eko-Fabrika kullan.");
            sb.AppendLine($"⚠️ [UYARI] Kirli alan %70'i aşarsa Çevre Felaketi tetiklenir! (Şu an: %{ctx.pollutedAreaPct:F0})");
        }
        else if (ctx.airPollution > 3f)
        {
            sb.AppendLine("🔶 [ÖNERİ] Kirlilik artmaya başlıyor.");
            sb.AppendLine("   • Sanayi ve konut arasına tampon parklar yerleştir.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Çevre gayet temiz! Yeşil dengeyi korumaya devam et.");
        }
    }

    private static void GenerateBuildingResponse(StringBuilder sb, CitySnapshot ctx, ChatEntity entity)
    {
        sb.AppendLine("🏗️ [ANALİZ] Bina tavsiyesi hazırlanıyor...");

        // En acil ihtiyaç nedir?
        if (ctx.isBlackout)
        {
            sb.AppendLine("⚡ [ÖNERİ] Karartma var! Acilen enerji santrali kur.");
            sb.AppendLine("   • Kömür Santrali (ucuz, hızlı ama kirli) veya Güneş Tarlası (pahalı, temiz).");
        }
        else if (ctx.isWaterShortage)
        {
            sb.AppendLine("💧 [ÖNERİ] Su kesintisi! Yeni Su Pompası inşa et.");
        }
        else if (ctx.population >= ctx.populationCapacity && ctx.populationCapacity > 0)
        {
            sb.AppendLine("🏠 [ÖNERİ] Nüfus kapasitesi dolmuş! Konut inşa et.");
            sb.AppendLine("   • Küçük Konut (500₺, hızlı) veya Apartman (2500₺, yüksek kapasite).");
        }
        else if (ctx.netIncome < 0)
        {
            sb.AppendLine("🏭 [ÖNERİ] Gelir açığı var. Sanayi binası inşa ederek vergi gelirini artır.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Şehir dengeli. İstediğin binayı kurabilirsin.");
            sb.AppendLine("   💡 Kültür Merkezi ile mutluluk, Hastane ile sağlık artırabilirsin.");
        }
    }

    private static void GenerateTrafficResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"🚗 [ANALİZ] Yol: {ctx.totalRoads} tile | Ort. Tıkanıklık: {ctx.avgCongestion:P0}");

        if (ctx.avgCongestion > 0.7f)
        {
            sb.AppendLine("❗ [ÖNERİ] Trafik yoğun! Yeni yollar ekle.");
            sb.AppendLine("   • Alternatif güzergahlar oluştur (paralel yollar).");
            sb.AppendLine("⚠️ [UYARI] Trafik kilidi fabrika verimini %80 düşürür!");
        }
        else if (ctx.totalRoads == 0)
        {
            sb.AppendLine("🔶 [ÖNERİ] Hiç yol yok. Konut ve sanayi arasına yol çek.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Trafik akıcı durumda.");
        }
    }

    private static void GenerateCitizenResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"😊 [ANALİZ] Memnuniyet: {ctx.satisfaction:F0}% | Sağlık: {ctx.health:F0} | Mutluluk: {ctx.happiness:F0}");
        sb.AppendLine($"👤 Nüfus: {ctx.population}/{ctx.populationCapacity} | Son göç: {ctx.lastMigration:+0;-0;0}");

        if (ctx.satisfaction < 30f)
        {
            sb.AppendLine("🔴 [ÖNERİ] İSYAN TEHLİKESİ! Acil müdahale:");
            sb.AppendLine("   • Karartma/Su kesintisi varsa gider. Park ve Kültür Merkezi kur.");
            sb.AppendLine("⚠️ [UYARI] 30 gün boyunca %30 altında kalırsa oyun biter!");
        }
        else if (ctx.satisfaction < 50f)
        {
            sb.AppendLine("🔶 [ÖNERİ] Memnuniyet düşük. Kirlilik/gürültü azalt, park ve hastane kur.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Vatandaşlar memnun. Göç çekerek nüfusu artırabilirsin.");
        }
    }

    private static void GenerateEnergyResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"⚡ [ANALİZ] Enerji: {ctx.energyProduction:F0}/{ctx.energyConsumption:F0} MW");

        if (ctx.isBlackout)
        {
            sb.AppendLine("🔴 [ÖNERİ] KARARTMA! Tüm binalar çalışmıyor.");
            sb.AppendLine("   • Acilen santral kur. Kömür ucuz ama kirli, Güneş pahalı ama temiz.");
        }
        else if (ctx.energyProduction < ctx.energyConsumption * 1.2f)
        {
            sb.AppendLine("🔶 [ÖNERİ] Enerji kapasiten sınıra yaklaşıyor. Yeni santral planla.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Enerji durumu iyi. Fazla kapasiten var.");
        }
    }

    private static void GenerateWaterResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine($"💧 [ANALİZ] Su: {ctx.waterProduction:F0}/{ctx.waterConsumption:F0} m³");

        if (ctx.isWaterShortage)
        {
            sb.AppendLine("🔴 [ÖNERİ] SU KESİNTİSİ! Sağlık çöküşü başlıyor.");
            sb.AppendLine("   • Acilen yeni Su Pompası kur.");
        }
        else
        {
            sb.AppendLine("✅ [ÖNERİ] Su arzı yeterli.");
        }
    }

    private static void GenerateStatusReport(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine("📋 [ŞEHİR RAPORU]");
        sb.AppendLine($"   Gün: {ctx.currentDay}");
        sb.AppendLine($"   💰 Kasa: {ctx.money:F0}₺ ({ctx.netIncome:+0;-0}/gün)");
        sb.AppendLine($"   ⚡ Enerji: {ctx.energyProduction:F0}/{ctx.energyConsumption:F0} MW {(ctx.isBlackout ? "❌" : "✅")}");
        sb.AppendLine($"   💧 Su: {ctx.waterProduction:F0}/{ctx.waterConsumption:F0} m³ {(ctx.isWaterShortage ? "❌" : "✅")}");
        sb.AppendLine($"   👤 Nüfus: {ctx.population}/{ctx.populationCapacity}");
        sb.AppendLine($"   🌫️ Kirlilik: {ctx.airPollution:F1} | Karbon: {ctx.carbonEmission:F0}");
        sb.AppendLine($"   😊 Memnuniyet: {ctx.satisfaction:F0}%");

        // En acil sorun
        string urgentIssue = "Yok";
        if (ctx.isBlackout) urgentIssue = "⚡ Karartma!";
        else if (ctx.isWaterShortage) urgentIssue = "💧 Su kesintisi!";
        else if (ctx.satisfaction < 30) urgentIssue = "🔥 İsyan tehlikesi!";
        else if (ctx.money < -5000) urgentIssue = "💸 İflas tehlikesi!";
        else if (ctx.pollutedAreaPct > 50) urgentIssue = "🌍 Kirlilik krizi!";
        sb.AppendLine($"   ⚠️ Acil: {urgentIssue}");
    }

    private static void GenerateDefaultResponse(StringBuilder sb, CitySnapshot ctx)
    {
        sb.AppendLine("🤖 [DANIŞMAN] Sana nasıl yardımcı olabilirim?");
        sb.AppendLine("   Sorabileceklerin: para, kirlilik, bina, trafik, enerji, su, mutluluk, durum");

        // Otomatik öneri — en acil sorunu söyle
        CrisisLevel crisis = ctx.GetCrisisLevel();
        if (crisis == CrisisLevel.Critical)
        {
            sb.AppendLine();
            sb.AppendLine("🔴 Dikkat! Şehirde kritik sorunlar var. 'durum' yazarak rapor al.");
        }
    }
}
