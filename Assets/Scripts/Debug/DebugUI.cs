using UnityEngine;

/// <summary>
/// Faz 0-1 doğrulaması için geçici debug arayüzü.
/// Sol üst: Oyun durumu ve kaynaklar
/// Sol alt: Bina seçim menüsü
/// Alt orta: Kontrol bilgisi
/// İleri fazlarda gerçek uGUI HUD ile değiştirilecektir.
/// </summary>
public class DebugUI : MonoBehaviour
{
    // ─── Stiller ────────────────────────────────────────────────
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle headerStyle;
    private GUIStyle smallLabelStyle;
    private GUIStyle activeButtonStyle;
    private GUIStyle warningStyle;
    private bool stylesInitialized = false;

    // ─── Scroll pozisyonu (bina listesi) ────────────────────────
    private Vector2 buildingScrollPos;

    // ─── Son tıklanan hücre ─────────────────────────────────────
    private string lastClickInfo = "—";

    // ─── Yol yerleştirme modu ───────────────────────────────────
    private bool isPlacingRoads = false;

    // ─── UI Panel alanları (click-through engelleme) ────────────
    private Rect statusPanelRect;
    private Rect buildingMenuRect;
    private Rect controlsBarRect;

    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnCellClicked += OnCellClicked;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnCellClicked -= OnCellClicked;
    }

    private void OnCellClicked(Vector2Int cell)
    {
        // Yol yerleştirme modu aktifse
        if (isPlacingRoads && RoadSystem.Instance != null)
        {
            if (RoadSystem.Instance.PlaceRoad(cell.x, cell.y))
                lastClickInfo = $"🛣️ Yol ({cell.x}, {cell.y})";
            else
                lastClickInfo = $"❌ Yol yerleştirilemedi ({cell.x}, {cell.y})";
            return;
        }

        string cellType = "?";
        if (GridSystem.Instance != null)
            cellType = GridSystem.Instance.GetCellType(cell.x, cell.y).ToString();
        lastClickInfo = $"({cell.x}, {cell.y}) [{cellType}]";
    }

    // ═══════════════════════════════════════════════════════════════
    // STİL BAŞLATMA
    // ═══════════════════════════════════════════════════════════════

    private void InitStyles()
    {
        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.08f, 0.85f));
        boxStyle.padding = new RectOffset(8, 8, 8, 8);

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 13;
        labelStyle.normal.textColor = Color.white;

        smallLabelStyle = new GUIStyle(GUI.skin.label);
        smallLabelStyle.fontSize = 11;
        smallLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 15;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 12;
        buttonStyle.fixedHeight = 26;

        activeButtonStyle = new GUIStyle(GUI.skin.button);
        activeButtonStyle.fontSize = 12;
        activeButtonStyle.fixedHeight = 26;
        activeButtonStyle.normal.textColor = Color.yellow;
        activeButtonStyle.fontStyle = FontStyle.Bold;

        warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.fontSize = 13;
        warningStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
        warningStyle.fontStyle = FontStyle.Bold;

        stylesInitialized = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // GUI ÇİZİMİ
    // ═══════════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (!stylesInitialized) InitStyles();

        DrawStatusPanel();
        DrawBuildingMenu();
        DrawQuestPanel();
        DrawControlsBar();

        // Oyun sonu ekranı (varsa)
        if (GameOverSystem.Instance != null && GameOverSystem.Instance.IsGameOver)
            DrawGameOverOverlay();

        // ─── Mouse'un OnGUI panelleri üzerinde olup olmadığını kontrol et ───
        Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        bool overAnyPanel = statusPanelRect.Contains(mousePos)
                         || buildingMenuRect.Contains(mousePos)
                         || controlsBarRect.Contains(mousePos);

        InputManager.IsPointerBlockedByUI = overAnyPanel;
    }

    // ─── SOL ÜST: Durum Paneli ─────────────────────────────────
    private void DrawStatusPanel()
    {
        float w = 300f;
        statusPanelRect = new Rect(10, 10, w, 530);
        GUILayout.BeginArea(statusPanelRect, boxStyle);

        GUILayout.Label("🏙️ Şehir Durumu", headerStyle);
        GUILayout.Space(3);

        // Oyun durumu
        if (GameManager.Instance != null)
        {
            var gm = GameManager.Instance;
            string stateIcon = gm.CurrentState == GameManager.GameState.Playing ? "▶" : "⏸";
            GUILayout.Label($"{stateIcon} Gün: {gm.CurrentDay}  |  Hız: {gm.SpeedMultiplier:F0}x", labelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⏸", buttonStyle, GUILayout.Width(35))) gm.Pause();
            if (GUILayout.Button("1x", buttonStyle, GUILayout.Width(35))) { gm.Resume(); gm.SetSpeed(1f); }
            if (GUILayout.Button("2x", buttonStyle, GUILayout.Width(35))) { gm.Resume(); gm.SetSpeed(2f); }
            if (GUILayout.Button("3x", buttonStyle, GUILayout.Width(35))) { gm.Resume(); gm.SetSpeed(3f); }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);

        // Kaynaklar
        if (ResourceSystem.Instance != null)
        {
            var rs = ResourceSystem.Instance;

            // Para
            string moneyColor = rs.Money > 0 ? "#66FF66" : "#FF6666";
            GUILayout.Label($"💰 Para: <color={moneyColor}>{rs.Money:F0}</color>  " +
                          $"(<color=#88FF88>+{rs.IncomePerTick:F0}</color> / " +
                          $"<color=#FF8888>-{rs.ExpensePerTick:F0}</color>)",
                          CreateRichLabel());

            // Enerji
            string energyIcon = rs.IsBlackout ? "⚡❌" : "⚡";
            GUILayout.Label($"{energyIcon} Enerji: {rs.EnergyProduction:F0} / {rs.EnergyConsumption:F0} MW", labelStyle);
            if (rs.IsBlackout)
                GUILayout.Label("   ⚠ KARARTMA! Enerji yetersiz!", warningStyle);

            // Su
            string waterIcon = rs.IsWaterShortage ? "💧❌" : "💧";
            GUILayout.Label($"{waterIcon} Su: {rs.WaterProduction:F0} / {rs.WaterConsumption:F0} m³", labelStyle);
            if (rs.IsWaterShortage)
                GUILayout.Label("   ⚠ SU KESİNTİSİ!", warningStyle);

            // Nüfus
            string migrationText = "";
            if (CitizenSystem.Instance != null && CitizenSystem.Instance.LastMigration != 0)
            {
                int m = CitizenSystem.Instance.LastMigration;
                migrationText = m > 0 ? $" <color=#88FF88>+{m}↑</color>" : $" <color=#FF8888>{m}↓</color>";
            }
            GUILayout.Label($"👤 Nüfus: {rs.Population} / {rs.PopulationCapacity}{migrationText}", CreateRichLabel());
        }

        GUILayout.Space(4);

        // ─── Çevre Metrikleri ───
        if (PollutionSystem.Instance != null)
        {
            var ps = PollutionSystem.Instance;
            string airColor = ps.AverageAirPollution > 5f ? "#FFAA44" : "#88FF88";
            GUILayout.Label($"🌫 Hava Kirliliği: <color={airColor}>{ps.AverageAirPollution:F1}</color>  |  " +
                          $"Kirli Alan: {ps.PollutedAreaPercentage:F0}%", CreateRichLabel());
            GUILayout.Label($"🏭 Karbon: {ps.GlobalCarbonEmission:F0}", smallLabelStyle);
        }

        if (NoiseSystem.Instance != null)
        {
            GUILayout.Label($"🔊 Ort. Gürültü: {NoiseSystem.Instance.AverageNoise:F1} dB", smallLabelStyle);
        }

        // ─── Vatandaş Metrikleri ───
        if (CitizenSystem.Instance != null)
        {
            var cs = CitizenSystem.Instance;
            string satColor = cs.GlobalSatisfaction > 50 ? "#88FF88" : cs.GlobalSatisfaction > 30 ? "#FFAA44" : "#FF4444";
            GUILayout.Label($"😊 Memnuniyet: <color={satColor}>{cs.GlobalSatisfaction:F0}%</color>  |  " +
                          $"Sağlık: {cs.GlobalHealth:F0}  |  Mutluluk: {cs.GlobalHappiness:F0}", CreateRichLabel());
        }

        // ─── Skor ───
        if (ScoreSystem.Instance != null)
        {
            GUILayout.Label($"🏅 Skor: {ScoreSystem.Instance.TotalScore} | Rank: {ScoreSystem.Instance.Rank}", labelStyle);
        }

        // ─── Zafer İlerlemesi ───
        if (GameOverSystem.Instance != null && !GameOverSystem.Instance.IsGameOver)
        {
            float progress = GameOverSystem.Instance.VictoryProgress;
            if (progress > 0f)
            {
                GUILayout.Label($"🏆 Zafer: {progress:F0}%", CreateRichLabel());
            }
            int riotDays = GameOverSystem.Instance.DaysUntilRiot;
            if (riotDays < 20)
            {
                GUILayout.Label($"<color=#FF4444>🔥 İsyana {riotDays} gün!</color>", CreateRichLabel());
            }
        }

        GUILayout.Space(3);

        // Hover/Tıklama
        if (InputManager.Instance != null)
        {
            var im = InputManager.Instance;
            string hover = im.IsPointerOverGrid ? $"({im.HoveredCell.x},{im.HoveredCell.y})" : "—";
            GUILayout.Label($"🖱 Hover: {hover}  |  Tık: {lastClickInfo}", smallLabelStyle);
        }

        // Mod
        if (BuildingPlacer.Instance != null)
        {
            string modText = BuildingPlacer.Instance.CurrentMode switch
            {
                BuildingPlacer.PlacementMode.Building => $"🔨 İnşaat: {BuildingPlacer.Instance.SelectedBuilding?.buildingName}",
                BuildingPlacer.PlacementMode.Demolish => "🔴 Yıkım Modu",
                _ => "📌 Seçim Modu"
            };
            GUILayout.Label(modText, smallLabelStyle);
        }

        GUILayout.EndArea();
    }

    // ─── SOL ALT: Bina Menüsü ──────────────────────────────────
    private void DrawBuildingMenu()
    {
        if (BuildingPlacer.Instance == null) return;
        var bp = BuildingPlacer.Instance;
        var buildings = bp.AvailableBuildings;
        if (buildings == null || buildings.Length == 0) return;

        float panelWidth = 300f;
        float menuStartY = 550f; // Status panelinin altından başla
        float panelHeight = Screen.height - menuStartY - 50f;
        if (panelHeight < 150f) panelHeight = 150f;

        buildingMenuRect = new Rect(10, menuStartY, panelWidth, panelHeight);
        GUILayout.BeginArea(buildingMenuRect, boxStyle);
        GUILayout.Label("🏗️ İnşaat Menüsü", headerStyle);
        GUILayout.Space(3);

        // Yıkım butonu
        GUIStyle demolishStyle = bp.CurrentMode == BuildingPlacer.PlacementMode.Demolish
            ? activeButtonStyle : buttonStyle;
        if (GUILayout.Button("🔨 Yıkım Modu (Bina Sil)", demolishStyle))
        {
            if (bp.CurrentMode == BuildingPlacer.PlacementMode.Demolish)
                bp.CancelMode();
            else
                bp.EnterDemolishMode();
        }

        // Yol yerleştirme butonu
        bool isRoadMode = isPlacingRoads;
        GUIStyle roadStyle = isRoadMode ? activeButtonStyle : buttonStyle;
        if (GUILayout.Button("🛣️ Yol Yerleştir (50₺)", roadStyle))
        {
            isPlacingRoads = !isPlacingRoads;
            if (isPlacingRoads) bp.CancelMode();
        }

        // Trafik bilgisi
        if (RoadSystem.Instance != null && RoadSystem.Instance.TotalRoads > 0)
        {
            var rs = RoadSystem.Instance;
            string congColor = rs.AverageCongestion > 0.7f ? "#FF6644" : "#AAAAAA";
            GUILayout.Label($"   🚗 Yol: {rs.TotalRoads} | " +
                          $"Trafik: <color={congColor}>{rs.AverageCongestion:P0}</color> | " +
                          $"Bakım: -{rs.TotalRoadMaintenance}/tick", CreateRichLabel());
        }

        // İptal butonu
        if (bp.CurrentMode != BuildingPlacer.PlacementMode.None || isPlacingRoads)
        {
            if (GUILayout.Button("❌ İptal (ESC / Sağ Tık)", buttonStyle))
            {
                bp.CancelMode();
                isPlacingRoads = false;
            }
        }

        GUILayout.Space(5);

        // Bina listesi (scrollable)
        buildingScrollPos = GUILayout.BeginScrollView(buildingScrollPos);

        string lastCategory = "";
        foreach (var building in buildings)
        {
            if (building == null) continue;

            // Kategori başlığı
            string catName = GetCategoryName(building.category);
            if (catName != lastCategory)
            {
                GUILayout.Space(4);
                GUILayout.Label($"── {catName} ──", smallLabelStyle);
                lastCategory = catName;
            }

            // Bina butonu
            bool isSelected = bp.SelectedBuilding == building;
            GUIStyle style = isSelected ? activeButtonStyle : buttonStyle;

            bool canAfford = ResourceSystem.Instance == null || ResourceSystem.Instance.CanAfford(building.buildCost);
            string affordIcon = canAfford ? "✅" : "❌";

            string btnText = $"{affordIcon} {building.buildingName} ({building.gridWidth}x{building.gridHeight}) — {building.buildCost}₺";

            if (GUILayout.Button(btnText, style))
            {
                if (isSelected)
                    bp.CancelMode();
                else
                    bp.SelectBuilding(building);
            }

            // Kısa bilgi
            if (building.energyDelta != 0)
            {
                string eText = building.energyDelta > 0
                    ? $"+{building.energyDelta:F0} MW"
                    : $"{building.energyDelta:F0} MW";
                GUILayout.Label($"   ⚡{eText}  💧{building.waterDelta:F0}m³  💰{building.taxIncomePerTick:F0}/tick", smallLabelStyle);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ─── ALT ORTA: Kontrol Bilgisi ─────────────────────────────
    private void DrawControlsBar()
    {
        float w = 600f;
        float h = 28f;
        float x = (Screen.width - w) / 2f;
        float y = Screen.height - h - 8f;

        controlsBarRect = new Rect(x, y, w, h);
        GUI.Box(controlsBarRect, "", boxStyle);
        GUI.Label(new Rect(x + 8, y + 4, w - 16, h - 8),
            "WASD:Kaydır | Scroll:Zoom | Sol Tık:Yerleştir | ESC:İptal | F5:Kaydet | F9:Yükle | T:Danışman",
            smallLabelStyle);
    }

    // ─── OYUN SONU OVERLAY ────────────────────────────────────
    private void DrawGameOverOverlay()
    {
        if (GameOverSystem.Instance == null) return;

        // Yarı-saydam arka plan
        Texture2D overlayBg = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.75f));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayBg);

        float panelW = 500f;
        float panelH = 350f;
        float panelX = (Screen.width - panelW) / 2f;
        float panelY = (Screen.height - panelH) / 2f;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH), boxStyle);

        // Başlık
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        bool isVictory = GameOverSystem.Instance.CurrentEndState == GameOverSystem.GameEndState.Victory;
        titleStyle.normal.textColor = isVictory ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);

        string title = GameOverSystem.Instance.CurrentEndState switch
        {
            GameOverSystem.GameEndState.Victory => "🏆 ZAFER!",
            GameOverSystem.GameEndState.FailPollution => "🌍 ÇEVRE FELAKETİ!",
            GameOverSystem.GameEndState.FailRiot => "🔥 HALK İSYANI!",
            GameOverSystem.GameEndState.FailBankruptcy => "💸 İFLAS!",
            _ => "OYUN BİTTİ"
        };
        GUILayout.Label(title, titleStyle);
        GUILayout.Space(10);

        // Alt başlık
        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 15;
        subStyle.normal.textColor = Color.white;
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.wordWrap = true;

        string subtitle = GameOverSystem.Instance.CurrentEndState switch
        {
            GameOverSystem.GameEndState.Victory => "Sürdürülebilir bir şehir inşa ettiniz. Tebrikler!",
            GameOverSystem.GameEndState.FailPollution => "Kirli alan %70'i aştı. İnsan yaşamı sürdürülemez.",
            GameOverSystem.GameEndState.FailRiot => "Vatandaşlar 30 gün boyunca mutsuz kaldı. Yönetim değişti.",
            GameOverSystem.GameEndState.FailBankruptcy => "Şehir kasası -10.000 altına düştü. Mali çöküş.",
            _ => ""
        };
        GUILayout.Label(subtitle, subStyle);
        GUILayout.Space(15);

        // Skor
        if (ScoreSystem.Instance != null)
        {
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label);
            scoreStyle.fontSize = 22;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            scoreStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label($"SKOR: {ScoreSystem.Instance.TotalScore}  |  RANK: {ScoreSystem.Instance.Rank}", scoreStyle);

            GUILayout.Space(5);
            GUIStyle detailStyle = new GUIStyle(GUI.skin.label);
            detailStyle.fontSize = 12;
            detailStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            detailStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(
                $"Nüfus: {ScoreSystem.Instance.PopulationScore} | " +
                $"Memnuniyet: {ScoreSystem.Instance.SatisfactionScore} | " +
                $"Sürdürülebilirlik: {ScoreSystem.Instance.SustainabilityBonus} | " +
                $"Temiz Enerji: {ScoreSystem.Instance.CleanEnergyBonus} | " +
                $"Mali: {ScoreSystem.Instance.FinancialBonus}",
                detailStyle);
        }

        GUILayout.Space(15);
        GUILayout.Label($"Gün: {GameManager.Instance?.CurrentDay ?? 0}", subStyle);

        GUILayout.Space(15);

        // Yeniden başlat butonu
        GUIStyle restartStyle = new GUIStyle(GUI.skin.button);
        restartStyle.fontSize = 18;
        restartStyle.fixedHeight = 40;
        if (GUILayout.Button("🔄 Yeniden Başlat", restartStyle))
        {
            GameOverSystem.Instance?.RestartGame();
        }

        GUILayout.EndArea();
    }

    // ─── SAĞ ÜST: Görev Paneli ─────────────────────────────────
    private void DrawQuestPanel()
    {
        if (QuestSystem.Instance == null) return;

        float panelW = 280f;
        float panelH = 120f;
        float panelX = Screen.width - panelW - 10f;
        float panelY = 10f;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH), boxStyle);
        GUILayout.Label("📋 Görevler", headerStyle);
        GUILayout.Space(3);

        foreach (var quest in QuestSystem.Instance.AllQuests)
        {
            if (quest.state == QuestSystem.QuestState.Locked) continue;

            string stateIcon = quest.state == QuestSystem.QuestState.Completed ? "✅" : "🔄";
            string progressText = quest.state == QuestSystem.QuestState.Completed
                ? "Tamamlandı!"
                : $"{quest.progress * 100:F0}%";

            string color = quest.state == QuestSystem.QuestState.Completed ? "#88FF88" : "#FFDD66";
            GUILayout.Label($"<color={color}>{stateIcon} {quest.title}</color>", CreateRichLabel());
            if (quest.state == QuestSystem.QuestState.Active)
            {
                GUILayout.Label($"   {quest.description}\n   İlerleme: {progressText}  Ödül: +{quest.reward}₺",
                    smallLabelStyle);
            }
        }

        GUILayout.EndArea();
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    private string GetCategoryName(BuildingData.BuildingCategory cat)
    {
        return cat switch
        {
            BuildingData.BuildingCategory.Residential => "🏠 Konut",
            BuildingData.BuildingCategory.Industrial => "🏭 Sanayi",
            BuildingData.BuildingCategory.Environmental => "🌳 Çevre",
            BuildingData.BuildingCategory.Social => "🎭 Toplum",
            BuildingData.BuildingCategory.Healthcare => "🏥 Sağlık",
            BuildingData.BuildingCategory.Power => "⚡ Enerji",
            BuildingData.BuildingCategory.Water => "💧 Su",
            _ => "Diğer"
        };
    }

    private GUIStyle CreateRichLabel()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        s.fontSize = 13;
        return s;
    }

    private Texture2D MakeTex(int w, int h, Color c)
    {
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        Texture2D t = new Texture2D(w, h);
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
