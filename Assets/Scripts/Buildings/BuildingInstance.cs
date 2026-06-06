using UnityEngine;

/// <summary>
/// Sahneye yerleştirilmiş bir bina instance'ı.
/// BuildingData'dan statik verileri okur, runtime durumunu (seviye, aktiflik) tutar.
/// </summary>
public class BuildingInstance : MonoBehaviour
{
    // ─── Veriler ────────────────────────────────────────────────
    public BuildingData Data { get; private set; }
    public int GridX { get; private set; }
    public int GridZ { get; private set; }
    public int Level { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    // ─── Renderer Cache ─────────────────────────────────────────
    private Renderer buildingRenderer;
    private Color originalColor;

    // ═══════════════════════════════════════════════════════════════
    // BAŞLATMA
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Bina yerleştirildikten sonra çağrılır.</summary>
    public void Initialize(BuildingData data, int gridX, int gridZ)
    {
        Data = data;
        GridX = gridX;
        GridZ = gridZ;
        Level = 1;
        IsActive = true;

        buildingRenderer = GetComponent<Renderer>();
        if (buildingRenderer != null)
        {
            originalColor = data.blockoutColor;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // AKTİFLİK YÖNETİMİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Binayı aktif/pasif yapar.
    /// Enerji veya su yoksa binalar deaktif olur (GDD Bölüm 4.1).
    /// Deaktif binalar vergi üretmez, nüfus barındırmaz.
    /// </summary>
    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;

        // Görsel geri bildirim: deaktif binalar kararır
        if (buildingRenderer != null)
        {
            Color displayColor = active
                ? originalColor
                : Color.Lerp(originalColor, Color.black, 0.6f);
            buildingRenderer.material.color = displayColor;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SEVİYE SİSTEMİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Konut binalarını seviye atlatır (GDD Bölüm 5.1).
    /// Çevresindeki kirlilik azsa ve memnuniyet yüksekse otomatik upgrade olur.
    /// </summary>
    public bool TryUpgrade()
    {
        if (Data == null || !Data.canUpgrade) return false;
        if (Level >= Data.maxLevel) return false;

        Level++;
        UpdateVisualForLevel();

        #if UNITY_EDITOR
        Debug.Log($"[BuildingInstance] {Data.buildingName} seviye atladı: Lv.{Level}");
        #endif

        return true;
    }

    /// <summary>Seviyeye göre blockout yüksekliğini artırır.</summary>
    private void UpdateVisualForLevel()
    {
        if (Data == null) return;

        // Her seviye biraz daha yüksek
        float newHeight = Data.blockoutHeight * (1f + (Level - 1) * 0.5f);
        float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;

        transform.localScale = new Vector3(
            Data.gridWidth * cellSize,
            newHeight,
            Data.gridHeight * cellSize
        );

        transform.position = new Vector3(
            transform.position.x,
            newHeight / 2f,
            transform.position.z
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // KAYNAK HESAPLAMA (ResourceSystem tarafından çağrılır)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Bu binanın şu anki tick'teki vergi geliri. Deaktifse 0.</summary>
    public float GetCurrentTaxIncome()
    {
        if (!IsActive || Data == null) return 0f;
        // Nüfus çarpanı ileriki fazlarda eklenecek
        return Data.taxIncomePerTick;
    }

    /// <summary>Bu binanın şu anki tick'teki bakım maliyeti. Her zaman ödenir.</summary>
    public float GetCurrentMaintenanceCost()
    {
        if (Data == null) return 0f;
        return Data.maintenanceCostPerTick;
    }

    /// <summary>Bu binanın net enerji etkisi (+üretim / -tüketim).</summary>
    public float GetEnergyDelta()
    {
        if (Data == null) return 0f;
        // Deaktif binalar enerji tüketmez ama enerji santralleri yine üretir
        if (!IsActive && Data.energyDelta < 0f) return 0f;
        return Data.energyDelta;
    }

    /// <summary>Bu binanın net su etkisi (+üretim / -tüketim).</summary>
    public float GetWaterDelta()
    {
        if (Data == null) return 0f;
        if (!IsActive && Data.waterDelta < 0f) return 0f;
        return Data.waterDelta;
    }
}
