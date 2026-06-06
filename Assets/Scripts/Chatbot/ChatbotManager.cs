using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Chatbot ana entegrasyon hattı ve IMGUI sohbet penceresi.
/// GDD AI-CSDD Bölüm 8: Unity Entegrasyonu
/// 
/// Oyuncu T tuşuyla sohbet penceresini açar/kapatır.
/// Serbest metin girer → NLP → Context → Response → Ekran
/// </summary>
public class ChatbotManager : MonoBehaviour
{
    public static ChatbotManager Instance { get; private set; }

    // ─── Sohbet Geçmişi ─────────────────────────────────────────
    [Serializable]
    public class ChatMessage
    {
        public string sender; // "Oyuncu" veya "Danışman"
        public string text;
        public float timestamp;
    }

    private List<ChatMessage> chatHistory = new List<ChatMessage>();
    private const int MAX_HISTORY = 30;

    // ─── Hafıza Sistemi (GDD AI-CSDD Bölüm 7) ──────────────────
    private int totalWarningsGiven = 0;
    private int ignoredWarnings = 0;
    private string playerStyle = "Yeni Başkan"; // Industrialist, Ecologist, Balanced

    // ─── UI Durumu ──────────────────────────────────────────────
    private bool isChatOpen = false;
    private string inputText = "";
    private Vector2 scrollPos;
    private Rect chatWindowRect;

    // ─── Stiller ────────────────────────────────────────────────
    private GUIStyle chatBoxStyle;
    private GUIStyle playerMsgStyle;
    private GUIStyle botMsgStyle;
    private GUIStyle inputStyle;
    private GUIStyle headerStyle;
    private bool stylesReady = false;

