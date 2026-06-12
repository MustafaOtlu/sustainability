using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Otomatik test botu. Şehri mantıklı bir stratejiyle kurar ve
/// her kararını Debug.Log ile açıklar.
/// 
/// Strateji Sırası:
///   1. Enerji altyapısı (Kömür veya Güneş)
///   2. Su altyapısı (Su Pompası)
///   3. Konutlar (nüfus barındırmak için)
///   4. Sanayi (vergi geliri için)
///   5. Parklar (kirlilik dengeleme)
///   6. Yollar (binalar arasına)
///   7. Sosyal binalar (memnuniyet artırma)
///   8. Kriz müdahalesi (otomatik)
/// </summary>
public class AutoTestBot : MonoBehaviour
{
    [Header("Bot Ayarları")]
    [Tooltip("Bot kaç tick'te bir aksiyon alsın")]
    [SerializeField] private int actionInterval = 2;

    [Tooltip("Botun aktif olup olmadığı")]
    [SerializeField] private bool botEnabled = true;

    [Header("Yerleşim Alanı")]
    [Tooltip("Binaların yerleştirileceği grid bölgesi (başlangıç X)")]
    [SerializeField] private int buildZoneStartX = 30;
    [SerializeField] private int buildZoneStartZ = 30;
    [SerializeField] private int buildZoneWidth = 40;
    [SerializeField] private int buildZoneHeight = 40;

    // ─── Durum Takibi ───────────────────────────────────────────
    private int tickCounter = 0;
    private int totalActions = 0;
    private BotPhase currentPhase = BotPhase.Infrastructure;

    // Bina referansları (isimle bulacağız)
    private BuildingData coalPlant;
    private BuildingData solarFarm;
    private BuildingData waterPump;
    private BuildingData smallHouse;
    private BuildingData apartment;
    private BuildingData heavyIndustry;
    private BuildingData ecoFactory;
    private BuildingData smallPark;
    private BuildingData cultureCenter;
    private BuildingData hospital;

    // Yerleştirme kursörü
    private int cursorX;
    private int cursorZ;

    private enum BotPhase
    {
        Infrastructure,  // Enerji + Su
        Housing,         // Konutlar
        Industry,        // Fabrikalar
        Roads,           // Yollar
        Environment,     // Parklar
        Social,          // Kültür Merkezi + Hastane
        Optimization,    // Dengeleme ve kriz yönetimi
        Done             // Test tamamlandı
    }

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Start()
    {
        if (!botEnabled) return;

        cursorX = buildZoneStartX;
        cursorZ = buildZoneStartZ;

        // Bina verilerini bul
        FindBuildingData();

        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += OnTick;

        LogBot("═══════════════════════════════════════════════════════");
        LogBot("🤖 OTOMATİK TEST BOTU AKTİF");
        LogBot($"   Yapı alanı: ({buildZoneStartX},{buildZoneStartZ}) → " +
               $"({buildZoneStartX + buildZoneWidth},{buildZoneStartZ + buildZoneHeight})");
        LogBot("   Strateji: Altyapı → Konut → Sanayi → Yol → Park → Sosyal");
        LogBot("═══════════════════════════════════════════════════════");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= OnTick;
    }

    // ═══════════════════════════════════════════════════════════════
    // BİNA VERİLERİNİ BUL
    // ═══════════════════════════════════════════════════════════════

