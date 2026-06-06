using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Yol yerleştirme ve trafik simülasyonu sistemi.
/// Yollar 1x1 grid hücreleridir. Konut↔İş alanları arasında sanal trafik yükü dağıtılır.
/// GDD Bölüm 7: Trafik Sistemi
/// 
/// Kapasite kuralları:
///   %70 → trafik yoğunlaşması (gürültü + kirlilik eklenir)
///   %100 → trafik kilidi (fabrika verimliliği %80 düşer)
/// </summary>
public class RoadSystem : MonoBehaviour
{
    public static RoadSystem Instance { get; private set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Yol Ayarları")]
    [Tooltip("Her yol hücresinin araç kapasitesi")]
    [SerializeField] private int roadCapacity = 20;
    [Tooltip("Yol başına bakım maliyeti (Eko-Para/tick)")]
    [SerializeField] private int roadMaintenancePerTick = 2;

    [Header("Trafik Eşikleri (GDD Bölüm 7)")]
    [Tooltip("Bu yüzdenin üzerinde trafik yoğunlaşması başlar")]
    [SerializeField] private float congestionThreshold = 0.70f;
    [Tooltip("Bu yüzdenin üzerinde trafik kilidi")]
    [SerializeField] private float gridlockThreshold = 1.0f;

    [Header("Trafik Cezaları")]
    [Tooltip("Yoğun yolların eklediği gürültü (dB)")]
    [SerializeField] private float congestionNoiseBonus = 15f;
    [Tooltip("Yoğun yolların eklediği hava kirliliği")]
    [SerializeField] private float congestionPollutionBonus = 3f;

    // ═══════════════════════════════════════════════════════════════
    // VERİ YAPILARI
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Bir yol hücresinin durumu.</summary>
    public class RoadTile
    {
        public int GridX;
        public int GridZ;
        public int CurrentLoad;       // Şu anki araç yükü
        public int Capacity;          // Maksimum kapasite
        public float CongestionRatio; // 0-1+ arası doluluk oranı
        public bool IsCongested;      // %70 üzeri
        public bool IsGridlocked;     // %100 üzeri
        public GameObject Visual;     // Blockout görsel
    }

    private Dictionary<Vector2Int, RoadTile> roadTiles = new Dictionary<Vector2Int, RoadTile>();

    // ─── Public Erişim ──────────────────────────────────────────
    public int TotalRoads => roadTiles.Count;
    public float AverageCongestion { get; private set; }
    public int TotalRoadMaintenance => roadTiles.Count * roadMaintenancePerTick;
    public int CongestedRoadCount { get; private set; }
    public int GridlockedRoadCount { get; private set; }

    // ─── Events ─────────────────────────────────────────────────
    public event Action<RoadTile> OnRoadPlaced;
    public event Action<Vector2Int> OnRoadRemoved;

    // ─── Blockout Renkleri ──────────────────────────────────────
    private readonly Color normalRoadColor = new Color(0.4f, 0.4f, 0.4f);      // Gri
    private readonly Color congestedRoadColor = new Color(0.8f, 0.6f, 0.1f);   // Turuncu
    private readonly Color gridlockedRoadColor = new Color(0.8f, 0.15f, 0.15f); // Kırmızı

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
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += ProcessTick;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= ProcessTick;
    }

    // ═══════════════════════════════════════════════════════════════
    // YOL YERLEŞTİRME / KALDIRMA
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Grid'e yol yerleştirir.</summary>
    public bool PlaceRoad(int x, int z)
    {
        if (GridSystem.Instance == null) return false;
        if (!GridSystem.Instance.IsCellEmpty(x, z)) return false;

        // Para kontrolü (yol maliyeti basit: 50 Eko-Para)
        int roadCost = 50;
        if (ResourceSystem.Instance != null && !ResourceSystem.Instance.SpendMoney(roadCost))
            return false;

        // Blockout görsel oluştur
        float cellSize = GridSystem.Instance.CellSize;
        GameObject roadObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roadObj.name = $"Road ({x},{z})";
        roadObj.transform.localScale = new Vector3(cellSize * 0.95f, 0.08f, cellSize * 0.95f);

        Vector3 worldPos = GridSystem.Instance.GridToWorldPosition(x, z);
        roadObj.transform.position = new Vector3(worldPos.x, 0.04f, worldPos.z);

        Renderer renderer = roadObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = normalRoadColor;
        renderer.material = mat;

        // RoadTile oluştur
        RoadTile tile = new RoadTile
        {
            GridX = x,
            GridZ = z,
            Capacity = roadCapacity,
            CurrentLoad = 0,
            Visual = roadObj
        };

        Vector2Int key = new Vector2Int(x, z);
        roadTiles[key] = tile;

        // Grid'e kaydet
        GridSystem.Instance.SetCell(x, z, GridSystem.CellType.Road, roadObj);
        OnRoadPlaced?.Invoke(tile);

        return true;
    }

