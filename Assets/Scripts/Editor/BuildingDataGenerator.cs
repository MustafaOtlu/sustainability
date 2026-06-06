#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editör yardımcı scripti: GDD'deki tüm bina tiplerinin ScriptableObject asset'lerini
/// tek tıklamayla otomatik oluşturur.
/// Unity menüsünden: Tools → Sustainability → Bina Asset'lerini Oluştur
/// </summary>
public class BuildingDataGenerator
{
    private const string BasePath = "Assets/Data/Buildings";

    [MenuItem("Tools/Sustainability/Bina Assetlerini Olustur")]
    public static void GenerateAllBuildingAssets()
    {
        // Klasör yoksa oluştur
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Buildings"))
            AssetDatabase.CreateFolder("Assets/Data", "Buildings");

        int created = 0;

        // ═══════════════════════════════════════════════════════════
        // KONUT BİNALARI (GDD 5.1)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("KucukKonut", b =>
        {
            b.buildingName = "Küçük Konut";
            b.category = BuildingData.BuildingCategory.Residential;
            b.description = "Tek katlı küçük konut. Az nüfus barındırır, düşük vergi üretir.";
            b.gridWidth = 1; b.gridHeight = 1;
            b.buildCost = 500;
            b.maintenanceCostPerTick = 0;
            b.energyDelta = -2f;
            b.waterDelta = -1f;
            b.taxIncomePerTick = 20f;
            b.populationCapacity = 5;
            b.noiseLevel = 5f;
            b.blockoutColor = new Color(0.55f, 0.75f, 0.95f); // Açık mavi
            b.blockoutHeight = 0.8f;
            b.canUpgrade = true;
            b.maxLevel = 3;
        });

        created += CreateBuilding("Apartman", b =>
        {
            b.buildingName = "Apartman";
            b.category = BuildingData.BuildingCategory.Residential;
            b.description = "Çok katlı konut binası. Daha fazla nüfus, daha fazla vergi.";
            b.gridWidth = 2; b.gridHeight = 2;
            b.buildCost = 2500;
            b.maintenanceCostPerTick = 10;
            b.energyDelta = -8f;
            b.waterDelta = -4f;
            b.taxIncomePerTick = 80f;
            b.populationCapacity = 20;
            b.noiseLevel = 10f;
            b.blockoutColor = new Color(0.35f, 0.55f, 0.85f); // Mavi
            b.blockoutHeight = 2.5f;
            b.canUpgrade = true;
            b.maxLevel = 3;
        });

