using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Bina yerleştirme ve yıkım sistemi.
/// Mouse ile grid üzerinde bina önizlemesi (ghost) gösterir ve tıklama ile yerleştirir.
/// GDD Bölüm 5: Bina Sistemi
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer Instance { get; private set; }

    // ─── Yerleştirme Modları ────────────────────────────────────
    public enum PlacementMode { None, Building, Demolish }

    // ─── Inspector ──────────────────────────────────────────────
    [Header("Mevcut Bina Tipleri")]
    [Tooltip("Bu listeye Data/Buildings klasöründeki ScriptableObject'leri sürükle")]
    [SerializeField] private BuildingData[] availableBuildings;

    // ─── Public Erişim ──────────────────────────────────────────
    public PlacementMode CurrentMode { get; private set; } = PlacementMode.None;
    public BuildingData SelectedBuilding { get; private set; }
    public BuildingData[] AvailableBuildings => availableBuildings;

    // ─── Events ─────────────────────────────────────────────────
    public event Action<BuildingInstance> OnBuildingPlaced;
    public event Action<BuildingInstance> OnBuildingDemolished;
    public event Action<PlacementMode> OnModeChanged;

    // ─── Yerleştirilmiş Binalar ─────────────────────────────────
    private List<BuildingInstance> placedBuildings = new List<BuildingInstance>();
    public IReadOnlyList<BuildingInstance> PlacedBuildings => placedBuildings;

    // ─── Ghost Önizleme ─────────────────────────────────────────
    private GameObject ghostPreview;
    private Renderer ghostRenderer;
    private bool canPlaceAtCurrent;

    // Renkler
    private readonly Color validPlacementColor = new Color(0.2f, 0.9f, 0.2f, 0.4f);
    private readonly Color invalidPlacementColor = new Color(0.9f, 0.2f, 0.2f, 0.4f);
    private readonly Color demolishHoverColor = new Color(1f, 0.15f, 0.15f, 0.5f);

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
        // Input event'lerine abone ol
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnCellClicked += HandleCellClicked;
            InputManager.Instance.OnCellHovered += HandleCellHovered;
            InputManager.Instance.OnCellRightClicked += HandleRightClick;
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnCellClicked -= HandleCellClicked;
            InputManager.Instance.OnCellHovered -= HandleCellHovered;
            InputManager.Instance.OnCellRightClicked -= HandleRightClick;
        }
    }

    private void Update()
    {
        // Escape ile mod iptal
        if (Input.GetKeyDown(KeyCode.Escape) && CurrentMode != PlacementMode.None)
        {
            CancelMode();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MOD SEÇİMİ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Bina seçerek inşaat moduna geç.</summary>
    public void SelectBuilding(BuildingData building)
    {
        if (building == null) return;

        SelectedBuilding = building;
        CurrentMode = PlacementMode.Building;
        OnModeChanged?.Invoke(CurrentMode);

        CreateGhostPreview();

        #if UNITY_EDITOR
        Debug.Log($"[BuildingPlacer] İnşaat modu: {building.buildingName}");
        #endif
    }

    /// <summary>İndex ile bina seç (UI butonları için).</summary>
    public void SelectBuilding(int index)
    {
        if (availableBuildings == null || index < 0 || index >= availableBuildings.Length) return;
        SelectBuilding(availableBuildings[index]);
    }

    /// <summary>Yıkım moduna geç.</summary>
    public void EnterDemolishMode()
    {
        SelectedBuilding = null;
        CurrentMode = PlacementMode.Demolish;
        OnModeChanged?.Invoke(CurrentMode);

        DestroyGhostPreview();

        if (InputManager.Instance != null)
            InputManager.Instance.SetHoverColor(demolishHoverColor);

        #if UNITY_EDITOR
        Debug.Log("[BuildingPlacer] Yıkım modu aktif.");
        #endif
    }

    /// <summary>Herhangi bir modu iptal et.</summary>
    public void CancelMode()
    {
        SelectedBuilding = null;
        CurrentMode = PlacementMode.None;
        OnModeChanged?.Invoke(CurrentMode);

        DestroyGhostPreview();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.ResetHoverColor();
            InputManager.Instance.ResetHoverSize();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GHOST ÖNİZLEME (Yarı-saydam bina hayaleti)
    // ═══════════════════════════════════════════════════════════════

    private void CreateGhostPreview()
    {
        DestroyGhostPreview();
        if (SelectedBuilding == null) return;

        ghostPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghostPreview.name = "GhostPreview";
        ghostPreview.layer = 2; // Ignore Raycast

        // Collider kaldır
        Collider col = ghostPreview.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Boyut ayarla
        float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
        ghostPreview.transform.localScale = new Vector3(
            SelectedBuilding.gridWidth * cellSize,
            SelectedBuilding.blockoutHeight,
            SelectedBuilding.gridHeight * cellSize
        );

        // Yarı-saydam materyal
        ghostRenderer = ghostPreview.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent mode
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = validPlacementColor;
        ghostRenderer.material = mat;

        ghostPreview.SetActive(false);

        // Varsayılan hover'ı gizle (ghost kendi göstergesini yapıyor)
        if (InputManager.Instance != null)
            InputManager.Instance.SetHoverSize(0, 0);
    }

    private void DestroyGhostPreview()
    {
        if (ghostPreview != null)
        {
            Destroy(ghostPreview);
            ghostPreview = null;
            ghostRenderer = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // INPUT HANDLER'LAR
    // ═══════════════════════════════════════════════════════════════

    private void HandleCellHovered(Vector2Int cell)
    {
        if (CurrentMode != PlacementMode.Building || SelectedBuilding == null) return;
        if (ghostPreview == null || GridSystem.Instance == null) return;

        // Ghost pozisyonunu güncelle (multi-tile merkezleme)
        Vector3 worldPos = GridSystem.Instance.GridToWorldPosition(
            cell.x, cell.y,
            SelectedBuilding.gridWidth, SelectedBuilding.gridHeight
        );

        ghostPreview.transform.position = new Vector3(
            worldPos.x,
            SelectedBuilding.blockoutHeight / 2f,
            worldPos.z
        );
        ghostPreview.SetActive(true);

        // Yerleştirilebilirlik kontrolü
        canPlaceAtCurrent = CanPlaceAt(cell.x, cell.y, SelectedBuilding);

        if (ghostRenderer != null)
        {
            ghostRenderer.material.color = canPlaceAtCurrent ? validPlacementColor : invalidPlacementColor;
        }
    }

    private void HandleCellClicked(Vector2Int cell)
    {
        switch (CurrentMode)
        {
            case PlacementMode.Building:
                TryPlaceBuilding(cell);
                break;
            case PlacementMode.Demolish:
                TryDemolish(cell);
                break;
        }
    }

    private void HandleRightClick(Vector2Int cell)
    {
        // Sağ tık = modu iptal et
        if (CurrentMode != PlacementMode.None)
        {
            CancelMode();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YERLEŞTİRME
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Belirtilen konuma bina yerleştirilebilir mi?</summary>
    private bool CanPlaceAt(int x, int z, BuildingData data)
    {
        if (data == null || GridSystem.Instance == null) return false;

        // Tüm hücreler geçerli ve boş mu?
        for (int dx = 0; dx < data.gridWidth; dx++)
        {
            for (int dz = 0; dz < data.gridHeight; dz++)
            {
                if (!GridSystem.Instance.IsCellEmpty(x + dx, z + dz))
                    return false;
            }
        }

        // Para yeterli mi?
        if (ResourceSystem.Instance != null && !ResourceSystem.Instance.CanAfford(data.buildCost))
            return false;

        return true;
    }

    /// <summary>Bina yerleştirmeyi dene.</summary>
    private void TryPlaceBuilding(Vector2Int cell)
    {
        if (SelectedBuilding == null || !canPlaceAtCurrent) return;

        // Parayı harca
        if (ResourceSystem.Instance != null)
        {
            if (!ResourceSystem.Instance.SpendMoney(SelectedBuilding.buildCost))
                return;
        }

        // Binayı yerleştir
        PlaceBuilding(cell.x, cell.y, SelectedBuilding);
    }

    /// <summary>Binayı sahnede oluşturur, grid'e kaydeder.</summary>
    public BuildingInstance PlaceBuilding(int gridX, int gridZ, BuildingData data)
    {
        if (GridSystem.Instance == null) return null;
        float cellSize = GridSystem.Instance.CellSize;

        // ─── Blockout küp oluştur ───
        GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        buildingObj.name = $"{data.buildingName} ({gridX},{gridZ})";

        // Boyut
        buildingObj.transform.localScale = new Vector3(
            data.gridWidth * cellSize,
            data.blockoutHeight,
            data.gridHeight * cellSize
        );

        // Pozisyon (merkez, yerden yarı yükseklik kadar yukarıda)
        Vector3 worldPos = GridSystem.Instance.GridToWorldPosition(gridX, gridZ, data.gridWidth, data.gridHeight);
        buildingObj.transform.position = new Vector3(
            worldPos.x,
            data.blockoutHeight / 2f,
            worldPos.z
        );

        // Renk
        Renderer renderer = buildingObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = data.blockoutColor;
        renderer.material = mat;

        // ─── BuildingInstance bileşeni ───
        BuildingInstance instance = buildingObj.AddComponent<BuildingInstance>();
        instance.Initialize(data, gridX, gridZ);

        // ─── Grid'e kaydet ───
        GridSystem.CellType cellType = data.category == BuildingData.BuildingCategory.Environmental
            ? GridSystem.CellType.Park
            : GridSystem.CellType.Building;

        GridSystem.Instance.SetArea(gridX, gridZ, data.gridWidth, data.gridHeight, cellType, buildingObj);

        // ─── Listeye ekle ───
        placedBuildings.Add(instance);
        OnBuildingPlaced?.Invoke(instance);

        #if UNITY_EDITOR
        Debug.Log($"[BuildingPlacer] ✅ {data.buildingName} yerleştirildi: ({gridX}, {gridZ}) | " +
                  $"Maliyet: {data.buildCost} Eko-Para");
        #endif

        return instance;
    }

    // ═══════════════════════════════════════════════════════════════
    // YIKIM
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Tıklanan hücredeki binayı yıkmayı dene.</summary>
    private void TryDemolish(Vector2Int cell)
    {
        if (GridSystem.Instance == null) return;

        GameObject buildingObj = GridSystem.Instance.GetBuildingAt(cell.x, cell.y);
        if (buildingObj == null) return;

        BuildingInstance instance = buildingObj.GetComponent<BuildingInstance>();
        if (instance == null) return;

        DemolishBuilding(instance);
    }

    /// <summary>Binayı yıkar, grid'den temizler.</summary>
    public void DemolishBuilding(BuildingInstance instance)
    {
        if (instance == null || instance.Data == null) return;

        // Grid'den temizle
        GridSystem.Instance.ClearArea(
            instance.GridX, instance.GridZ,
            instance.Data.gridWidth, instance.Data.gridHeight
        );

        // Listeden çıkar
        placedBuildings.Remove(instance);
        OnBuildingDemolished?.Invoke(instance);

        #if UNITY_EDITOR
        Debug.Log($"[BuildingPlacer] 🔨 {instance.Data.buildingName} yıkıldı: ({instance.GridX}, {instance.GridZ})");
        #endif

        // Objeyi yok et
        Destroy(instance.gameObject);
    }
}
