using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Mouse girişlerini grid koordinatlarına çeviren input yöneticisi.
/// Grid üzerinde hover (üzerine gelme) ve tıklama olaylarını yönetir.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    // ─── Events ─────────────────────────────────────────────────
    /// <summary>Grid hücresine sol tıklandığında tetiklenir.</summary>
    public event Action<Vector2Int> OnCellClicked;

    /// <summary>Mouse farklı bir grid hücresine geçtiğinde tetiklenir.</summary>
    public event Action<Vector2Int> OnCellHovered;

    /// <summary>Sağ tık ile hücre seçildiğinde tetiklenir (yıkım vb. için).</summary>
    public event Action<Vector2Int> OnCellRightClicked;

    // ─── Public Erişim ──────────────────────────────────────────
    /// <summary>Mouse'un şu an üzerinde olduğu grid hücresi (-1,-1 = grid dışı).</summary>
    public Vector2Int HoveredCell { get; private set; } = new Vector2Int(-1, -1);

    /// <summary>Mouse grid üzerinde mi?</summary>
    public bool IsPointerOverGrid { get; private set; }

    /// <summary>
    /// Mouse şu an bir UI panelinin (OnGUI/IMGUI) üzerinde mi?
    /// DebugUI gibi OnGUI tabanlı paneller bu flag'i set eder.
    /// </summary>
    public static bool IsPointerBlockedByUI { get; set; }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Hover Görseli")]
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.35f);

    // ─── Private ────────────────────────────────────────────────
    private Camera mainCam;
    private GameObject hoverIndicator;
    private Renderer hoverRenderer;
    private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

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
        mainCam = Camera.main;
        CreateHoverIndicator();
    }

    private void Update()
    {
        UpdateHover();
        HandleClicks();
    }

    // ═══════════════════════════════════════════════════════════════
    // HOVER İNDİKATÖRÜ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Grid üzerinde mouse'un bulunduğu hücreyi gösteren yarı-saydam küp.</summary>
    private void CreateHoverIndicator()
    {
        hoverIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hoverIndicator.name = "HoverIndicator";

        float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
        hoverIndicator.transform.localScale = new Vector3(cellSize, 0.05f, cellSize);

        // Raycast'e karışmasın
        Collider col = hoverIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Yarı-saydam materyal
        hoverRenderer = hoverIndicator.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.color = hoverColor;
        hoverRenderer.material = mat;

        hoverIndicator.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    // HOVER GÜNCELLEME
    // ═══════════════════════════════════════════════════════════════

    private void UpdateHover()
    {
        if (mainCam == null || GridSystem.Instance == null) return;

        // UI üzerindeyse grid hover'ı kapat (hem uGUI hem OnGUI panelleri)
        bool overUGUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUGUI || IsPointerBlockedByUI)
        {
            HideHover();
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector2Int gridPos = GridSystem.Instance.WorldToGridPosition(hitPoint);

            if (GridSystem.Instance.IsValidCoordinate(gridPos.x, gridPos.y))
            {
                IsPointerOverGrid = true;

                if (gridPos != HoveredCell)
                {
                    HoveredCell = gridPos;
                    OnCellHovered?.Invoke(gridPos);

                    // Hover göstergesini güncelle
                    Vector3 worldPos = GridSystem.Instance.GridToWorldPosition(gridPos.x, gridPos.y);
                    hoverIndicator.transform.position = new Vector3(worldPos.x, 0.03f, worldPos.z);
                    hoverIndicator.SetActive(true);
                }
            }
            else
            {
                HideHover();
            }
        }
        else
        {
            HideHover();
        }
    }

    private void HideHover()
    {
        IsPointerOverGrid = false;
        HoveredCell = new Vector2Int(-1, -1);
        if (hoverIndicator != null) hoverIndicator.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    // TIKLAMA
    // ═══════════════════════════════════════════════════════════════

    private void HandleClicks()
    {
        if (!IsPointerOverGrid) return;

        // UI üzerindeyse tıklamayı yoksay (hem uGUI hem OnGUI panelleri)
        if (IsPointerBlockedByUI) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Sol tık
        if (Input.GetMouseButtonDown(0))
        {
            OnCellClicked?.Invoke(HoveredCell);

            #if UNITY_EDITOR
            Debug.Log($"[InputManager] Hücre seçildi: ({HoveredCell.x}, {HoveredCell.y})");
            #endif
        }

        // Sağ tık
        if (Input.GetMouseButtonDown(1))
        {
            OnCellRightClicked?.Invoke(HoveredCell);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HOVER RENK DEĞİŞTİRME (İnşaat sistemi kullanacak)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hover indikatörünün rengini değiştirir (yeşil = yerleştirilebilir, kırmızı = dolu).</summary>
    public void SetHoverColor(Color color)
    {
        if (hoverRenderer != null)
        {
            hoverRenderer.material.color = color;
        }
    }

    /// <summary>Hover indikatörünü varsayılan rengine döndürür.</summary>
    public void ResetHoverColor()
    {
        SetHoverColor(hoverColor);
    }

    /// <summary>Hover indikatörünün boyutunu değiştirir (multi-tile bina önizlemesi için).</summary>
    public void SetHoverSize(int gridWidth, int gridHeight)
    {
        if (hoverIndicator == null || GridSystem.Instance == null) return;
        float cellSize = GridSystem.Instance.CellSize;
        hoverIndicator.transform.localScale = new Vector3(
            gridWidth * cellSize,
            0.05f,
            gridHeight * cellSize
        );
    }

    /// <summary>Hover indikatörü boyutunu 1x1'e sıfırlar.</summary>
    public void ResetHoverSize()
    {
        SetHoverSize(1, 1);
    }
}
