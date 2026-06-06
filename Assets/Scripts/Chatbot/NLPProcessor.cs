using UnityEngine;
using System.Text.RegularExpressions;

/// <summary>
/// Hafif kural tabanlı NLP motoru. Oyuncunun serbest Türkçe metin girişini analiz eder.
/// GDD AI-CSDD Bölüm 5: NLP Sistemi
/// 
/// Intent (Niyet): Oyuncunun ne sorduğunu/istediğini belirler.
/// Entity (Varlık): Hangi oyun nesnesiyle ilgili olduğunu belirler.
/// </summary>
public enum ChatIntent
{
    NONE,
    FINANCE_HELP,       // Para, gelir, bütçe
    ENVIRONMENT_HELP,   // Kirlilik, hava, sağlık
    BUILDING_SUGGEST,   // Bina, inşa, yapı
    TRAFFIC_HELP,       // Trafik, yol, araç
    CITIZEN_HELP,       // Mutluluk, memnuniyet, göç
    ENERGY_HELP,        // Enerji, elektrik, karartma
    WATER_HELP,         // Su, pompa, kesinti
    GENERAL_STATUS      // Durum, nasıl, rapor
}

public enum ChatEntity
{
    NONE,
    FACTORY,    // Fabrika, sanayi
    PARK,       // Park, yeşil alan
    HOSPITAL,   // Hastane, sağlık
    HOUSE,      // Ev, konut, apartman
    ROAD,       // Yol, cadde
    POWER_PLANT // Santral, enerji
}

public static class NLPProcessor
{
    // ─── Derlenmiş Regex Kalıpları (GC-safe) ────────────────────
    private static readonly Regex FinanceRegex = new Regex(
        @"(para|gelir|vergi|bütçe|iflas|borç|maliyet|kazanç|harca|kasa|ekonomi|mali)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PollutionRegex = new Regex(
        @"(kirli|hava|pis|duman|sağlık|hastalık|çevre|emisyon|karbon|zehir)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BuildingRegex = new Regex(
        @"(bina|inşa|yapı|kur|yerleştir|yık|yap)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TrafficRegex = new Regex(
        @"(trafik|yol|araç|tıkanık|ulaşım|cadde|kilitlen)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CitizenRegex = new Regex(
        @"(mutlu|memnun|göç|vatandaş|halk|isyan|mutsuz|nüfus)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnergyRegex = new Regex(
        @"(enerji|elektrik|karartma|santral|güneş|kömür|mw|watt)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WaterRegex = new Regex(
        @"(su|pompa|kesinti|kurak|susuz)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StatusRegex = new Regex(
        @"(durum|nasıl|rapor|özet|genel|ne yapmalı|analiz|yardım)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Entity Regex'leri
    private static readonly Regex FactoryEntity = new Regex(
        @"(fabrika|sanayi|endüstri|atölye|ağır)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ParkEntity = new Regex(
        @"(park|yeşil|bahçe|ağaç)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HospitalEntity = new Regex(
        @"(hastane|sağlık|doktor|klinik)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HouseEntity = new Regex(
        @"(ev|konut|apartman|yaşam|barınak)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RoadEntity = new Regex(
        @"(yol|cadde|sokak|şerit)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PowerEntity = new Regex(
        @"(santral|güneş tarlası|kömür santral)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Oyuncunun metin girişini analiz ederek Intent ve Entity çıkarır.
    /// Ağırlık bazlı — birden fazla eşleşmede en çok match olan kazanır.
    /// </summary>
    public static void AnalyzeInput(string input, out ChatIntent intent, out ChatEntity entity)
    {
        intent = ChatIntent.NONE;
        entity = ChatEntity.NONE;

        if (string.IsNullOrWhiteSpace(input)) return;

        string lower = input.ToLowerInvariant();

        // ─── Intent Skoru ───
        int maxScore = 0;

        int finScore = FinanceRegex.Matches(lower).Count;
        if (finScore > maxScore) { maxScore = finScore; intent = ChatIntent.FINANCE_HELP; }

        int polScore = PollutionRegex.Matches(lower).Count;
        if (polScore > maxScore) { maxScore = polScore; intent = ChatIntent.ENVIRONMENT_HELP; }

        int bldScore = BuildingRegex.Matches(lower).Count;
        if (bldScore > maxScore) { maxScore = bldScore; intent = ChatIntent.BUILDING_SUGGEST; }

        int trafScore = TrafficRegex.Matches(lower).Count;
        if (trafScore > maxScore) { maxScore = trafScore; intent = ChatIntent.TRAFFIC_HELP; }

        int citScore = CitizenRegex.Matches(lower).Count;
        if (citScore > maxScore) { maxScore = citScore; intent = ChatIntent.CITIZEN_HELP; }

        int engScore = EnergyRegex.Matches(lower).Count;
        if (engScore > maxScore) { maxScore = engScore; intent = ChatIntent.ENERGY_HELP; }

        int watScore = WaterRegex.Matches(lower).Count;
        if (watScore > maxScore) { maxScore = watScore; intent = ChatIntent.WATER_HELP; }

        int statScore = StatusRegex.Matches(lower).Count;
        if (statScore > maxScore) { intent = ChatIntent.GENERAL_STATUS; }

        // ─── Entity Tespiti ───
        if (FactoryEntity.IsMatch(lower)) entity = ChatEntity.FACTORY;
        else if (ParkEntity.IsMatch(lower)) entity = ChatEntity.PARK;
        else if (HospitalEntity.IsMatch(lower)) entity = ChatEntity.HOSPITAL;
        else if (HouseEntity.IsMatch(lower)) entity = ChatEntity.HOUSE;
        else if (RoadEntity.IsMatch(lower)) entity = ChatEntity.ROAD;
        else if (PowerEntity.IsMatch(lower)) entity = ChatEntity.POWER_PLANT;
    }
}
