using UnityEngine;

/// <summary>
/// Skor hesaplama sistemi.
/// GDD Bölüm 10: Skor Tablosu
/// 
/// Skor Bileşenleri:
///   - Nüfus × 10 puan
///   - Memnuniyet × 2 puan
///   - Sürdürülebilirlik bonusu (düşük kirlilik)
///   - Temiz enerji oranı bonusu
///   - Mali denge bonusu
///   - Zaman cezası (ne kadar hızlı olursa o kadar iyi)
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance { get; private set; }

    // ─── Skor Bileşenleri ───────────────────────────────────────
    public int PopulationScore { get; private set; }
    public int SatisfactionScore { get; private set; }
    public int SustainabilityBonus { get; private set; }
    public int CleanEnergyBonus { get; private set; }
    public int FinancialBonus { get; private set; }
    public int TotalScore { get; private set; }

    // Rank
    public string Rank { get; private set; } = "F";

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
            GameManager.Instance.OnTick += UpdateScore;

        if (GameOverSystem.Instance != null)
            GameOverSystem.Instance.OnGameEnded += OnGameEnded;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= UpdateScore;

        if (GameOverSystem.Instance != null)
            GameOverSystem.Instance.OnGameEnded -= OnGameEnded;
    }

    // ═══════════════════════════════════════════════════════════════
    // SKOR HESAPLAMA
    // ═══════════════════════════════════════════════════════════════

    private void UpdateScore()
    {
        // Nüfus puanı
        PopulationScore = (ResourceSystem.Instance?.Population ?? 0) * 10;

        // Memnuniyet puanı
        SatisfactionScore = Mathf.RoundToInt((CitizenSystem.Instance?.GlobalSatisfaction ?? 0) * 2f);

        // Sürdürülebilirlik bonusu (düşük kirlilik = yüksek bonus)
        if (PollutionSystem.Instance != null)
        {
            float pollutionPct = PollutionSystem.Instance.PollutedAreaPercentage;
            if (pollutionPct < 5f) SustainabilityBonus = 500;
            else if (pollutionPct < 15f) SustainabilityBonus = 300;
            else if (pollutionPct < 30f) SustainabilityBonus = 100;
            else SustainabilityBonus = 0;
        }

        // Temiz enerji oranı bonusu
        if (ResourceSystem.Instance != null && ResourceSystem.Instance.EnergyProduction > 0)
        {
            // Toplam enerjide temiz enerji oranı (güneş vs kömür)
            float totalEnergy = ResourceSystem.Instance.EnergyProduction;
            float dirtyEnergy = 0f;

            if (BuildingPlacer.Instance != null)
            {
                foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
                {
                    if (building?.Data == null) continue;
                    if (building.Data.energyDelta > 0 && building.Data.carbonEmissionPerTick > 0)
                    {
                        dirtyEnergy += building.Data.energyDelta;
                    }
                }
            }

            float cleanRatio = 1f - (dirtyEnergy / totalEnergy);
            CleanEnergyBonus = Mathf.RoundToInt(cleanRatio * 300f);
        }

        // Mali bonus
        if (ResourceSystem.Instance != null)
        {
            float money = ResourceSystem.Instance.Money;
            if (money > 10000f) FinancialBonus = 200;
            else if (money > 5000f) FinancialBonus = 100;
            else if (money > 0f) FinancialBonus = 50;
            else FinancialBonus = 0;
        }

        // Toplam
        TotalScore = PopulationScore + SatisfactionScore + SustainabilityBonus +
                     CleanEnergyBonus + FinancialBonus;

        // Rank hesapla
        Rank = TotalScore switch
        {
            >= 3000 => "S+",
            >= 2500 => "S",
            >= 2000 => "A",
            >= 1500 => "B",
            >= 1000 => "C",
            >= 500 => "D",
            _ => "F"
        };
    }

    private void OnGameEnded(GameOverSystem.GameEndState state)
    {
        // Son skor hesabı
        UpdateScore();

        // Başarı bonusu
        if (state == GameOverSystem.GameEndState.Victory)
        {
            TotalScore += 1000;
            Debug.Log($"[Score] 🏆 Zafer bonusu: +1000!");
        }

        Debug.Log($"[Score] ═══════════════════════════════════════");
        Debug.Log($"[Score] 📊 FİNAL SKOR: {TotalScore}");
        Debug.Log($"[Score]    Nüfus: {PopulationScore}");
        Debug.Log($"[Score]    Memnuniyet: {SatisfactionScore}");
        Debug.Log($"[Score]    Sürdürülebilirlik: {SustainabilityBonus}");
        Debug.Log($"[Score]    Temiz Enerji: {CleanEnergyBonus}");
        Debug.Log($"[Score]    Mali: {FinancialBonus}");
        Debug.Log($"[Score]    RANK: {Rank}");
        Debug.Log($"[Score] ═══════════════════════════════════════");
    }
}
