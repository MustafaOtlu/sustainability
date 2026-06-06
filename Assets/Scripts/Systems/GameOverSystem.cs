using UnityEngine;
using System;

/// <summary>
/// Oyun sonu koşullarını izleyen ve tetikleyen sistem.
/// GDD Bölüm 9: Oyun Sonu Koşulları
/// 
/// Başarısızlık:
///   - Kirli alan %70'i aşarsa → Çevre felaketi
///   - Memnuniyet %30 altına düşüp 30 gün kalırsa → İsyan
///   - Kasa -10.000 altına düşerse → İflas
/// 
/// Başarı:
///   - Nüfus hedefine ulaşma + yüksek memnuniyet + düşük kirlilik
/// </summary>
public class GameOverSystem : MonoBehaviour
{
    public static GameOverSystem Instance { get; private set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Başarısızlık Eşikleri (GDD Bölüm 9)")]
    [Tooltip("Kirli alan bu yüzdeyi aşarsa oyun biter")]
    [SerializeField] private float pollutionFailThreshold = 70f;

    [Tooltip("Memnuniyet bu altında kalırsa süre başlar")]
    [SerializeField] private float satisfactionFailThreshold = 30f;

    [Tooltip("Düşük memnuniyet kaç gün sürerken oyun biter")]
    [SerializeField] private int satisfactionFailDays = 30;

    [Tooltip("Kasa bu değerin altına düşerse oyun biter")]
    [SerializeField] private float bankruptcyThreshold = -10000f;

    [Header("Başarı Koşulları")]
    [Tooltip("Kazanmak için gereken minimum nüfus")]
    [SerializeField] private int victoryPopulation = 100;

    [Tooltip("Kazanmak için minimum memnuniyet")]
    [SerializeField] private float victorySatisfaction = 70f;

    [Tooltip("Kazanmak için maksimum kirli alan yüzdesi")]
    [SerializeField] private float victoryMaxPollution = 20f;

    [Tooltip("Başarı koşulları kaç gün boyunca sürdürülmeli")]
    [SerializeField] private int victoryRequiredDays = 50;

    // ═══════════════════════════════════════════════════════════════
    // DURUM
    // ═══════════════════════════════════════════════════════════════

    public enum GameEndState { Playing, Victory, FailPollution, FailRiot, FailBankruptcy }

    public GameEndState CurrentEndState { get; private set; } = GameEndState.Playing;
    public bool IsGameOver => CurrentEndState != GameEndState.Playing;

    // Başarısızlık sayaçları
    private int lowSatisfactionDays = 0;
    private int victoryProgressDays = 0;

    // Events
    public event Action<GameEndState> OnGameEnded;
    public event Action<int> OnVictoryProgress; // kalan gün

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
            GameManager.Instance.OnTick += CheckConditions;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= CheckConditions;
    }

    // ═══════════════════════════════════════════════════════════════
    // KOŞUL KONTROLÜ
    // ═══════════════════════════════════════════════════════════════

    private void CheckConditions()
    {
        if (IsGameOver) return;

        // İlk 20 gün kontrol yapma (kurulum süresi)
        if (GameManager.Instance != null && GameManager.Instance.CurrentDay < 20) return;

        // ─── BAŞARISIZLIK KONTROLLERI ─────────────────────────
        if (CheckPollutionFail()) return;
        if (CheckRiotFail()) return;
        if (CheckBankruptcyFail()) return;

        // ─── BAŞARI KONTROLÜ ─────────────────────────────────
        CheckVictory();
    }

    // ─── Çevre Felaketi ─────────────────────────────────────────
    private bool CheckPollutionFail()
    {
        if (PollutionSystem.Instance == null) return false;

        if (PollutionSystem.Instance.PollutedAreaPercentage >= pollutionFailThreshold)
        {
            TriggerGameOver(GameEndState.FailPollution);
            return true;
        }
        return false;
    }