    /// <summary>Sürükle-bırak ile yol hattı çeker (başlangıç → bitiş).</summary>
    public int PlaceRoadLine(int startX, int startZ, int endX, int endZ)
    {
        int placed = 0;

        // Yatay hat
        if (startX != endX)
        {
            int minX = Mathf.Min(startX, endX);
            int maxX = Mathf.Max(startX, endX);
            for (int x = minX; x <= maxX; x++)
            {
                if (PlaceRoad(x, startZ)) placed++;
            }
        }

        // Dikey hat
        if (startZ != endZ)
        {
            int minZ = Mathf.Min(startZ, endZ);
            int maxZ = Mathf.Max(startZ, endZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                if (PlaceRoad(endX, z)) placed++;
            }
        }

        // Tek nokta
        if (startX == endX && startZ == endZ)
        {
            if (PlaceRoad(startX, startZ)) placed++;
        }

        return placed;
    }

    /// <summary>Yol kaldırır.</summary>
    public bool RemoveRoad(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);
        if (!roadTiles.ContainsKey(key)) return false;

        RoadTile tile = roadTiles[key];

        // Grid'den temizle
        GridSystem.Instance.SetCell(x, z, GridSystem.CellType.Empty, null);

        // Görseli sil
        if (tile.Visual != null) Destroy(tile.Visual);

        roadTiles.Remove(key);
        OnRoadRemoved?.Invoke(key);

        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK İŞLEME (GDD Bölüm 8 — Pipeline Adım 3: Lojistik Çözümü)
    // ═══════════════════════════════════════════════════════════════

    private void ProcessTick()
    {
        ResetTrafficLoads();
        DistributeTraffic();
        UpdateCongestion();
        UpdateRoadVisuals();
        ApplyRoadMaintenance();
    }

    /// <summary>Tüm yolların trafik yükünü sıfırla.</summary>
    private void ResetTrafficLoads()
    {
        foreach (var tile in roadTiles.Values)
        {
            tile.CurrentLoad = 0;
        }
    }