    // Events
    public event Action<string> OnBotResponseReady;

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
        // Açılış mesajı
        AddBotMessage("🤖 Şehir Başdanışmanınız hazır!\n" +
                      "Bana para, kirlilik, enerji, bina veya durum hakkında sorabilirsin.\n" +
                      "T tuşuyla sohbeti aç/kapat.");
    }

    private void Update()
    {
        // T tuşu ile toggle (Input alanında değilken)
        if (Input.GetKeyDown(KeyCode.T) && !isChatOpen)
        {
            isChatOpen = true;
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && isChatOpen)
        {
            isChatOpen = false;
        }

        // Proaktif uyarılar (her 30 tick'te bir)
        if (GameManager.Instance != null && GameManager.Instance.CurrentDay % 30 == 0
            && GameManager.Instance.CurrentDay > 0)
        {
            CheckProactiveWarnings();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MESAJ İŞLEME
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Oyuncunun mesajını alır ve yanıt üretir.</summary>
    public void ReceivePlayerMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        // Oyuncu mesajını kaydet
        AddPlayerMessage(message);

        // 1. NLP analizi
        NLPProcessor.AnalyzeInput(message, out ChatIntent intent, out ChatEntity entity);

        // 2. Şehir verisi
        CitySnapshot context = GameContextReader.ReadCityData();

        // 3. Yanıt üret
        string response = ResponseGenerator.GenerateResponse(intent, entity, context);

        // 4. Hafıza güncelle
        UpdateMemory(intent, context);

        // 5. Yanıtı göster
        AddBotMessage(response);
        OnBotResponseReady?.Invoke(response);

        #if UNITY_EDITOR
        Debug.Log($"[Chatbot] Intent: {intent} | Entity: {entity} | Tone: {ResponseGenerator.EvaluateTone(context)}");
        #endif
    }

    // ─── Proaktif Uyarılar ──────────────────────────────────────
    private void CheckProactiveWarnings()
    {
        CitySnapshot ctx = GameContextReader.ReadCityData();
        CrisisLevel crisis = ctx.GetCrisisLevel();

        if (crisis == CrisisLevel.Critical && !isChatOpen)
        {
            string warning = "";
            if (ctx.isBlackout) warning = "⚡ Karartma devam ediyor! Acil santral kur.";
            else if (ctx.isWaterShortage) warning = "💧 Su kesintisi! Pompa kur.";
            else if (ctx.satisfaction < 30) warning = "🔥 İsyan tehlikesi! Memnuniyeti artır.";
            else if (ctx.money < -5000) warning = "💸 İflas yaklaşıyor! Harcamaları kıs.";

            if (!string.IsNullOrEmpty(warning))
            {
                AddBotMessage($"🚨 [OTOMATİK UYARI] {warning}\n(T tuşuyla sohbeti aç)");
                totalWarningsGiven++;
            }
        }
    }

    // ─── Hafıza Güncelleme ──────────────────────────────────────
    private void UpdateMemory(ChatIntent intent, CitySnapshot ctx)
    {
        // Oyuncu profili güncelle
        if (ctx.carbonEmission < 10f && ctx.airPollution < 1f)
            playerStyle = "Ekolog";
        else if (ctx.netIncome > 500f)
            playerStyle = "Sanayici";
        else
            playerStyle = "Dengeli";
    }

    // ═══════════════════════════════════════════════════════════════
    // SOHBET GEÇMİŞİ
    // ═══════════════════════════════════════════════════════════════

    private void AddPlayerMessage(string text)
    {
        chatHistory.Add(new ChatMessage
        {
            sender = "Oyuncu",
            text = text,
            timestamp = Time.time
        });
        TrimHistory();
    }

    private void AddBotMessage(string text)
    {
        chatHistory.Add(new ChatMessage
        {
            sender = "Danışman",
            text = text,
            timestamp = Time.time
        });
        TrimHistory();
        scrollPos = new Vector2(0, 99999f); // Otomatik scroll
    }

    private void TrimHistory()
    {
        while (chatHistory.Count > MAX_HISTORY)
            chatHistory.RemoveAt(0);
    }

    // ═══════════════════════════════════════════════════════════════
    // IMGUI SOHBET PENCERESİ
    // ═══════════════════════════════════════════════════════════════

    private void InitStyles()
    {
        chatBoxStyle = new GUIStyle(GUI.skin.box);
        chatBoxStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.92f));
        chatBoxStyle.padding = new RectOffset(8, 8, 8, 8);

        playerMsgStyle = new GUIStyle(GUI.skin.label);
        playerMsgStyle.normal.textColor = new Color(0.5f, 0.85f, 1f);
        playerMsgStyle.fontSize = 13;
        playerMsgStyle.wordWrap = true;

        botMsgStyle = new GUIStyle(GUI.skin.label);
        botMsgStyle.normal.textColor = new Color(0.8f, 0.9f, 0.7f);
        botMsgStyle.fontSize = 13;
        botMsgStyle.wordWrap = true;
        botMsgStyle.richText = true;

        inputStyle = new GUIStyle(GUI.skin.textField);
        inputStyle.fontSize = 14;
        inputStyle.fixedHeight = 28;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!isChatOpen) return;
        if (!stylesReady) InitStyles();

        float chatW = 420f;
        float chatH = 450f;
        float chatX = Screen.width - chatW - 15f;
        float chatY = 15f;

        chatWindowRect = new Rect(chatX, chatY, chatW, chatH);

        GUILayout.BeginArea(chatWindowRect, chatBoxStyle);

        // Başlık
        GUILayout.BeginHorizontal();
        GUILayout.Label($"🤖 Şehir Danışmanı ({playerStyle})", headerStyle);
        if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(20)))
        {
            isChatOpen = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Sohbet geçmişi (scrollable)
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(chatH - 90));

        foreach (var msg in chatHistory)
        {
            if (msg.sender == "Oyuncu")
            {
                GUILayout.Label($"💬 Sen: {msg.text}", playerMsgStyle);
            }
            else
            {
                GUILayout.Label($"{msg.text}", botMsgStyle);
            }
            GUILayout.Space(3);
        }

        GUILayout.EndScrollView();

        // Metin girişi
        GUILayout.BeginHorizontal();

        GUI.SetNextControlName("ChatInput");
        inputText = GUILayout.TextField(inputText, inputStyle);

        if (GUILayout.Button("Gönder", GUILayout.Width(65), GUILayout.Height(28)))
        {
            SendInput();
        }

        GUILayout.EndHorizontal();

        // Enter tuşu
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && GUI.GetNameOfFocusedControl() == "ChatInput")
        {
            SendInput();
            Event.current.Use();
        }

        GUILayout.EndArea();

        // Chat penceresi de UI block'a ekle
        Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (chatWindowRect.Contains(mousePos))
        {
            InputManager.IsPointerBlockedByUI = true;
        }
    }

    private void SendInput()
    {
        if (!string.IsNullOrWhiteSpace(inputText))
        {
            ReceivePlayerMessage(inputText.Trim());
            inputText = "";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

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
