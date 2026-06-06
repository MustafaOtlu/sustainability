using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Grid tabanlı kirlilik simülasyon sistemi.
/// Hava, su, toprak kirliliği hücrelerde yayılır; parklar absorbe eder.
/// Global karbon salınımı ayrı takip edilir.
/// GDD Bölüm 4.2: Çevre ve Kirlilik Sistemi
/// </summary>
public class PollutionSystem : MonoBehaviour
{
    public static PollutionSystem Instance { get; private set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Yayılım Ayarları")]
    [Tooltip("Her tick'te kirliliğin komşulara yayılma oranı (GDD: %15)")]
    [SerializeField] private float spreadRate = 0.15f;

    [Tooltip("Her tick'te doğal kirlilik azalma oranı")]
    [SerializeField] private float naturalDecayRate = 0.02f;

    [Tooltip("Kirlilik yayılımında maksimum hücre değeri")]
    [SerializeField] private float maxPollutionPerCell = 100f;

    // ═══════════════════════════════════════════════════════════════
    // KİRLİLİK VERİ KATMANLARI
    // ═══════════════════════════════════════════════════════════════

    private float[,] airPollution;
    private float[,] waterPollution;
    private float[,] soilPollution;

    // Double-buffer (yayılımda sıra bağımlılığını engellemek için)
    private float[,] airPollutionBuffer;
    private float[,] waterPollutionBuffer;

    private int width, height;

    // ─── Global Değerler ────────────────────────────────────────
    /// <summary>Şehir genelindeki toplam karbon salınımı (GDD: global değer).</summary>
    public float GlobalCarbonEmission { get; private set; }

    /// <summary>Haritadaki ortalama hava kirliliği (0-100).</summary>
    public float AverageAirPollution { get; private set; }

    /// <summary>Haritadaki ortalama su kirliliği (0-100).</summary>
    public float AverageWaterPollution { get; private set; }

    /// <summary>Haritadaki kirli hücre oranı (%0-100). Game Over eşiği: %70</summary>
    public float PollutedAreaPercentage { get; private set; }

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
        InitializeGrids();

        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += ProcessTick;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= ProcessTick;
    }

    private void InitializeGrids()
    {
        if (GridSystem.Instance != null)
        {
            width = GridSystem.Instance.GridWidth;
            height = GridSystem.Instance.GridHeight;
        }
        else
        {
            width = 100;
            height = 100;
        }

        airPollution = new float[width, height];
        waterPollution = new float[width, height];
        soilPollution = new float[width, height];
        airPollutionBuffer = new float[width, height];
        waterPollutionBuffer = new float[width, height];
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK İŞLEME (GDD Bölüm 8 — Pipeline Adım 2: Çevre Güncellemesi)
    // ═══════════════════════════════════════════════════════════════

    private void ProcessTick()
    {
        ProducePollution();
        SpreadPollution();
        AbsorbPollution();
        ApplyNaturalDecay();
        CalculateGlobalMetrics();
    }

    // ─── 1. Kirlilik Üretimi (Binalardan) ───────────────────────
    private void ProducePollution()
    {
        if (BuildingPlacer.Instance == null) return;

        GlobalCarbonEmission = 0f;

        foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (building == null || building.Data == null || !building.IsActive) continue;
            var data = building.Data;

            // Kirlilik üretimi, binanın kapladığı alana yayılır
            int tiles = data.TotalTiles;
            if (tiles <= 0) tiles = 1;

            for (int dx = 0; dx < data.gridWidth; dx++)
            {
                for (int dz = 0; dz < data.gridHeight; dz++)
                {
                    int cx = building.GridX + dx;
                    int cz = building.GridZ + dz;
                    if (!IsValid(cx, cz)) continue;

                    airPollution[cx, cz] += data.airPollutionPerTick / tiles;
                    waterPollution[cx, cz] += data.waterPollutionPerTick / tiles;
                    soilPollution[cx, cz] += data.soilPollutionPerTick / tiles;
                }
            }

            // Global karbon (şehir geneli, GDD: global değer)
            GlobalCarbonEmission += data.carbonEmissionPerTick;
        }
    }

    // ─── 2. Kirlilik Yayılımı (GDD: %15 komşulara) ─────────────
    private void SpreadPollution()
    {
        // Hava kirliliği yayılımı (double-buffer)
        System.Array.Copy(airPollution, airPollutionBuffer, airPollution.Length);
        SpreadLayer(airPollution, airPollutionBuffer);
        System.Array.Copy(airPollutionBuffer, airPollution, airPollution.Length);

        // Su kirliliği yayılımı
        System.Array.Copy(waterPollution, waterPollutionBuffer, waterPollution.Length);
        SpreadLayer(waterPollution, waterPollutionBuffer);
        System.Array.Copy(waterPollutionBuffer, waterPollution, waterPollution.Length);

        // Toprak kirliliği yayılmaz (sabit, GDD'de yayılma belirtilmemiş)
    }