    /// <summary>
    /// Konut ↔ İş alanları arasında sanal trafik yükü dağıtır.
    /// GDD: "Konutlar ile iş alanları arasında simülasyon her çalıştığında sanal bir yük dağıtılır."
    /// 
    /// Basitleştirilmiş model: Her konut ve fabrika/ticaret binası etrafındaki
    /// yollara orantılı yük ekler.
    /// </summary>
    private void DistributeTraffic()
    {
        if (BuildingPlacer.Instance == null) return;

        foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (building == null || building.Data == null || !building.IsActive) continue;

            // Konut ve sanayi binaları trafik üretir
            int trafficGeneration = 0;
            switch (building.Data.category)
            {
                case BuildingData.BuildingCategory.Residential:
                    trafficGeneration = building.Data.populationCapacity / 2;
                    break;
                case BuildingData.BuildingCategory.Industrial:
                    trafficGeneration = 4;
                    break;
                case BuildingData.BuildingCategory.Social:
                case BuildingData.BuildingCategory.Healthcare:
                    trafficGeneration = 2;
                    break;
                default:
                    continue;
            }

            if (trafficGeneration <= 0) continue;

            // Binanın etrafındaki yollara yük dağıt
            AddTrafficAroundBuilding(building, trafficGeneration);
        }
    }

    /// <summary>Bina çevresindeki yollara trafik yükü ekler.</summary>
    private void AddTrafficAroundBuilding(BuildingInstance building, int load)
    {
        int searchRadius = 3; // Bina çevresinde kaç hücre aranacak

        for (int dx = -searchRadius; dx <= building.Data.gridWidth + searchRadius; dx++)
        {
            for (int dz = -searchRadius; dz <= building.Data.gridHeight + searchRadius; dz++)
            {
                int tx = building.GridX + dx;
                int tz = building.GridZ + dz;
                Vector2Int key = new Vector2Int(tx, tz);

                if (roadTiles.ContainsKey(key))
                {
                    // Mesafeye göre azalan yük
                    int dist = Mathf.Max(1, Mathf.Abs(dx) + Mathf.Abs(dz));
                    int addedLoad = Mathf.Max(1, load / dist);
                    roadTiles[key].CurrentLoad += addedLoad;
                }
            }
        }
    }

    /// <summary>Tıkanıklık durumlarını hesapla.</summary>
    private void UpdateCongestion()
    {
        float totalCongestion = 0f;
        CongestedRoadCount = 0;
        GridlockedRoadCount = 0;

        foreach (var tile in roadTiles.Values)
        {
            tile.CongestionRatio = tile.Capacity > 0
                ? (float)tile.CurrentLoad / tile.Capacity
                : 0f;

            tile.IsCongested = tile.CongestionRatio >= congestionThreshold;
            tile.IsGridlocked = tile.CongestionRatio >= gridlockThreshold;

            if (tile.IsCongested) CongestedRoadCount++;
            if (tile.IsGridlocked) GridlockedRoadCount++;

            totalCongestion += tile.CongestionRatio;

            // Yoğun yollar ekstra kirlilik ve gürültü ekler
            if (tile.IsCongested && PollutionSystem.Instance != null)
            {
                // PollutionSystem doğrudan erişimle hücreye kirlilik eklenebilir
                // Şimdilik global etki — ileriki fazlarda doğrudan grid erişimi
            }
        }

        AverageCongestion = roadTiles.Count > 0
            ? totalCongestion / roadTiles.Count
            : 0f;
    }

    /// <summary>Yol renklerini trafik durumuna göre güncelle.</summary>
    private void UpdateRoadVisuals()
    {
        foreach (var tile in roadTiles.Values)
        {
            if (tile.Visual == null) continue;
            Renderer renderer = tile.Visual.GetComponent<Renderer>();
            if (renderer == null) continue;

            Color targetColor;
            if (tile.IsGridlocked)
                targetColor = gridlockedRoadColor;
            else if (tile.IsCongested)
                targetColor = congestedRoadColor;
            else
                targetColor = normalRoadColor;

            renderer.material.color = targetColor;
        }
    }

    /// <summary>Yol bakım maliyetlerini ResourceSystem'e bildir.</summary>
    private void ApplyRoadMaintenance()
    {
        // ResourceSystem zaten bakım giderlerini topluyor, ama yollar
        // BuildingInstance olmadığından ayrıca eklememiz gerekir.
        if (ResourceSystem.Instance != null && roadTiles.Count > 0)
        {
            float maintenance = roadTiles.Count * roadMaintenancePerTick;
            ResourceSystem.Instance.AddMoney(-maintenance);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen hücrede yol var mı?</summary>
    public bool HasRoad(int x, int z)
    {
        return roadTiles.ContainsKey(new Vector2Int(x, z));
    }

    /// <summary>Belirtilen hücredeki yolun tıkanıklık oranı (0-1+).</summary>
    public float GetCongestion(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);
        if (roadTiles.ContainsKey(key))
            return roadTiles[key].CongestionRatio;
        return 0f;
    }

    /// <summary>Belirtilen hücredeki yol kilitli mi? (Fabrika verimlilik düşüşü için)</summary>
    public bool IsGridlocked(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);
        return roadTiles.ContainsKey(key) && roadTiles[key].IsGridlocked;
    }

    /// <summary>Gürültü sistemi için: yol gürültü katkısı (dB).</summary>
    public float GetRoadNoise(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);
        if (!roadTiles.ContainsKey(key)) return 0f;

        var tile = roadTiles[key];
        // GDD: Yoğun Yollar 60 dB
        float baseNoise = 30f; // Normal yol
        if (tile.IsCongested) baseNoise = 60f; // Yoğun yol (GDD)
        return baseNoise;
    }
}