        // ═══════════════════════════════════════════════════════════
        // SANAYİ BİNALARI (GDD 5.2)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("AgirSanayi", b =>
        {
            b.buildingName = "Ağır Sanayi";
            b.category = BuildingData.BuildingCategory.Industrial;
            b.description = "Yüksek vergi geliri sağlar. Ağır kirlilik ve 80 dB gürültü yayar.";
            b.gridWidth = 3; b.gridHeight = 3;
            b.buildCost = 3000;
            b.maintenanceCostPerTick = 30;
            b.energyDelta = -15f;
            b.waterDelta = -5f;
            b.taxIncomePerTick = 150f;
            b.airPollutionPerTick = 30f;
            b.waterPollutionPerTick = 15f;
            b.soilPollutionPerTick = 5f;
            b.carbonEmissionPerTick = 20f;
            b.noiseLevel = 80f;
            b.blockoutColor = new Color(0.35f, 0.35f, 0.35f); // Koyu gri
            b.blockoutHeight = 2.0f;
        });

        created += CreateBuilding("EkoFabrika", b =>
        {
            b.buildingName = "Eko-Teknoloji Fabrikası";
            b.category = BuildingData.BuildingCategory.Industrial;
            b.description = "Gelişmiş endüstri. Yüksek vergi, minimum kirlilik. Pahalı yatırım.";
            b.gridWidth = 3; b.gridHeight = 3;
            b.buildCost = 9000;
            b.maintenanceCostPerTick = 50;
            b.energyDelta = -20f;
            b.waterDelta = -3f;
            b.taxIncomePerTick = 180f;
            b.airPollutionPerTick = 3f;
            b.waterPollutionPerTick = 1f;
            b.carbonEmissionPerTick = 2f;
            b.noiseLevel = 20f;
            b.blockoutColor = new Color(0.2f, 0.7f, 0.65f); // Teal
            b.blockoutHeight = 2.2f;
        });

        // ═══════════════════════════════════════════════════════════
        // PARKLAR (GDD 5.3)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("KucukPark", b =>
        {
            b.buildingName = "Küçük Park";
            b.category = BuildingData.BuildingCategory.Environmental;
            b.description = "Kirliliği ve gürültüyü absorbe eder. Yakın konutların mutluluğunu artırır.";
            b.gridWidth = 1; b.gridHeight = 1;
            b.buildCost = 300;
            b.maintenanceCostPerTick = 15;
            b.happinessBonus = 10f;
            b.effectRadius = 4;
            b.pollutionAbsorptionPerTick = 10f;
            b.noiseAbsorptionBonus = 16f; // GDD: Ağaç bariyeri ekstra 16 dB
            b.blockoutColor = new Color(0.2f, 0.75f, 0.25f); // Yeşil
            b.blockoutHeight = 0.4f;
        });

        // ═══════════════════════════════════════════════════════════
        // TOPLUM VE KÜLTÜR (GDD 5.4)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("KulturMerkezi", b =>
        {
            b.buildingName = "Kültür Merkezi";
            b.category = BuildingData.BuildingCategory.Social;
            b.description = "Etki alanındaki vatandaşların mutluluk taban değerini +20 puan artırır.";
            b.gridWidth = 3; b.gridHeight = 2;
            b.buildCost = 4000;
            b.maintenanceCostPerTick = 200;
            b.energyDelta = -5f;
            b.waterDelta = -1f;
            b.happinessBonus = 20f;
            b.effectRadius = 8;
            b.blockoutColor = new Color(0.65f, 0.35f, 0.75f); // Mor
            b.blockoutHeight = 1.8f;
        });

        // ═══════════════════════════════════════════════════════════
        // SAĞLIK (GDD 5.5)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("BolgeHastanesi", b =>
        {
            b.buildingName = "Bölge Hastanesi";
            b.category = BuildingData.BuildingCategory.Healthcare;
            b.description = "Tüm şehrin taban sağlık değerini yükseltir. Ölüm/hastalık oranlarını düşürür.";
            b.gridWidth = 4; b.gridHeight = 3;
            b.buildCost = 6000;
            b.maintenanceCostPerTick = 400;
            b.energyDelta = -10f;
            b.waterDelta = -3f;
            b.healthBonus = 30f;
            b.blockoutColor = new Color(0.9f, 0.9f, 0.95f); // Beyazımsı
            b.blockoutHeight = 2.0f;
        });

        // ═══════════════════════════════════════════════════════════
        // ENERJİ SANTRALLERİ (GDD 5.6)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("KomurSantrali", b =>
        {
            b.buildingName = "Kömür Santrali";
            b.category = BuildingData.BuildingCategory.Power;
            b.description = "Ucuz ve kesintisiz +100 MW enerji. Çok yüksek kirlilik ve karbon salınımı!";
            b.gridWidth = 3; b.gridHeight = 3;
            b.buildCost = 4000;
            b.maintenanceCostPerTick = 100;
            b.energyDelta = 100f;  // +100 MW üretim
            b.waterDelta = -5f;
            b.airPollutionPerTick = 50f;
            b.carbonEmissionPerTick = 40f;
            b.noiseLevel = 40f;
            b.blockoutColor = new Color(0.3f, 0.2f, 0.15f); // Koyu kahverengi
            b.blockoutHeight = 3.0f;
        });

        created += CreateBuilding("GunesTarlasi", b =>
        {
            b.buildingName = "Güneş Tarlası";
            b.category = BuildingData.BuildingCategory.Power;
            b.description = "Temiz enerji +40 MW. Sıfır kirlilik! Yüksek yatırım maliyeti.";
            b.gridWidth = 4; b.gridHeight = 4;
            b.buildCost = 12000;
            b.maintenanceCostPerTick = 50;
            b.energyDelta = 40f;  // +40 MW üretim
            b.airPollutionPerTick = 0f;
            b.carbonEmissionPerTick = 0f;
            b.blockoutColor = new Color(0.95f, 0.85f, 0.2f); // Altın sarısı
            b.blockoutHeight = 0.3f;
        });

        // ═══════════════════════════════════════════════════════════
        // SU ALTYAPISI (GDD 4.1 — detaylar eklendi)
        // ═══════════════════════════════════════════════════════════

        created += CreateBuilding("SuPompasi", b =>
        {
            b.buildingName = "Su Pompası";
            b.category = BuildingData.BuildingCategory.Water;
            b.description = "Şehre su sağlar (+30 m³). Yanındaki su kirliliği verimini düşürür.";
            b.gridWidth = 2; b.gridHeight = 2;
            b.buildCost = 2000;
            b.maintenanceCostPerTick = 50;
            b.energyDelta = -3f;
            b.waterDelta = 30f;  // +30 m³ üretim
            b.noiseLevel = 15f;
            b.blockoutColor = new Color(0.3f, 0.6f, 0.9f); // Su mavisi
            b.blockoutHeight = 1.2f;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BuildingDataGenerator] ✅ {created} bina asset'i oluşturuldu: {BasePath}");
        EditorUtility.DisplayDialog(
            "Bina Asset'leri",
            $"{created} adet bina ScriptableObject'i başarıyla oluşturuldu!\n\nKonum: {BasePath}",
            "Tamam"
        );
    }

    /// <summary>
    /// Tek bir BuildingData ScriptableObject oluşturur.
    /// Zaten varsa atlar (üzerine yazmaz).
    /// </summary>
    private static int CreateBuilding(string fileName, System.Action<BuildingData> configure)
    {
        string assetPath = $"{BasePath}/{fileName}.asset";

        // Zaten varsa atla
        if (AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath) != null)
        {
            Debug.Log($"[BuildingDataGenerator] ⏭ {fileName} zaten mevcut, atlanıyor.");
            return 0;
        }

        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        configure(data);

        AssetDatabase.CreateAsset(data, assetPath);
        Debug.Log($"[BuildingDataGenerator] ✨ {data.buildingName} oluşturuldu.");
        return 1;
    }
}
#endif
