using UnityEngine;
using System;

/// <summary>
/// Oyunun ana yöneticisi. Tick (oyun günü) döngüsünü, oyun hızını ve durumunu kontrol eder.
/// Tüm sistemler OnTick event'ine abone olarak her tick'te güncellenir.
/// GDD Bölüm 8: Her 5 gerçek saniyede 1 "Tick" (Oyun Günü) ilerler.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ─── Oyun Durumları ─────────────────────────────────────────
    public enum GameState { Playing, Paused, GameOver, Victory }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Tick Ayarları")]
    [Tooltip("Bir tick (oyun günü) kaç gerçek saniyede ilerler")]
    [SerializeField] private float tickInterval = 5f;

    // ─── Public Erişim ──────────────────────────────────────────
    public GameState CurrentState { get; private set; } = GameState.Paused;
    public int CurrentDay { get; private set; } = 0;
    public float SpeedMultiplier { get; private set; } = 1f;
    public float TickProgress => tickTimer / tickInterval; // 0-1 arası tick ilerleme yüzdesi

    // ─── Events ─────────────────────────────────────────────────
    /// <summary>Her oyun günü (tick) başında tetiklenir. Tüm sistemler buna abone olur.</summary>
    public event Action OnTick;

    /// <summary>Oyun durumu değiştiğinde tetiklenir.</summary>
    public event Action<GameState> OnGameStateChanged;

    /// <summary>Oyun hızı değiştiğinde tetiklenir.</summary>
    public event Action<float> OnSpeedChanged;

    // ─── Private ────────────────────────────────────────────────
    private float tickTimer;

    // ═══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Oyunu başlat
        SetGameState(GameState.Playing);
    }

    private void Update()
    {
        if (CurrentState != GameState.Playing) return;

        tickTimer += Time.deltaTime * SpeedMultiplier;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            ProcessTick();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TICK SİSTEMİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick Pipeline (GDD Bölüm 8):
    /// 1. Altyapı Kontrolü
    /// 2. Çevre Güncellemesi
    /// 3. Lojistik Çözümü
    /// 4. Vatandaş Hesaplaması
    /// 5. Finans Raporu
    /// Tüm bu sıralama OnTick'e abone olan sistemlerin öncelik sırasına bağlıdır.
    /// </summary>
    private void ProcessTick()
    {
        CurrentDay++;
        OnTick?.Invoke();

        #if UNITY_EDITOR
        Debug.Log($"[GameManager] Gün {CurrentDay} tamamlandı.");
        #endif
    }

    // ═══════════════════════════════════════════════════════════════
    // HIZ KONTROLÜ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Oyun hızını ayarlar. 0 = Duraklat, 1 = Normal, 2 = Hızlı, 3 = Çok Hızlı</summary>
    public void SetSpeed(float multiplier)
    {
        SpeedMultiplier = Mathf.Clamp(multiplier, 0f, 3f);

        if (Mathf.Approximately(SpeedMultiplier, 0f))
        {
            SetGameState(GameState.Paused);
        }
        else if (CurrentState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
        }

        OnSpeedChanged?.Invoke(SpeedMultiplier);
    }

    /// <summary>Oyunu duraklatır.</summary>
    public void Pause()
    {
        SpeedMultiplier = 0f;
        SetGameState(GameState.Paused);
        OnSpeedChanged?.Invoke(0f);
    }

    /// <summary>Oyunu devam ettirir (son hızda).</summary>
    public void Resume()
    {
        if (SpeedMultiplier <= 0f) SpeedMultiplier = 1f;
        SetGameState(GameState.Playing);
        OnSpeedChanged?.Invoke(SpeedMultiplier);
    }

    // ═══════════════════════════════════════════════════════════════
    // DURUM YÖNETİMİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Oyun durumunu değiştirir.</summary>
    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);

        #if UNITY_EDITOR
        Debug.Log($"[GameManager] Oyun durumu: {newState}");
        #endif
    }

    /// <summary>Gün sayacını doğrudan ayarlar (SaveLoadSystem için).</summary>
    public void SetDay(int day)
    {
        CurrentDay = Mathf.Max(0, day);
    }
}
