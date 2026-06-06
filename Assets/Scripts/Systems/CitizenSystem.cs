using UnityEngine;
using System;

/// <summary>
/// Vatandaş simülasyon sistemi. Mahalle grupları (Cohorts) bazında mutluluk, sağlık
/// ve göç hesaplamalarını yönetir.
/// GDD Bölüm 4.3: Vatandaş Sistemi
/// 
/// Formül: Memnuniyet = Sağlık - (HavaKirliliği + Gürültü + TrafikYoğunluğu)
/// </summary>
public class CitizenSystem : MonoBehaviour
{
    public static CitizenSystem Instance { get; private set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Başlangıç Değerleri")]
    [SerializeField] private float baseHealth = 80f;
    [SerializeField] private float baseHappiness = 70f;

    [Header("Göç Ayarları")]
    [Tooltip("Memnuniyet bu değerin üzerindeyse göç gelir")]
    [SerializeField] private float immigrationThreshold = 50f;
    [Tooltip("Memnuniyet bu değerin altındaysa göç gider")]
    [SerializeField] private float emigrationThreshold = 35f;
    [Tooltip("Her tick'te maksimum göç miktarı")]
    [SerializeField] private int maxMigrationPerTick = 5;

    [Header("Kirlilik Etki Çarpanları")]
    [Tooltip("Hava kirliliğinin sağlığa etkisi (0-1)")]
    [SerializeField] private float pollutionHealthImpact = 0.5f;
    [Tooltip("Su kirliliğinin sağlığa etkisi (0-1)")]
    [SerializeField] private float waterPollutionHealthImpact = 0.7f;

    // ═══════════════════════════════════════════════════════════════
    // VATANDAŞ METRİKLERİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Şehir genelindeki ortalama sağlık (0-100).</summary>
    public float GlobalHealth { get; private set; }

    /// <summary>Şehir genelindeki ortalama mutluluk (0-100).</summary>
    public float GlobalHappiness { get; private set; }

    /// <summary>Genel Memnuniyet Endeksi (0-100). Game Over eşiği: 30</summary>
    public float GlobalSatisfaction { get; private set; }

    /// <summary>Bu tick'te gelen/giden göç miktarı (+göç/-göç).</summary>
    public int LastMigration { get; private set; }

    // ─── Events ─────────────────────────────────────────────────
    /// <summary>Memnuniyet kritik seviyeye düştüğünde (< 30).</summary>
    public event Action OnSatisfactionCritical;

    /// <summary>Memnuniyet her güncellendiğinde.</summary>
    public event Action OnMetricsUpdated;

    // ─── Private ────────────────────────────────────────────────
    private int consecutiveLowSatisfactionDays = 0;

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

        GlobalHealth = baseHealth;
        GlobalHappiness = baseHappiness;
        GlobalSatisfaction = baseHappiness;
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
    // TICK İŞLEME (GDD Bölüm 8 — Pipeline Adım 4: Vatandaş Hesaplaması)
    // ═══════════════════════════════════════════════════════════════

    private void ProcessTick()
    {
        CalculateHealth();
        CalculateHappiness();
        CalculateSatisfaction();
        ProcessMigration();
        CheckCriticalState();

        OnMetricsUpdated?.Invoke();
    }

    // ─── 1. Sağlık Hesaplama ────────────────────────────────────
    private void CalculateHealth()
    {
        float health = baseHealth;

        // Hastane bonusu
        if (BuildingPlacer.Instance != null)
        {
            foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
            {
                if (building == null || building.Data == null || !building.IsActive) continue;
                if (building.Data.healthBonus > 0f)
                {
                    health += building.Data.healthBonus;
                }
            }
        }

        // Hava kirliliği sağlık düşüşü
        if (PollutionSystem.Instance != null)
        {
            health -= PollutionSystem.Instance.AverageAirPollution * pollutionHealthImpact;
            health -= PollutionSystem.Instance.AverageWaterPollution * waterPollutionHealthImpact;
        }

        // Su kesintisi sağlığı çökertir (GDD 4.1)
        if (ResourceSystem.Instance != null && ResourceSystem.Instance.IsWaterShortage)
        {
            health -= 20f;
        }

        // Karartma da sağlığı dolaylı etkiler
        if (ResourceSystem.Instance != null && ResourceSystem.Instance.IsBlackout)
        {
            health -= 10f;
        }

        GlobalHealth = Mathf.Clamp(health, 0f, 100f);
    }

    // ─── 2. Mutluluk Hesaplama ──────────────────────────────────
    private void CalculateHappiness()
    {
        // GDD Formül: Memnuniyet = Sağlık - (HavaKirliliği + Gürültü + TrafikYoğunluğu)
        float happiness = GlobalHealth;

        // Kirlilik cezası
        if (PollutionSystem.Instance != null)
        {
            happiness -= PollutionSystem.Instance.AverageAirPollution * 0.3f;
        }

        // Gürültü cezası
        if (NoiseSystem.Instance != null)
        {
            happiness -= NoiseSystem.Instance.AverageNoise * 0.2f;
        }

        // Trafik cezası (Faz 3'te RoadSystem eklendiğinde aktif olacak)
        // happiness -= trafficCongestion * 0.2f;

        // Bina bonusları (Kültür Merkezi, Park vb.)
        if (BuildingPlacer.Instance != null)
        {
            foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
            {
                if (building == null || building.Data == null || !building.IsActive) continue;
                if (building.Data.happinessBonus > 0f)
                {
                    // Bonus etkisi binanın etki alanı ve nüfus oranına bağlı (basitleştirilmiş)
                    float scaledBonus = building.Data.happinessBonus * 0.3f; // Ölçeklenmiş
                    happiness += scaledBonus;
                }
            }
        }

        // Karartma ve su kesintisi mutsuzluk
        if (ResourceSystem.Instance != null)
        {
            if (ResourceSystem.Instance.IsBlackout) happiness -= 15f;
            if (ResourceSystem.Instance.IsWaterShortage) happiness -= 20f;
        }

        GlobalHappiness = Mathf.Clamp(happiness, 0f, 100f);
    }

    // ─── 3. Genel Memnuniyet ────────────────────────────────────
    private void CalculateSatisfaction()
    {
        // Ağırlıklı ortalama
        GlobalSatisfaction = (GlobalHealth * 0.4f) + (GlobalHappiness * 0.6f);
        GlobalSatisfaction = Mathf.Clamp(GlobalSatisfaction, 0f, 100f);
    }

    // ─── 4. Göç Sistemi ─────────────────────────────────────────
    private void ProcessMigration()
    {
        if (ResourceSystem.Instance == null) return;

        int currentPop = ResourceSystem.Instance.Population;
        int capacity = ResourceSystem.Instance.PopulationCapacity;
        LastMigration = 0;

        if (capacity <= 0) return; // Konut yoksa göç olmaz

        if (GlobalSatisfaction >= immigrationThreshold && currentPop < capacity)
        {
            // Göç gelir (memnuniyet yüksek + yer var)
            float attractiveness = (GlobalSatisfaction - immigrationThreshold) / (100f - immigrationThreshold);
            int immigrants = Mathf.CeilToInt(attractiveness * maxMigrationPerTick);
            immigrants = Mathf.Min(immigrants, capacity - currentPop);
            LastMigration = immigrants;
        }
        else if (GlobalSatisfaction < emigrationThreshold && currentPop > 0)
        {
            // Göç gider (memnuniyet düşük)
            float urgency = (emigrationThreshold - GlobalSatisfaction) / emigrationThreshold;
            int emigrants = Mathf.CeilToInt(urgency * maxMigrationPerTick);
            emigrants = Mathf.Min(emigrants, currentPop);
            LastMigration = -emigrants;
        }

        if (LastMigration != 0)
        {
            ResourceSystem.Instance.SetPopulation(currentPop + LastMigration);

            #if UNITY_EDITOR
            string dir = LastMigration > 0 ? "göç geldi" : "göç gitti";
            Debug.Log($"[CitizenSystem] {Mathf.Abs(LastMigration)} kişi {dir}. " +
                      $"Nüfus: {ResourceSystem.Instance.Population} | Memnuniyet: {GlobalSatisfaction:F1}%");
            #endif
        }
    }

    // ─── 5. Kritik Durum Kontrolü ───────────────────────────────
    private void CheckCriticalState()
    {
        if (GlobalSatisfaction < 30f)
        {
            consecutiveLowSatisfactionDays++;

            if (consecutiveLowSatisfactionDays >= 2)
            {
                OnSatisfactionCritical?.Invoke();
                #if UNITY_EDITOR
                Debug.LogWarning($"[CitizenSystem] ⚠ KRİTİK! Memnuniyet {GlobalSatisfaction:F1}% — " +
                                 $"Ardışık {consecutiveLowSatisfactionDays} gün düşük!");
                #endif
            }
        }
        else
        {
            consecutiveLowSatisfactionDays = 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen konut hücresinin yerel memnuniyet değeri.</summary>
    public float GetLocalSatisfaction(int x, int z)
    {
        float localHealth = GlobalHealth;
        float localHappiness = GlobalHappiness;

        // Yerel kirlilik etkisi
        if (PollutionSystem.Instance != null)
        {
            float localPollution = PollutionSystem.Instance.GetAirPollution(x, z);
            localHappiness -= localPollution * 0.5f;
        }

        // Yerel gürültü etkisi
        if (NoiseSystem.Instance != null)
        {
            float penalty = NoiseSystem.Instance.GetNoisePenalty(x, z);
            localHappiness -= penalty * 30f;
            localHealth -= penalty * 15f;
        }

        float satisfaction = (Mathf.Clamp(localHealth, 0, 100) * 0.4f) +
                            (Mathf.Clamp(localHappiness, 0, 100) * 0.6f);
        return Mathf.Clamp(satisfaction, 0f, 100f);
    }
}