    private void FindBuildingData()
    {
        if (BuildingPlacer.Instance == null || BuildingPlacer.Instance.AvailableBuildings == null)
        {
            LogBot("⚠ BuildingPlacer bulunamadı veya bina listesi boş!");
            botEnabled = false;
            return;
        }

        foreach (var b in BuildingPlacer.Instance.AvailableBuildings)
        {
            if (b == null) continue;
            string name = b.buildingName.ToLowerInvariant();

            // Kategori + özellik bazlı eşleştirme (isim bağımsız)
            switch (b.category)
            {
                case BuildingData.BuildingCategory.Power:
                    if (b.carbonEmissionPerTick > 0 && coalPlant == null)
                        coalPlant = b;   // Kirli enerji = kömür
                    else if (b.carbonEmissionPerTick <= 0 && solarFarm == null)
                        solarFarm = b;   // Temiz enerji = güneş
                    break;
                case BuildingData.BuildingCategory.Water:
                    if (waterPump == null) waterPump = b;
                    break;
                case BuildingData.BuildingCategory.Residential:
                    if (b.gridWidth == 1 && b.gridHeight == 1 && smallHouse == null)
                        smallHouse = b;  // Küçük konut (1x1)
                    else if ((b.gridWidth > 1 || b.gridHeight > 1) && apartment == null)
                        apartment = b;   // Büyük konut (2x2+)
                    break;
                case BuildingData.BuildingCategory.Industrial:
                    if (b.carbonEmissionPerTick > 50 && heavyIndustry == null)
                        heavyIndustry = b; // Yüksek karbon = ağır sanayi
                    else if (b.carbonEmissionPerTick <= 50 && ecoFactory == null)
                        ecoFactory = b;    // Düşük karbon = eko fabrika
                    break;
                case BuildingData.BuildingCategory.Environmental:
                    if (smallPark == null) smallPark = b;
                    break;
                case BuildingData.BuildingCategory.Social:
                    if (cultureCenter == null) cultureCenter = b;
                    break;
                case BuildingData.BuildingCategory.Healthcare:
                    if (hospital == null) hospital = b;
                    break;
            }
        }

        // İsim bazlı fallback (eğer kategoriler dolduramadıysa)
        foreach (var b in BuildingPlacer.Instance.AvailableBuildings)
        {
            if (b == null) continue;
            string name = b.buildingName.ToLowerInvariant();
            if (coalPlant == null && (name.Contains("kömür") || name.Contains("termik"))) coalPlant = b;
            if (solarFarm == null && (name.Contains("güneş") || name.Contains("solar"))) solarFarm = b;
            if (waterPump == null && (name.Contains("pompa") || name.Contains("su"))) waterPump = b;
            if (smallHouse == null && (name.Contains("küçük") || name.Contains("konut"))) smallHouse = b;
            if (apartment == null && name.Contains("apartman")) apartment = b;
            if (heavyIndustry == null && (name.Contains("ağır") || name.Contains("sanayi"))) heavyIndustry = b;
            if (ecoFactory == null && (name.Contains("eko") || name.Contains("yeşil fabrika"))) ecoFactory = b;
            if (smallPark == null && name.Contains("park")) smallPark = b;
            if (cultureCenter == null && name.Contains("kültür")) cultureCenter = b;
            if (hospital == null && name.Contains("hastane")) hospital = b;
        }

        int found = new[] { coalPlant, solarFarm, waterPump, smallHouse, apartment,
                            heavyIndustry, ecoFactory, smallPark, cultureCenter, hospital }
                    .Count(x => x != null);

        LogBot($"📋 {found}/10 bina tipi bulundu.");
        if (coalPlant != null) LogBot($"   ⚡ Kömür: {coalPlant.buildingName}");
        if (solarFarm != null) LogBot($"   ☀️ Güneş: {solarFarm.buildingName}");
        if (waterPump != null) LogBot($"   💧 Su: {waterPump.buildingName}");
        if (smallHouse != null) LogBot($"   🏠 Konut: {smallHouse.buildingName}");
        if (heavyIndustry != null) LogBot($"   🏭 Sanayi: {heavyIndustry.buildingName}");
        if (ecoFactory != null) LogBot($"   ♻️ EkoFabrika: {ecoFactory.buildingName}");
        if (smallPark != null) LogBot($"   🌳 Park: {smallPark.buildingName}");

        if (found < 3)
        {
            LogBot("⚠ Yeterli bina tipi bulunamadı! Bot devre dışı.");
            botEnabled = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK İŞLEME
    // ═══════════════════════════════════════════════════════════════

    private void OnTick()
    {
        if (!botEnabled) return;

        tickCounter++;

        // Her N tick'te bir aksiyon al
        if (tickCounter % actionInterval != 0) return;

        // Durum raporu (her 10 aksiyonda)
        if (totalActions % 10 == 0 && totalActions > 0)
        {
            PrintStatusReport();
        }

        // Kriz kontrolü (her zaman öncelikli)
        if (HandleCrisis()) return;

        // Normal faz ilerlemesi
        switch (currentPhase)
        {
            case BotPhase.Infrastructure:
                DoInfrastructure();
                break;
            case BotPhase.Housing:
                DoHousing();
                break;
            case BotPhase.Industry:
                DoIndustry();
                break;
            case BotPhase.Roads:
                DoRoads();
                break;
            case BotPhase.Environment:
                DoEnvironment();
                break;
            case BotPhase.Social:
                DoSocial();
                break;
            case BotPhase.Optimization:
                DoOptimization();
                break;
            case BotPhase.Done:
                // Son rapor
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 1: ALTYAPI (Enerji + Su)
    // ═══════════════════════════════════════════════════════════════

    private int infrastructureStep = 0;

    private void DoInfrastructure()
    {
        switch (infrastructureStep)
        {
            case 0:
                LogBot("───── FAZ 1: ALTYAPI ─────");
                LogBot("📌 Önce enerji santrali kurulacak (şehrin can damarı).");
                if (coalPlant != null && TryPlace(coalPlant, buildZoneStartX, buildZoneStartZ + buildZoneHeight - 5))
                {
                    LogBot("✅ Kömür Santrali kuruldu (+100 MW). Kirlilik yüksek ama başlangıç için şart.");
                    infrastructureStep++;
                }
                break;

            case 1:
                LogBot("📌 Su pompası kurulacak (sağlık için kritik).");
                if (waterPump != null && TryPlace(waterPump, buildZoneStartX + 5, buildZoneStartZ + buildZoneHeight - 5))
                {
                    LogBot("✅ Su Pompası kuruldu (+30 m³). Temel ihtiyaçlar karşılanıyor.");
                    infrastructureStep++;
                }
                break;

            case 2:
                LogBot("✅ Altyapı tamam! Enerji ve su hazır. Konut fazına geçiliyor.");
                AdvancePhase(BotPhase.Housing);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 2: KONUTLAR
    // ═══════════════════════════════════════════════════════════════

    private int housesBuilt = 0;
    private int targetHouses = 8;

    private void DoHousing()
    {
        if (housesBuilt == 0)
        {
            LogBot("───── FAZ 2: KONUT ─────");
            LogBot($"📌 {targetHouses} adet küçük konut yerleştirilecek (nüfus çekmek için).");
        }

        if (housesBuilt >= targetHouses)
        {
            LogBot($"✅ {housesBuilt} konut yerleştirildi. Toplam kapasite: {ResourceSystem.Instance?.PopulationCapacity ?? 0}");
            LogBot("   Sanayi fazına geçiliyor (vergi geliri gerekli).");
            AdvancePhase(BotPhase.Industry);
            return;
        }

        // Konutları enerji santralinden uzağa yerleştir (kirlilik!)
        int houseX = buildZoneStartX + 15 + (housesBuilt % 4) * 2;
        int houseZ = buildZoneStartZ + 5 + (housesBuilt / 4) * 2;

        BuildingData houseType = (housesBuilt % 3 == 2 && apartment != null) ? apartment : smallHouse;

        if (houseType != null && TryPlace(houseType, houseX, houseZ))
        {
            housesBuilt++;
            LogBot($"🏠 [{housesBuilt}/{targetHouses}] {houseType.buildingName} yerleştirildi → ({houseX},{houseZ})");
        }
        else
        {
            // Pozisyon doluysa bir sonraki boş yeri dene
            Vector2Int freeSpot = FindFreeSpot(houseType != null ? houseType.gridWidth : 1,
                                               houseType != null ? houseType.gridHeight : 1,
                                               buildZoneStartX + 12, buildZoneStartZ + 3, 20, 15);
            if (freeSpot.x >= 0 && houseType != null && TryPlace(houseType, freeSpot.x, freeSpot.y))
            {
                housesBuilt++;
                LogBot($"🏠 [{housesBuilt}/{targetHouses}] {houseType.buildingName} yerleştirildi → ({freeSpot.x},{freeSpot.y}) (alternatif konum)");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 3: SANAYİ
    // ═══════════════════════════════════════════════════════════════

    private int factoriesBuilt = 0;
    private int targetFactories = 3;

    private void DoIndustry()
    {
        if (factoriesBuilt == 0)
        {
            LogBot("───── FAZ 3: SANAYİ ─────");
            LogBot("📌 Fabrikalar yerleştirilecek (vergi geliri için). Konutlardan UZAĞA!");
        }

        if (factoriesBuilt >= targetFactories)
        {
            float income = ResourceSystem.Instance?.IncomePerTick ?? 0;
            float expense = ResourceSystem.Instance?.ExpensePerTick ?? 0;
            LogBot($"✅ {factoriesBuilt} fabrika kuruldu. Gelir: +{income:F0}/tick, Gider: -{expense:F0}/tick");
            AdvancePhase(BotPhase.Roads);
            return;
        }

        // Fabrikaları haritanın diğer köşesine koy (konutlardan uzak)
        int factX = buildZoneStartX + (factoriesBuilt * 5);
        int factZ = buildZoneStartZ + buildZoneHeight - 12;

        BuildingData factoryType = (factoriesBuilt == 2 && ecoFactory != null &&
                                    ResourceSystem.Instance != null &&
                                    ResourceSystem.Instance.CanAfford(ecoFactory.buildCost))
                                    ? ecoFactory : heavyIndustry;

        if (factoryType != null)
        {
            Vector2Int spot = FindFreeSpot(factoryType.gridWidth, factoryType.gridHeight,
                                           factX, factZ, 15, 10);
            if (spot.x >= 0 && TryPlace(factoryType, spot.x, spot.y))
            {
                factoriesBuilt++;
                LogBot($"🏭 [{factoriesBuilt}/{targetFactories}] {factoryType.buildingName} → ({spot.x},{spot.y})");

                if (factoryType == heavyIndustry)
                    LogBot("   ⚠ Ağır sanayi kirlilik yayar! Park ile dengelenecek.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 4: YOLLAR
    // ═══════════════════════════════════════════════════════════════

    private int roadsBuilt = 0;
    private int targetRoads = 20;

    private void DoRoads()
    {
        if (roadsBuilt == 0)
        {
            LogBot("───── FAZ 4: YOLLAR ─────");
            LogBot("📌 Konutlar ile fabrikalar arasına ana yol çekilecek.");
        }

        if (roadsBuilt >= targetRoads || RoadSystem.Instance == null)
        {
            LogBot($"✅ {roadsBuilt} yol tile'ı yerleştirildi.");
            AdvancePhase(BotPhase.Environment);
            return;
        }

        // Ana cadde: konut bölgesinden sanayi bölgesine dikey yol
        int roadX = buildZoneStartX + 12;
        int roadZ = buildZoneStartZ + 3 + roadsBuilt;

        if (roadZ < buildZoneStartZ + buildZoneHeight - 3)
        {
            if (RoadSystem.Instance.PlaceRoad(roadX, roadZ))
            {
                roadsBuilt++;
                if (roadsBuilt % 5 == 0)
                    LogBot($"🛣️ Yol ilerlemesi: {roadsBuilt}/{targetRoads} tile");
            }
            else
            {
                roadsBuilt++; // Atlayıp devam et
            }
        }
        else
        {
            LogBot($"✅ Yol tamamlandı ({roadsBuilt} tile).");
            AdvancePhase(BotPhase.Environment);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 5: ÇEVRE (Parklar)
    // ═══════════════════════════════════════════════════════════════

    private int parksBuilt = 0;
    private int targetParks = 6;

    private void DoEnvironment()
    {
        if (parksBuilt == 0)
        {
            LogBot("───── FAZ 5: ÇEVRE ─────");
            LogBot("📌 Parklar yerleştirilecek — fabrika kirliliğini absorbe etmek için.");
            LogBot($"   Şu anki kirlilik: {PollutionSystem.Instance?.AverageAirPollution:F1}");
        }

        if (parksBuilt >= targetParks)
        {
            LogBot($"✅ {parksBuilt} park yerleştirildi. Kirlilik: {PollutionSystem.Instance?.AverageAirPollution:F1}");
            AdvancePhase(BotPhase.Social);
            return;
        }

        if (smallPark == null) { AdvancePhase(BotPhase.Social); return; }

        // Parkları fabrikaların yanına ve konutların arasına koy
        Vector2Int spot = FindFreeSpot(1, 1, buildZoneStartX + 8, buildZoneStartZ + 3, 25, 30);

        if (spot.x >= 0 && TryPlace(smallPark, spot.x, spot.y))
        {
            parksBuilt++;
            LogBot($"🌳 [{parksBuilt}/{targetParks}] Park → ({spot.x},{spot.y})");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 6: SOSYAL BİNALAR
    // ═══════════════════════════════════════════════════════════════

    private bool hospitalBuilt = false;
    private bool cultureBuilt = false;

    private void DoSocial()
    {
        LogBot("───── FAZ 6: SOSYAL ─────");

        if (!hospitalBuilt && hospital != null && ResourceSystem.Instance.CanAfford(hospital.buildCost))
        {
            Vector2Int spot = FindFreeSpot(hospital.gridWidth, hospital.gridHeight,
                                           buildZoneStartX + 15, buildZoneStartZ + 12, 15, 10);
            if (spot.x >= 0 && TryPlace(hospital, spot.x, spot.y))
            {
                hospitalBuilt = true;
                LogBot($"🏥 Hastane kuruldu → ({spot.x},{spot.y}). Şehir sağlığı artacak.");
            }
        }

        if (!cultureBuilt && cultureCenter != null && ResourceSystem.Instance.CanAfford(cultureCenter.buildCost))
        {
            Vector2Int spot = FindFreeSpot(cultureCenter.gridWidth, cultureCenter.gridHeight,
                                           buildZoneStartX + 18, buildZoneStartZ + 8, 12, 10);
            if (spot.x >= 0 && TryPlace(cultureCenter, spot.x, spot.y))
            {
                cultureBuilt = true;
                LogBot($"🎭 Kültür Merkezi kuruldu → ({spot.x},{spot.y}). Mutluluk +20 bölgesel.");
            }
        }

        if (hospitalBuilt || cultureBuilt || totalActions > 50)
        {
            AdvancePhase(BotPhase.Optimization);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FAZ 7: OPTİMİZASYON
    // ═══════════════════════════════════════════════════════════════

    private int optimizationTicks = 0;

    private void DoOptimization()
    {
        if (optimizationTicks == 0)
        {
            LogBot("───── FAZ 7: OPTİMİZASYON ─────");
            LogBot("📌 Şehir dengeleniyor. Eksiklere göre aksiyon alınacak.");
        }

        optimizationTicks++;

        // 30 tick boyunca gözlem yap
        if (optimizationTicks > 30)
        {
            PrintFinalReport();
            AdvancePhase(BotPhase.Done);
            botEnabled = false;
            return;
        }

        // Enerji eksikse santral ekle
        if (ResourceSystem.Instance != null && ResourceSystem.Instance.IsBlackout)
        {
            LogBot("⚡ Enerji açığı var — ek santral kurulacak.");
            BuildingData plant = (ResourceSystem.Instance.CanAfford(solarFarm?.buildCost ?? 99999))
                ? solarFarm : coalPlant;
            if (plant != null)
            {
                Vector2Int spot = FindFreeSpot(plant.gridWidth, plant.gridHeight,
                                               buildZoneStartX, buildZoneStartZ + buildZoneHeight - 8, 20, 6);
                if (spot.x >= 0) TryPlace(plant, spot.x, spot.y);
            }
        }

        // Kirlilik yüksekse park ekle
        if (PollutionSystem.Instance != null && PollutionSystem.Instance.AverageAirPollution > 3f)
        {
            if (smallPark != null && ResourceSystem.Instance.CanAfford(smallPark.buildCost))
            {
                Vector2Int spot = FindFreeSpot(1, 1, buildZoneStartX + 5, buildZoneStartZ + 5, 30, 25);
                if (spot.x >= 0 && TryPlace(smallPark, spot.x, spot.y))
                {
                    LogBot($"🌳 Ekstra park eklendi (kirlilik: {PollutionSystem.Instance.AverageAirPollution:F1})");
                }
            }
        }

        // Konut yetersizse ekle
        if (ResourceSystem.Instance != null &&
            ResourceSystem.Instance.Population >= ResourceSystem.Instance.PopulationCapacity &&
            ResourceSystem.Instance.PopulationCapacity > 0)
        {
            BuildingData house = (ResourceSystem.Instance.CanAfford(apartment?.buildCost ?? 99999))
                ? apartment : smallHouse;
            if (house != null)
            {
                Vector2Int spot = FindFreeSpot(house.gridWidth, house.gridHeight,
                                               buildZoneStartX + 12, buildZoneStartZ + 3, 20, 15);
                if (spot.x >= 0 && TryPlace(house, spot.x, spot.y))
                {
                    LogBot($"🏠 Ekstra konut eklendi (nüfus kapasitesi doluydu).");
                }
            }
        }

        // Her 10 tick'te durum raporu
        if (optimizationTicks % 10 == 0) PrintStatusReport();
    }

    // ═══════════════════════════════════════════════════════════════
    // KRİZ YÖNETİMİ
    // ═══════════════════════════════════════════════════════════════

    private bool HandleCrisis()
    {
        if (ResourceSystem.Instance == null) return false;

        // İflas tehlikesi
        if (ResourceSystem.Instance.Money < 0)
        {
            LogBot($"🚨 KRİZ: Kasa negatif ({ResourceSystem.Instance.Money:F0})! " +
                   "Giderleri kısmak gerekebilir.");
            return false; // Devam et ama uyar
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCI METODLAR
    // ═══════════════════════════════════════════════════════════════

    private bool TryPlace(BuildingData data, int x, int z)
    {
        if (BuildingPlacer.Instance == null || GridSystem.Instance == null) return false;
        if (ResourceSystem.Instance != null && !ResourceSystem.Instance.CanAfford(data.buildCost)) return false;
        if (!GridSystem.Instance.IsAreaEmpty(x, z, data.gridWidth, data.gridHeight)) return false;

        // Para harca
        ResourceSystem.Instance?.SpendMoney(data.buildCost);

        // Yerleştir
        BuildingPlacer.Instance.PlaceBuilding(x, z, data);
        totalActions++;
        return true;
    }

    /// <summary>Belirtilen alanda boş bir yer bul.</summary>
    private Vector2Int FindFreeSpot(int w, int h, int searchX, int searchZ, int searchW, int searchH)
    {
        if (GridSystem.Instance == null) return new Vector2Int(-1, -1);

        for (int z = searchZ; z < searchZ + searchH; z += h)
        {
            for (int x = searchX; x < searchX + searchW; x += w)
            {
                if (GridSystem.Instance.IsAreaEmpty(x, z, w, h))
                    return new Vector2Int(x, z);
            }
        }
        return new Vector2Int(-1, -1);
    }

    private void AdvancePhase(BotPhase newPhase)
    {
        currentPhase = newPhase;
        LogBot($"═══ Faz değişti: {newPhase} ═══");
    }

    // ═══════════════════════════════════════════════════════════════
    // RAPORLAMA
    // ═══════════════════════════════════════════════════════════════

    private void PrintStatusReport()
    {
        LogBot("┌─────────── DURUM RAPORU ───────────┐");
        LogBot($"│ Gün: {GameManager.Instance?.CurrentDay ?? 0}  |  Faz: {currentPhase}");

        if (ResourceSystem.Instance != null)
        {
            var rs = ResourceSystem.Instance;
            LogBot($"│ 💰 Para: {rs.Money:F0} (net: {rs.NetIncomePerTick:F0}/tick)");
            LogBot($"│ ⚡ Enerji: {rs.EnergyProduction:F0}/{rs.EnergyConsumption:F0} MW {(rs.IsBlackout ? "❌KARARTMA" : "✅")}");
            LogBot($"│ 💧 Su: {rs.WaterProduction:F0}/{rs.WaterConsumption:F0} m³ {(rs.IsWaterShortage ? "❌KESİNTİ" : "✅")}");
            LogBot($"│ 👤 Nüfus: {rs.Population}/{rs.PopulationCapacity}");
        }

        if (PollutionSystem.Instance != null)
        {
            var ps = PollutionSystem.Instance;
            LogBot($"│ 🌫 Kirlilik: {ps.AverageAirPollution:F2} | Karbon: {ps.GlobalCarbonEmission:F0} | Kirli: {ps.PollutedAreaPercentage:F1}%");
        }

        if (NoiseSystem.Instance != null)
            LogBot($"│ 🔊 Gürültü: {NoiseSystem.Instance.AverageNoise:F1} dB");

        if (CitizenSystem.Instance != null)
        {
            var cs = CitizenSystem.Instance;
            LogBot($"│ 😊 Memnuniyet: {cs.GlobalSatisfaction:F1}% | Sağlık: {cs.GlobalHealth:F1} | Mutluluk: {cs.GlobalHappiness:F1}");
            LogBot($"│ 🚶 Son göç: {cs.LastMigration:+0;-0;0}");
        }

        if (RoadSystem.Instance != null && RoadSystem.Instance.TotalRoads > 0)
            LogBot($"│ 🛣️ Yollar: {RoadSystem.Instance.TotalRoads} | Trafik: {RoadSystem.Instance.AverageCongestion:P0}");

        LogBot($"│ 📊 Toplam aksiyon: {totalActions}");
        LogBot("└────────────────────────────────────┘");
    }

    private void PrintFinalReport()
    {
        LogBot("╔═══════════════════════════════════════════════════╗");
        LogBot("║           🏆 TEST TAMAMLANDI — SONUÇ RAPORU       ║");
        LogBot("╠═══════════════════════════════════════════════════╣");

        PrintStatusReport();

        // Değerlendirme
        float score = 0f;
        string verdict = "";

        if (ResourceSystem.Instance != null)
        {
            if (ResourceSystem.Instance.Money > 0) score += 20f;
            if (!ResourceSystem.Instance.IsBlackout) score += 20f;
            if (!ResourceSystem.Instance.IsWaterShortage) score += 15f;
            if (ResourceSystem.Instance.Population > 20) score += 15f;
        }

        if (CitizenSystem.Instance != null)
        {
            if (CitizenSystem.Instance.GlobalSatisfaction > 50) score += 15f;
            if (CitizenSystem.Instance.GlobalSatisfaction > 70) score += 15f;
        }

        if (score >= 80) verdict = "🏆 MÜKEMMEL — Sürdürülebilir şehir!";
        else if (score >= 60) verdict = "✅ İYİ — Dengeli gelişim.";
        else if (score >= 40) verdict = "⚠ ORTA — İyileştirme gerekli.";
        else verdict = "❌ KÖTÜ — Sistemlerde sorun var.";

        LogBot($"║ PUAN: {score:F0}/100 — {verdict}");
        LogBot("╚═══════════════════════════════════════════════════╝");
    }

    private void LogBot(string message)
    {
        Debug.Log($"[🤖 BOT] {message}");
    }
}
