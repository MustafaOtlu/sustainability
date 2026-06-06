using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject tabanlı görev sistemi.
/// GDD Bölüm 6: Görev Sistemi
/// 
/// 3 görev:
///   1. "Şehre İlk Adım" → 3 konut + 50 nüfus (Ödül: +2000₺)
///   2. "Yeşil Dönüşüm" → Kirlilik <%30 + Park + Hastane (Ödül: +5000₺)
///   3. "Sıfır Karbon" → Memnuniyet >%80 + %100 yenilenebilir enerji (Ödül: Sandbox)
/// </summary>
public class QuestSystem : MonoBehaviour
{
    public static QuestSystem Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    // GÖREV YAPISI
    // ═══════════════════════════════════════════════════════════════

    [Serializable]
    public class Quest
    {
        public string id;
        public string title;
        public string description;
        public int reward;
        public QuestState state;
        public float progress; // 0-1
    }

    public enum QuestState { Locked, Active, Completed }

    private List<Quest> quests = new List<Quest>();

    /// <summary>Tüm görevlere salt okunur erişim.</summary>
    public IReadOnlyList<Quest> AllQuests => quests;

    /// <summary>Aktif görev (varsa).</summary>
    public Quest ActiveQuest => quests.Find(q => q.state == QuestState.Active);

    // Events
    public event Action<Quest> OnQuestCompleted;
    public event Action<Quest> OnQuestActivated;

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

        InitializeQuests();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick += CheckQuests;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnTick -= CheckQuests;
    }

    // ═══════════════════════════════════════════════════════════════
    // GÖREV TANIMLARI
    // ═══════════════════════════════════════════════════════════════

    private void InitializeQuests()
    {
        quests.Add(new Quest
        {
            id = "first_steps",
            title = "🏠 Şehre İlk Adım",
            description = "3 adet konut inşa et ve nüfusu 50 kişiye ulaştır.",
            reward = 2000,
            state = QuestState.Active // Başlangıçta aktif
        });

        quests.Add(new Quest
        {
            id = "green_transition",
            title = "🌿 Yeşil Dönüşüm",
            description = "Hava kirliliğini %30 altına indir, 1 Park ve 1 Hastane yap.",
            reward = 5000,
            state = QuestState.Locked
        });

        quests.Add(new Quest
        {
            id = "zero_carbon",
            title = "♻️ Sıfır Karbon",
            description = "Memnuniyeti %80 üzerinde tut ve %100 yenilenebilir enerjiye geç.",
            reward = 10000,
            state = QuestState.Locked
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // GÖREV KONTROLÜ (her tick)
    // ═══════════════════════════════════════════════════════════════

    private void CheckQuests()
    {
        foreach (var quest in quests)
        {
            if (quest.state != QuestState.Active) continue;

            switch (quest.id)
            {
                case "first_steps":
                    CheckFirstSteps(quest);
                    break;
                case "green_transition":
                    CheckGreenTransition(quest);
                    break;
                case "zero_carbon":
                    CheckZeroCarbon(quest);
                    break;
            }
        }
    }

    // ─── Görev 1: Şehre İlk Adım ───────────────────────────────
    private void CheckFirstSteps(Quest quest)
    {
        if (BuildingPlacer.Instance == null || ResourceSystem.Instance == null) return;

        int houseCount = 0;
        foreach (var b in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (b?.Data?.category == BuildingData.BuildingCategory.Residential)
                houseCount++;
        }

        int pop = ResourceSystem.Instance.Population;
        float houseProg = Mathf.Clamp01(houseCount / 3f);
        float popProg = Mathf.Clamp01(pop / 50f);
        quest.progress = (houseProg + popProg) / 2f;

        if (houseCount >= 3 && pop >= 50)
            CompleteQuest(quest);
    }

    // ─── Görev 2: Yeşil Dönüşüm ────────────────────────────────
    private void CheckGreenTransition(Quest quest)
    {
        if (PollutionSystem.Instance == null || BuildingPlacer.Instance == null) return;

        bool lowPollution = PollutionSystem.Instance.PollutedAreaPercentage < 30f;
        bool hasPark = false, hasHospital = false;

        foreach (var b in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (b?.Data == null) continue;
            if (b.Data.category == BuildingData.BuildingCategory.Environmental) hasPark = true;
            if (b.Data.category == BuildingData.BuildingCategory.Healthcare) hasHospital = true;
        }

        float prog = 0f;
        if (lowPollution) prog += 0.34f;
        if (hasPark) prog += 0.33f;
        if (hasHospital) prog += 0.33f;
        quest.progress = prog;

        if (lowPollution && hasPark && hasHospital)
            CompleteQuest(quest);
    }

    // ─── Görev 3: Sıfır Karbon ──────────────────────────────────
    private void CheckZeroCarbon(Quest quest)
    {
        if (CitizenSystem.Instance == null || BuildingPlacer.Instance == null) return;

        bool highSatisfaction = CitizenSystem.Instance.GlobalSatisfaction >= 80f;

        // Tüm enerji santrallerinin temiz olup olmadığını kontrol et
        bool allClean = true;
        bool hasAnyPower = false;

        foreach (var b in BuildingPlacer.Instance.PlacedBuildings)
        {
            if (b?.Data == null) continue;
            if (b.Data.category == BuildingData.BuildingCategory.Power)
            {
                hasAnyPower = true;
                if (b.Data.carbonEmissionPerTick > 0f)
                {
                    allClean = false;
                    break;
                }
            }
        }

        float prog = 0f;
        if (highSatisfaction) prog += 0.5f;
        if (allClean && hasAnyPower) prog += 0.5f;
        quest.progress = prog;

        if (highSatisfaction && allClean && hasAnyPower)
            CompleteQuest(quest);
    }

    // ═══════════════════════════════════════════════════════════════
    // GÖREV TAMAMLAMA
    // ═══════════════════════════════════════════════════════════════

    private void CompleteQuest(Quest quest)
    {
        quest.state = QuestState.Completed;
        quest.progress = 1f;

        // Ödül ver
        if (ResourceSystem.Instance != null)
        {
            ResourceSystem.Instance.AddMoney(quest.reward);
        }

        Debug.Log($"[QuestSystem] 🎉 GÖREV TAMAMLANDI: {quest.title} — Ödül: +{quest.reward}₺");
        OnQuestCompleted?.Invoke(quest);

        // Sıradaki görevi aç
        ActivateNextQuest();
    }

    private void ActivateNextQuest()
    {
        foreach (var quest in quests)
        {
            if (quest.state == QuestState.Locked)
            {
                quest.state = QuestState.Active;
                Debug.Log($"[QuestSystem] 📋 Yeni görev açıldı: {quest.title}");
                OnQuestActivated?.Invoke(quest);
                break;
            }
        }
    }
}
