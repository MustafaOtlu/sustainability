using UnityEngine;

/// <summary>
/// Grid tabanlı gürültü kirliliği simülasyonu.
/// Gürültü kaynaktan uzaklaştıkça tile başına azalır; parklar ekstra sönümlenme sağlar.
/// GDD Bölüm 4.4: Gürültü Kirliliği Sistemi
/// </summary>
public class NoiseSystem : MonoBehaviour
{
    public static NoiseSystem Instance { get; private set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Sönümlenme")]
    [Tooltip("Gürültü her tile uzaklaştığında kaç dB azalır (GDD: 8 dB)")]
    [SerializeField] private float decayPerTile = 8f;

    [Tooltip("Park hücresi varsa ekstra sönümlenme (GDD: 16 dB Ağaç bariyeri)")]
    [SerializeField] private float parkBarrierBonus = 16f;

    [Header("Vatandaş Etkisi")]
    [Tooltip("Konut bölgesinde bu dB üzerine çıkınca ceza başlar (GDD: 55 dB)")]
    [SerializeField] private float residentialThreshold = 55f;

    // ─── Veri ───────────────────────────────────────────────────
    private float[,] noiseMap;
    private int width, height;

    /// <summary>Şehir genelindeki ortalama gürültü seviyesi (dB).</summary>
    public float AverageNoise { get; private set; }

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

        noiseMap = new float[width, height];

        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += ProcessTick;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= ProcessTick;
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK İŞLEME
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Her tick'te gürültü haritasını sıfırdan hesaplar.
    /// Gürültü anlık bir değerdir (kirlilik gibi birikmez), her tick yeniden hesaplanır.
    /// </summary>
    private void ProcessTick()
    {
        ClearNoiseMap();
        CalculateNoiseFromBuildings();
        CalculateAverageNoise();
    }

    private void ClearNoiseMap()
    {
        System.Array.Clear(noiseMap, 0, noiseMap.Length);
    }

    // ─── Binalardan Gürültü Hesaplama ───────────────────────────
    private void CalculateNoiseFromBuildings()
    {
        if (BuildingPlacer.Instance == null) return;

        foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (building == null || building.Data == null || !building.IsActive) continue;
            if (building.Data.noiseLevel <= 0f) continue;

            float sourceNoise = building.Data.noiseLevel;
            int centerX = building.GridX + building.Data.gridWidth / 2;
            int centerZ = building.GridZ + building.Data.gridHeight / 2;

            // Gürültünün ulaşabileceği maksimum mesafe
            int maxRange = Mathf.CeilToInt(sourceNoise / decayPerTile) + 1;

            PropagateNoise(centerX, centerZ, sourceNoise, maxRange);
        }
    }

    /// <summary>
    /// Bir gürültü kaynağından çevreye yayılım hesaplar.
    /// Mesafe bazlı sönümlenme + park bariyeri uygulanır.
    /// GDD: "Gürültü, her tile uzaklaştığında 8 dB azalır.
    ///        Eğer arada Park hücresi varsa gürültü ekstra 16 dB düşer."
    /// </summary>
    private void PropagateNoise(int sourceX, int sourceZ, float sourceNoise, int maxRange)
    {
        for (int dx = -maxRange; dx <= maxRange; dx++)
        {
            for (int dz = -maxRange; dz <= maxRange; dz++)
            {
                int tx = sourceX + dx;
                int tz = sourceZ + dz;
                if (!IsValid(tx, tz)) continue;

                // Manhattan mesafesi (grid tabanlı oyunlar için uygun)
                int distance = Mathf.Abs(dx) + Mathf.Abs(dz);
                if (distance == 0)
                {
                    // Kaynağın kendi hücresi
                    noiseMap[tx, tz] = Mathf.Max(noiseMap[tx, tz], sourceNoise);
                    continue;
                }

                // Mesafe sönümlenme
                float decayedNoise = sourceNoise - (distance * decayPerTile);

                // Park bariyeri: kaynak ile hedef arasındaki yol üzerinde park var mı?
                // Basitleştirilmiş kontrol: hedef hücrede veya komşusunda park varsa ekstra azalt
                decayedNoise -= CountParksInPath(sourceX, sourceZ, tx, tz) * parkBarrierBonus;

                if (decayedNoise <= 0f) continue;

                // En yüksek gürültü kaynağı geçerli (üst üste binme: max alınır)
                noiseMap[tx, tz] = Mathf.Max(noiseMap[tx, tz], decayedNoise);
            }
        }
    }

    /// <summary>
    /// Kaynak ile hedef arasındaki basitleştirilmiş yolda kaç park hücresi var.
    /// Performans için sadece doğrudan çizgi üzerindeki hücreleri kontrol eder.
    /// </summary>
    private int CountParksInPath(int fromX, int fromZ, int toX, int toZ)
    {
        if (GridSystem.Instance == null) return 0;

        int parkCount = 0;
        int steps = Mathf.Max(Mathf.Abs(toX - fromX), Mathf.Abs(toZ - fromZ));
        if (steps <= 1) return 0;

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            int checkX = Mathf.RoundToInt(Mathf.Lerp(fromX, toX, t));
            int checkZ = Mathf.RoundToInt(Mathf.Lerp(fromZ, toZ, t));

            if (GridSystem.Instance.GetCellType(checkX, checkZ) == GridSystem.CellType.Park)
            {
                parkCount++;
            }
        }

        return parkCount;
    }

    // ─── Ortalama Hesaplama ─────────────────────────────────────
    private void CalculateAverageNoise()
    {
        float total = 0f;
        int noisyCells = 0;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (noiseMap[x, z] > 0f)
                {
                    total += noiseMap[x, z];
                    noisyCells++;
                }
            }
        }

        AverageNoise = noisyCells > 0 ? total / noisyCells : 0f;
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen hücredeki gürültü seviyesi (dB).</summary>
    public float GetNoise(int x, int z)
    {
        if (!IsValid(x, z)) return 0f;
        return noiseMap[x, z];
    }

    /// <summary>
    /// Belirtilen konut hücresinin gürültü cezası (0-1 arası).
    /// 55 dB altı = 0 ceza, 55-100 dB arası kademeli ceza.
    /// GDD: "Konut bölgelerinde gürültü 55 dB üzerine çıkarsa vatandaşların
    ///       sağlığı ve mutluluğu kademeli olarak azalır."
    /// </summary>
    public float GetNoisePenalty(int x, int z)
    {
        float noise = GetNoise(x, z);
        if (noise <= residentialThreshold) return 0f;

        // 55 dB'den 100 dB'ye doğru 0-1 arası kademeli ceza
        return Mathf.Clamp01((noise - residentialThreshold) / (100f - residentialThreshold));
    }

    private bool IsValid(int x, int z)
    {
        return x >= 0 && x < width && z >= 0 && z < height;
    }
}