    // ─── İsyan ──────────────────────────────────────────────────
    private bool CheckRiotFail()
    {
        if (CitizenSystem.Instance == null) return false;

        if (CitizenSystem.Instance.GlobalSatisfaction < satisfactionFailThreshold)
        {
            lowSatisfactionDays++;

            if (lowSatisfactionDays % 10 == 0)
            {
                int remaining = satisfactionFailDays - lowSatisfactionDays;
                Debug.LogWarning($"[GameOver] ⚠ Memnuniyet düşük! İsyana {remaining} gün kaldı!");
            }

            if (lowSatisfactionDays >= satisfactionFailDays)
            {
                TriggerGameOver(GameEndState.FailRiot);
                return true;
            }
        }
        else
        {
            // Toparlanma
            if (lowSatisfactionDays > 0)
            {
                lowSatisfactionDays = Mathf.Max(0, lowSatisfactionDays - 2);
            }
        }
        return false;
    }

    // ─── İflas ──────────────────────────────────────────────────
    private bool CheckBankruptcyFail()
    {
        if (ResourceSystem.Instance == null) return false;

        if (ResourceSystem.Instance.Money <= bankruptcyThreshold)
        {
            TriggerGameOver(GameEndState.FailBankruptcy);
            return true;
        }
        return false;
    }

    // ─── Başarı ─────────────────────────────────────────────────
    private void CheckVictory()
    {
        bool popOk = ResourceSystem.Instance != null &&
                     ResourceSystem.Instance.Population >= victoryPopulation;

        bool satOk = CitizenSystem.Instance != null &&
                     CitizenSystem.Instance.GlobalSatisfaction >= victorySatisfaction;

        bool polOk = PollutionSystem.Instance != null &&
                     PollutionSystem.Instance.PollutedAreaPercentage <= victoryMaxPollution;

        if (popOk && satOk && polOk)
        {
            victoryProgressDays++;
            OnVictoryProgress?.Invoke(victoryRequiredDays - victoryProgressDays);

            if (victoryProgressDays % 10 == 0)
            {
                Debug.Log($"[GameOver] 🏆 Başarıya {victoryRequiredDays - victoryProgressDays} gün kaldı!");
            }

            if (victoryProgressDays >= victoryRequiredDays)
            {
                TriggerGameOver(GameEndState.Victory);
            }
        }
        else
        {
            // Koşullardan biri bozulursa sayaç sıfırlanır
            if (victoryProgressDays > 0)
            {
                victoryProgressDays = Mathf.Max(0, victoryProgressDays - 1);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // OYUN SONU TETİKLEME
    // ═══════════════════════════════════════════════════════════════

    private void TriggerGameOver(GameEndState state)
    {
        CurrentEndState = state;

        // Oyunu duraklat
        if (GameManager.Instance != null)
            GameManager.Instance.Pause();

        string message = state switch
        {
            GameEndState.FailPollution => "🌍 ÇEVRE FELAKETİ! Kirli alan %70'i aştı.",
            GameEndState.FailRiot => "🔥 HALK İSYANI! Memnuniyet 30 gün boyunca %30 altında kaldı.",
            GameEndState.FailBankruptcy => "💸 İFLAS! Kasa -10.000 altına düştü.",
            GameEndState.Victory => "🏆 TEBRİKLER! Sürdürülebilir şehir hedefine ulaştınız!",
            _ => "Oyun bitti."
        };

        Debug.Log($"[GameOver] ═══════════════════════════════════════");
        Debug.Log($"[GameOver] {message}");
        Debug.Log($"[GameOver] Gün: {GameManager.Instance?.CurrentDay ?? 0}");
        Debug.Log($"[GameOver] ═══════════════════════════════════════");

        OnGameEnded?.Invoke(state);
    }

    // ═══════════════════════════════════════════════════════════════
    // DIŞ ERİŞİM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Oyunu yeniden başlatır (sahne yeniden yüklenir).</summary>
    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    /// <summary>Başarı ilerlemesi yüzdesi (0-100).</summary>
    public float VictoryProgress => (float)victoryProgressDays / victoryRequiredDays * 100f;

    /// <summary>İsyana kalan gün (< 0 ise tehlike yok).</summary>
    public int DaysUntilRiot => satisfactionFailDays - lowSatisfactionDays;
}