    private void SpreadLayer(float[,] source, float[,] dest)
    {
        // 4 yönlü komşulara yayılım
        int[] dx = { 0, 0, 1, -1 };
        int[] dz = { 1, -1, 0, 0 };

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float current = source[x, z];
                if (current < 0.1f) continue; // Çok düşük değerleri atla

                float spreadAmount = current * spreadRate;
                float perNeighbor = spreadAmount / 4f;

                // Kaynak hücreden çıkan miktar
                dest[x, z] -= spreadAmount;

                // Komşulara yay
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d];
                    int nz = z + dz[d];
                    if (!IsValid(nx, nz)) continue;
                    dest[nx, nz] += perNeighbor;
                }
            }
        }

        // Clamp
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                dest[x, z] = Mathf.Clamp(dest[x, z], 0f, maxPollutionPerCell);
    }

    // ─── 3. Park Absorbe (GDD: Kirlilik ve gürültüyü absorbe eder) ─
    private void AbsorbPollution()
    {
        if (BuildingPlacer.Instance == null) return;

        foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (building == null || building.Data == null) continue;
            if (building.Data.pollutionAbsorptionPerTick <= 0f) continue;

            float absorption = building.Data.pollutionAbsorptionPerTick;
            int radius = building.Data.effectRadius;
            if (radius <= 0) radius = 1;

            // Binanın merkez koordinatı
            int centerX = building.GridX + building.Data.gridWidth / 2;
            int centerZ = building.GridZ + building.Data.gridHeight / 2;

            // Etki alanındaki hücrelerden kirlilik em
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int tx = centerX + dx;
                    int tz = centerZ + dz;
                    if (!IsValid(tx, tz)) continue;

                    // Mesafe bazlı azalan emme
                    float dist = Mathf.Max(1, Mathf.Abs(dx) + Mathf.Abs(dz));
                    float localAbsorb = absorption / (dist * dist);

                    airPollution[tx, tz] = Mathf.Max(0f, airPollution[tx, tz] - localAbsorb);
                    waterPollution[tx, tz] = Mathf.Max(0f, waterPollution[tx, tz] - localAbsorb * 0.5f);
                }
            }

            // Karbon azaltma (parklar global karbonu da azaltır)
            GlobalCarbonEmission = Mathf.Max(0f, GlobalCarbonEmission - absorption * 0.3f);
        }
    }

    // ─── 4. Doğal Azalma ────────────────────────────────────────
    private void ApplyNaturalDecay()
    {
        float decayMultiplier = 1f - naturalDecayRate;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                airPollution[x, z] *= decayMultiplier;
                waterPollution[x, z] *= decayMultiplier;
                soilPollution[x, z] *= decayMultiplier;

                // Çok küçük değerleri sıfırla (performans)
                if (airPollution[x, z] < 0.01f) airPollution[x, z] = 0f;
                if (waterPollution[x, z] < 0.01f) waterPollution[x, z] = 0f;
                if (soilPollution[x, z] < 0.01f) soilPollution[x, z] = 0f;
            }
        }
    }

    // ─── 5. Global Metrikler ────────────────────────────────────
    private void CalculateGlobalMetrics()
    {
        float totalAir = 0f;
        float totalWater = 0f;
        int pollutedCells = 0;
        int totalCells = width * height;
        float pollutionThreshold = 10f; // Bu değerin üzeri "kirli" sayılır

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                totalAir += airPollution[x, z];
                totalWater += waterPollution[x, z];

                if (airPollution[x, z] > pollutionThreshold ||
                    waterPollution[x, z] > pollutionThreshold)
                {
                    pollutedCells++;
                }
            }
        }

        AverageAirPollution = totalAir / totalCells;
        AverageWaterPollution = totalWater / totalCells;
        PollutedAreaPercentage = (float)pollutedCells / totalCells * 100f;
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen hücredeki hava kirliliği (0-100).</summary>
    public float GetAirPollution(int x, int z)
    {
        if (!IsValid(x, z)) return 0f;
        return airPollution[x, z];
    }

    /// <summary>Belirtilen hücredeki su kirliliği (0-100).</summary>
    public float GetWaterPollution(int x, int z)
    {
        if (!IsValid(x, z)) return 0f;
        return waterPollution[x, z];
    }

    /// <summary>Belirtilen hücredeki toprak kirliliği (0-100).</summary>
    public float GetSoilPollution(int x, int z)
    {
        if (!IsValid(x, z)) return 0f;
        return soilPollution[x, z];
    }

    /// <summary>Belirtilen hücredeki toplam kirlilik (hava + su + toprak ortalaması).</summary>
    public float GetTotalPollution(int x, int z)
    {
        if (!IsValid(x, z)) return 0f;
        return (airPollution[x, z] + waterPollution[x, z] + soilPollution[x, z]) / 3f;
    }

    /// <summary>Belirtilen hücrede toprak kirliliği konut inşaatına engel mi? (GDD 4.2)</summary>
    public bool IsSoilTooContaminated(int x, int z)
    {
        if (!IsValid(x, z)) return false;
        return soilPollution[x, z] > 50f;
    }

    private bool IsValid(int x, int z)
    {
        return x >= 0 && x < width && z >= 0 && z < height;
    }
}
