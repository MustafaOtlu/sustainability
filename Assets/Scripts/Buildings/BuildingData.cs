using UnityEngine;

/// <summary>
/// Bina verilerini tanımlayan ScriptableObject.
/// Her bina tipi için bir asset oluşturulur ve BuildingPlacer tarafından kullanılır.
/// GDD Bölüm 5: Bina Sistemi
/// </summary>
[CreateAssetMenu(fileName = "NewBuilding", menuName = "Sustainability/Building Data")]
public class BuildingData : ScriptableObject
{
    // ─── Bina Kategorileri ───────────────────────────────────────
    public enum BuildingCategory
    {
        Residential,    // Konut
        Industrial,     // Sanayi
        Environmental,  // Park / Yeşil Alan
        Social,         // Toplum / Kültür
        Healthcare,     // Sağlık
        Power,          // Enerji Santrali
        Water           // Su Altyapısı
    }

    // ─── Temel Bilgiler ─────────────────────────────────────────
    [Header("Temel Bilgiler")]
    public string buildingName = "Yeni Bina";
    public BuildingCategory category;
    [TextArea(2, 4)] public string description;

    // ─── Boyut ve Maliyet ───────────────────────────────────────
    [Header("Boyut ve Maliyet")]
    [Tooltip("Grid genişliği (X)")]
    public int gridWidth = 1;
    [Tooltip("Grid derinliği (Z)")]
    public int gridHeight = 1;
    [Tooltip("İnşaat maliyeti (Eko-Para)")]
    public int buildCost = 100;
    [Tooltip("Her tick'teki bakım maliyeti (Eko-Para)")]
    public int maintenanceCostPerTick = 0;

    // ─── Kaynak Üretimi / Tüketimi ─────────────────────────────
    [Header("Enerji (MW)")]
    [Tooltip("Pozitif = üretim, Negatif = tüketim")]
    public float energyDelta = 0f;

    [Header("Su (m³)")]
    [Tooltip("Pozitif = üretim, Negatif = tüketim")]
    public float waterDelta = 0f;

    // ─── Gelir ──────────────────────────────────────────────────
    [Header("Gelir")]
    [Tooltip("Her tick'te toplanan vergi (Eko-Para)")]
    public float taxIncomePerTick = 0f;

    // ─── Nüfus ──────────────────────────────────────────────────
    [Header("Nüfus")]
    [Tooltip("Bu binanın barındırabileceği maksimum vatandaş sayısı")]
    public int populationCapacity = 0;

    // ─── Kirlilik Üretimi ───────────────────────────────────────
    [Header("Kirlilik Üretimi (Her Tick)")]
    public float airPollutionPerTick = 0f;
    public float waterPollutionPerTick = 0f;
    public float soilPollutionPerTick = 0f;
    public float carbonEmissionPerTick = 0f;

    // ─── Gürültü ────────────────────────────────────────────────
    [Header("Gürültü")]
    [Tooltip("Bu binanın yaydığı gürültü seviyesi (dB)")]
    public float noiseLevel = 0f;

    // ─── Pozitif Etkiler ────────────────────────────────────────
    [Header("Pozitif Etkiler")]
    [Tooltip("Etki alanındaki konutlara verilen mutluluk bonusu")]
    public float happinessBonus = 0f;
    [Tooltip("Şehir genelindeki sağlık değerine katkı")]
    public float healthBonus = 0f;
    [Tooltip("Etki alanı yarıçapı (tile)")]
    public int effectRadius = 0;

    // ─── Çevre Absorbe (Park sistemi) ───────────────────────────
    [Header("Çevre Absorbe (Parklar İçin)")]
    [Tooltip("Her tick'te çevresindeki kirliliği ne kadar emer")]
    public float pollutionAbsorptionPerTick = 0f;
    [Tooltip("Gürültüyü ekstra ne kadar azaltır (dB) — GDD: Ağaç bariyeri")]
    public float noiseAbsorptionBonus = 0f;

    // ─── Blockout Görsel ────────────────────────────────────────
    [Header("Blockout Görsel (Geçici)")]
    [Tooltip("Blockout küpün rengi — bina tipine göre farklılık gösterir")]
    public Color blockoutColor = Color.gray;
    [Tooltip("Blockout küpün yüksekliği (birim)")]
    public float blockoutHeight = 1f;

    // ─── Upgrade (Konut İçin) ───────────────────────────────────
    [Header("Seviye Sistemi")]
    public bool canUpgrade = false;
    public int maxLevel = 1;

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCI PROPERTYLER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Bina enerji üretiyor mu?</summary>
    public bool IsEnergyProducer => energyDelta > 0f;

    /// <summary>Bina su üretiyor mu?</summary>
    public bool IsWaterProducer => waterDelta > 0f;

    /// <summary>Bina kirlilik üretiyor mu?</summary>
    public bool ProducesPollution =>
        airPollutionPerTick > 0f || waterPollutionPerTick > 0f ||
        soilPollutionPerTick > 0f || carbonEmissionPerTick > 0f;

    /// <summary>Toplam tile sayısı (gridWidth x gridHeight).</summary>
    public int TotalTiles => gridWidth * gridHeight;
}
