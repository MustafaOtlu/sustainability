using UnityEngine;
using System;

/// <summary>
/// Oyunun tüm kaynaklarını takip eden ve her tick'te güncelleyen merkezi sistem.
/// GDD Bölüm 4.1: Para, Enerji, Su, Nüfus
/// </summary>
public class ResourceSystem : MonoBehaviour
{
    public static ResourceSystem Instance { get; private set; }

    // ─── Başlangıç Değerleri ────────────────────────────────────
    [Header("Başlangıç Kaynakları")]
    [SerializeField] private float startingMoney = 15000f;

    // ═══════════════════════════════════════════════════════════════
    // KAYNAK DEĞERLERİ
    // ═══════════════════════════════════════════════════════════════

    // ─── Para (Eko-Para) ────────────────────────────────────────
    public float Money { get; private set; }
    public float IncomePerTick { get; private set; }
    public float ExpensePerTick { get; private set; }
    public float NetIncomePerTick => IncomePerTick - ExpensePerTick;

    // ─── Enerji (MW) ────────────────────────────────────────────
    public float EnergyProduction { get; private set; }
    public float EnergyConsumption { get; private set; }
    public float EnergyBalance => EnergyProduction - EnergyConsumption;

    // ─── Su (m³) ────────────────────────────────────────────────
    public float WaterProduction { get; private set; }
    public float WaterConsumption { get; private set; }
    public float WaterBalance => WaterProduction - WaterConsumption;

    // ─── Nüfus ──────────────────────────────────────────────────
    public int Population { get; private set; }
    public int PopulationCapacity { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    // DURUM BAYRAKLARI (GDD Bölüm 4.1)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Enerji yetersizse karartma (blackout) yaşanır.</summary>
    public bool IsBlackout => EnergyConsumption > EnergyProduction && EnergyConsumption > 0;

    /// <summary>Su yetersizse sağlık endeksi çöker.</summary>
    public bool IsWaterShortage => WaterConsumption > WaterProduction && WaterConsumption > 0;

    /// <summary>Kasa -10.000 altındaysa iflas süreci.</summary>
    public bool IsBankrupt => Money < -10000f;

    // ═══════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Para değiştiğinde (miktar ile).</summary>
    public event Action<float> OnMoneyChanged;

    /// <summary>Tüm kaynaklar yeniden hesaplandığında.</summary>
    public event Action OnResourcesUpdated;

    /// <summary>Karartma başladığında.</summary>
    public event Action OnBlackoutStarted;

    /// <summary>Su kesintisi başladığında.</summary>
    public event Action OnWaterShortageStarted;

    // ─── Private ────────────────────────────────────────────────
    private bool wasBlackout = false;
    private bool wasWaterShortage = false;

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

        Money = startingMoney;
    }

    private void Start()
    {
        // GameManager tick event'ine abone ol
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += ProcessTick;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= ProcessTick;
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK İŞLEME (GDD Bölüm 8 — Pipeline Adım 5: Finans Raporu)
    // ═══════════════════════════════════════════════════════════════

    private void ProcessTick()
    {
        RecalculateFromBuildings();
        ApplyFinancials();
        CheckCrisisStates();

        OnResourcesUpdated?.Invoke();
    }

    /// <summary>Tüm aktif binalardan kaynak toplamlarını hesapla.</summary>
    private void RecalculateFromBuildings()
    {
        float totalIncome = 0f;
        float totalExpense = 0f;
        float totalEnergyProd = 0f;
        float totalEnergyCons = 0f;
        float totalWaterProd = 0f;
        float totalWaterCons = 0f;
        int totalPopCap = 0;

        // BuildingPlacer üzerinden tüm binaları tara
        if (BuildingPlacer.Instance != null)
        {
            foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
            {
                if (building == null || building.Data == null) continue;

                totalIncome += building.GetCurrentTaxIncome();
                totalExpense += building.GetCurrentMaintenanceCost();

                float eDelta = building.GetEnergyDelta();
                if (eDelta > 0f) totalEnergyProd += eDelta;
                else totalEnergyCons += Mathf.Abs(eDelta);

                float wDelta = building.GetWaterDelta();
                if (wDelta > 0f) totalWaterProd += wDelta;
                else totalWaterCons += Mathf.Abs(wDelta);

                if (building.IsActive)
                    totalPopCap += building.Data.populationCapacity;
            }
        }

        IncomePerTick = totalIncome;
        ExpensePerTick = totalExpense;
        EnergyProduction = totalEnergyProd;
        EnergyConsumption = totalEnergyCons;
        WaterProduction = totalWaterProd;
        WaterConsumption = totalWaterCons;
        PopulationCapacity = totalPopCap;

        // Basit nüfus hesabı — ileriki fazlarda CitizenSystem devralacak
        // Şimdilik: kapasite kadar nüfus var (tam doluluk)
        Population = Mathf.Min(Population, totalPopCap);
        if (Population < totalPopCap)
        {
            // Her tick'te kapasiteye doğru yavaş göç
            int migration = Mathf.Max(1, (totalPopCap - Population) / 5);
            Population = Mathf.Min(Population + migration, totalPopCap);
        }
    }

    /// <summary>Finansal işlemleri uygula: vergi topla, bakım öde.</summary>
    private void ApplyFinancials()
    {
        Money += NetIncomePerTick;
        OnMoneyChanged?.Invoke(Money);

        #if UNITY_EDITOR
        if (NetIncomePerTick != 0)
            Debug.Log($"[ResourceSystem] Gün {GameManager.Instance.CurrentDay}: " +
                      $"Gelir: +{IncomePerTick:F0} | Gider: -{ExpensePerTick:F0} | " +
                      $"Net: {NetIncomePerTick:F0} | Kasa: {Money:F0}");
        #endif
    }

    /// <summary>Kriz durumlarını kontrol et ve event tetikle.</summary>
    private void CheckCrisisStates()
    {
        // Blackout kontrolü
        if (IsBlackout && !wasBlackout)
        {
            OnBlackoutStarted?.Invoke();
            #if UNITY_EDITOR
            Debug.LogWarning("[ResourceSystem] ⚡ KARARTMA! Enerji yetersiz.");
            #endif
        }
        wasBlackout = IsBlackout;

        // Su kesintisi kontrolü
        if (IsWaterShortage && !wasWaterShortage)
        {
            OnWaterShortageStarted?.Invoke();
            #if UNITY_EDITOR
            Debug.LogWarning("[ResourceSystem] 💧 SU KESİNTİSİ! Su yetersiz.");
            #endif
        }
        wasWaterShortage = IsWaterShortage;
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM (BuildingPlacer vb. tarafından kullanılır)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen miktarı karşılayabilecek para var mı?</summary>
    public bool CanAfford(int cost)
    {
        return Money >= cost;
    }

    /// <summary>Para harcar. Başarılıysa true döner.</summary>
    public bool SpendMoney(int amount)
    {
        if (Money < amount) return false;
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        return true;
    }

    /// <summary>Para ekler (ödül, hile vb.).</summary>
    public void AddMoney(float amount)
    {
        Money += amount;
        OnMoneyChanged?.Invoke(Money);
    }

    /// <summary>Nüfusu doğrudan ayarlar (CitizenSystem kullanacak).</summary>
    public void SetPopulation(int pop)
    {
        Population = Mathf.Max(0, pop);
    }
}
