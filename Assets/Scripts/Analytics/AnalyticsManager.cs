using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Oyuncu davranış kaydı ve şehir metrikleri toplama sistemi.
/// GDD LA-GTSDD: Learning Analytics & Game Telemetry
/// 
/// Her tick'te şehir metriklerini loglar.
/// Oyuncu aksiyonlarını (bina yerleştirme, yıkım, yol çekme) kaydeder.
/// Oyun sonunda CSV rapor dosyası üretir.
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [Header("Ayarlar")]
    [SerializeField] private bool analyticsEnabled = true;
    [Tooltip("Kaç tick'te bir metrik kaydedilsin")]
    [SerializeField] private int recordInterval = 5;

    // ─── Veri Yapıları ──────────────────────────────────────────

    [Serializable]
    public class MetricSnapshot
    {
        public int day;
        public float money;
        public float netIncome;
        public int population;
        public float satisfaction;
        public float health;
        public float airPollution;
        public float carbonEmission;
        public float avgNoise;
        public float avgCongestion;
        public int totalBuildings;
        public int totalRoads;
    }

    [Serializable]
    public class PlayerAction
    {
        public int day;
        public string actionType; // "build", "demolish", "road", "quest"
        public string detail;
        public float timestamp;
    }

    private List<MetricSnapshot> metricHistory = new List<MetricSnapshot>();
    private List<PlayerAction> actionHistory = new List<PlayerAction>();

    // ─── Oyuncu Profili ─────────────────────────────────────────
    public int TotalBuildingsPlaced { get; private set; }
    public int TotalBuildingsDemolished { get; private set; }
    public int TotalRoadsPlaced { get; private set; }
    public int GreenBuildingsPlaced { get; private set; } // Park, Güneş Tarlası
    public int DirtyBuildingsPlaced { get; private set; } // Kömür, Ağır Sanayi

    /// <summary>Oyuncunun profil tipi.</summary>
    public string PlayerProfile
    {
        get
        {
            if (GreenBuildingsPlaced > DirtyBuildingsPlaced * 2) return "Ekolog";
            if (DirtyBuildingsPlaced > GreenBuildingsPlaced * 2) return "Sanayici";
            return "Dengeli";
        }
    }

    // ─── Dosya Yolları ──────────────────────────────────────────
    private string MetricsFilePath => Path.Combine(Application.persistentDataPath, "analytics_metrics.csv");
    private string ActionsFilePath => Path.Combine(Application.persistentDataPath, "analytics_actions.csv");

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (!analyticsEnabled) return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += OnTick;

        // Bina yerleştirme/yıkım eventleri dinle
        if (BuildingPlacer.Instance != null)
        {
            BuildingPlacer.Instance.OnBuildingPlaced += OnBuildingPlaced;
            BuildingPlacer.Instance.OnBuildingDemolished += OnBuildingDemolished;
        }

        // Yol eventleri
        if (RoadSystem.Instance != null)
        {
            RoadSystem.Instance.OnRoadPlaced += OnRoadPlaced;
        }

        // Görev eventleri
        if (QuestSystem.Instance != null)
        {
            QuestSystem.Instance.OnQuestCompleted += OnQuestCompleted;
        }

        // Oyun sonu - rapor yaz
        if (GameOverSystem.Instance != null)
        {
            GameOverSystem.Instance.OnGameEnded += OnGameEnded;
        }

        Debug.Log($"[Analytics] 📊 Analitik sistemi aktif. Kayıt aralığı: her {recordInterval} tick");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= OnTick;

        // Dosyaya yaz (güvenli çıkış)
        if (metricHistory.Count > 0)
            ExportToCSV();
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK KAYDI
    // ═══════════════════════════════════════════════════════════════

    private void OnTick()
    {
        if (!analyticsEnabled) return;
        if (GameManager.Instance == null) return;

        int day = GameManager.Instance.CurrentDay;
        if (day % recordInterval != 0) return;

        // Snapshot al
        MetricSnapshot snapshot = new MetricSnapshot
        {
            day = day,
            money = ResourceSystem.Instance?.Money ?? 0,
            netIncome = ResourceSystem.Instance?.NetIncomePerTick ?? 0,
            population = ResourceSystem.Instance?.Population ?? 0,
            satisfaction = CitizenSystem.Instance?.GlobalSatisfaction ?? 0,
            health = CitizenSystem.Instance?.GlobalHealth ?? 0,
            airPollution = PollutionSystem.Instance?.AverageAirPollution ?? 0,
            carbonEmission = PollutionSystem.Instance?.GlobalCarbonEmission ?? 0,
            avgNoise = NoiseSystem.Instance?.AverageNoise ?? 0,
            avgCongestion = RoadSystem.Instance?.AverageCongestion ?? 0,
            totalBuildings = BuildingPlacer.Instance?.PlacedBuildings.Count ?? 0,
            totalRoads = RoadSystem.Instance?.TotalRoads ?? 0
        };

        metricHistory.Add(snapshot);
    }

    // ═══════════════════════════════════════════════════════════════
    // AKSİYON KAYDI
    // ═══════════════════════════════════════════════════════════════

    private void OnBuildingPlaced(BuildingInstance building)
    {
        if (building?.Data == null) return;

        TotalBuildingsPlaced++;

        // Yeşil/Kirli sınıflandırma
        if (building.Data.carbonEmissionPerTick <= 0 &&
            (building.Data.category == BuildingData.BuildingCategory.Environmental ||
             building.Data.category == BuildingData.BuildingCategory.Power && building.Data.airPollutionPerTick <= 0))
        {
            GreenBuildingsPlaced++;
        }
        else if (building.Data.carbonEmissionPerTick > 0 || building.Data.airPollutionPerTick > 5)
        {
            DirtyBuildingsPlaced++;
        }

        RecordAction("build", $"{building.Data.buildingName} ({building.GridX},{building.GridZ})");
    }

    private void OnBuildingDemolished(BuildingInstance building)
    {
        TotalBuildingsDemolished++;
        RecordAction("demolish", building?.Data?.buildingName ?? "?");
    }

    private void OnRoadPlaced(RoadSystem.RoadTile road)
    {
        TotalRoadsPlaced++;
        // Yolları toplu logla (spam önleme)
        if (TotalRoadsPlaced % 5 == 0)
            RecordAction("road", $"Toplam {TotalRoadsPlaced} yol");
    }

    private void OnQuestCompleted(QuestSystem.Quest quest)
    {
        RecordAction("quest", $"Tamamlandı: {quest.title}");
    }

    private void RecordAction(string type, string detail)
    {
        actionHistory.Add(new PlayerAction
        {
            day = GameManager.Instance?.CurrentDay ?? 0,
            actionType = type,
            detail = detail,
            timestamp = Time.time
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // OYUN SONU RAPORU
    // ═══════════════════════════════════════════════════════════════

    private void OnGameEnded(GameOverSystem.GameEndState state)
    {
        ExportToCSV();
        PrintAnalyticsReport(state);
    }

    private void PrintAnalyticsReport(GameOverSystem.GameEndState state)
    {
        Debug.Log("[Analytics] ╔═══════════════════════════════════════╗");
        Debug.Log("[Analytics] ║     📊 ÖĞRENME ANALİTİĞİ RAPORU       ║");
        Debug.Log("[Analytics] ╠═══════════════════════════════════════╣");
        Debug.Log($"[Analytics] ║ Oyun Sonu: {state}");
        Debug.Log($"[Analytics] ║ Gün: {GameManager.Instance?.CurrentDay ?? 0}");
        Debug.Log($"[Analytics] ║ Profil: {PlayerProfile}");
        Debug.Log($"[Analytics] ║ Toplam Bina: {TotalBuildingsPlaced} | Yıkılan: {TotalBuildingsDemolished}");
        Debug.Log($"[Analytics] ║ Yeşil: {GreenBuildingsPlaced} | Kirli: {DirtyBuildingsPlaced}");
        Debug.Log($"[Analytics] ║ Yollar: {TotalRoadsPlaced}");
        Debug.Log($"[Analytics] ║ Metrik Kaydı: {metricHistory.Count} snapshot");
        Debug.Log($"[Analytics] ║ Aksiyon Kaydı: {actionHistory.Count} eylem");
        Debug.Log($"[Analytics] ║ CSV: {MetricsFilePath}");
        Debug.Log("[Analytics] ╚═══════════════════════════════════════╝");
    }

    // ═══════════════════════════════════════════════════════════════
    // CSV EXPORT
    // ═══════════════════════════════════════════════════════════════

    private void ExportToCSV()
    {
        try
        {
            // Metrik CSV
            StringBuilder metricCSV = new StringBuilder();
            metricCSV.AppendLine("Day,Money,NetIncome,Population,Satisfaction,Health,AirPollution,Carbon,Noise,Congestion,Buildings,Roads");

            foreach (var s in metricHistory)
            {
                metricCSV.AppendLine($"{s.day},{s.money:F0},{s.netIncome:F0},{s.population}," +
                    $"{s.satisfaction:F1},{s.health:F1},{s.airPollution:F2},{s.carbonEmission:F0}," +
                    $"{s.avgNoise:F1},{s.avgCongestion:F2},{s.totalBuildings},{s.totalRoads}");
            }
            File.WriteAllText(MetricsFilePath, metricCSV.ToString());

            // Aksiyon CSV
            StringBuilder actionCSV = new StringBuilder();
            actionCSV.AppendLine("Day,Type,Detail,Timestamp");

            foreach (var a in actionHistory)
            {
                string safeDetail = a.detail.Replace(",", " ");
                actionCSV.AppendLine($"{a.day},{a.actionType},{safeDetail},{a.timestamp:F1}");
            }
            File.WriteAllText(ActionsFilePath, actionCSV.ToString());

            Debug.Log($"[Analytics] 💾 CSV kaydedildi: {MetricsFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Analytics] ❌ CSV yazma hatası: {e.Message}");
        }
    }
}
