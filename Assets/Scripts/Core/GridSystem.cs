using UnityEngine;

/// <summary>
/// 100x100 Grid sistemi. Haritadaki her hücrenin durumunu takip eder,
/// koordinat dönüşümlerini yapar ve blockout zemin görselini oluşturur.
/// </summary>
public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    // ─── Hücre Tipleri ──────────────────────────────────────────
    public enum CellType { Empty, Road, Building, Park }

    // ─── Inspector Ayarları ─────────────────────────────────────
    [Header("Grid Ayarları")]
    [SerializeField] private int gridWidth = 100;
    [SerializeField] private int gridHeight = 100;
    [SerializeField] private float cellSize = 1f;

    [Header("Zemin Görselleştirme")]
    [SerializeField] private Color groundColor = new Color(0.35f, 0.55f, 0.30f);

    [Header("Grid Çizgileri (Runtime)")]
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private Color gridLineColor = new Color(0.2f, 0.2f, 0.2f, 0.25f);

    // ─── Public Erişim ──────────────────────────────────────────
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float CellSize => cellSize;
    public float MapWorldWidth => gridWidth * cellSize;
    public float MapWorldHeight => gridHeight * cellSize;

    // ─── Veri Yapıları ──────────────────────────────────────────
    private CellType[,] grid;
    private GameObject[,] buildingReferences;

    // ─── Grid çizgi materyali ───────────────────────────────────
    private Material gridLineMaterial;

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

        InitializeGrid();
        CreateGridLineMaterial();
    }

    // ═══════════════════════════════════════════════════════════════
    // BAŞLATMA
    // ═══════════════════════════════════════════════════════════════

    private void InitializeGrid()
    {
        grid = new CellType[gridWidth, gridHeight];
        buildingReferences = new GameObject[gridWidth, gridHeight];

        CreateGroundPlane();
    }

    /// <summary>Zemin düzlemini oluşturur (blockout yeşil düzlem).</summary>
    private void CreateGroundPlane()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.parent = transform;

        // Unity Plane varsayılan olarak 10x10 birimdir
        float scaleX = MapWorldWidth / 10f;
        float scaleZ = MapWorldHeight / 10f;
        ground.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
        ground.transform.position = new Vector3(MapWorldWidth / 2f, 0f, MapWorldHeight / 2f);

        Renderer renderer = ground.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        renderer.material.color = groundColor;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Grid çizgileri için materyal (GL rendering).</summary>
    private void CreateGridLineMaterial()
    {
        // Sprites/Default shader'ı transparan renkleri destekler
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        gridLineMaterial = new Material(shader);
        gridLineMaterial.hideFlags = HideFlags.HideAndDontSave;
        gridLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        gridLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        gridLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        gridLineMaterial.SetInt("_ZWrite", 0);
    }

    // ═══════════════════════════════════════════════════════════════
    // KOORDİNAT DÖNÜŞÜMÜ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Grid koordinatından dünya pozisyonuna (hücre merkezi).</summary>
    public Vector3 GridToWorldPosition(int x, int z)
    {
        return new Vector3(
            x * cellSize + cellSize / 2f,
            0f,
            z * cellSize + cellSize / 2f
        );
    }

    /// <summary>Multi-tile bina için merkez dünya pozisyonu.</summary>
    public Vector3 GridToWorldPosition(int x, int z, int width, int height)
    {
        return new Vector3(
            x * cellSize + (width * cellSize) / 2f,
            0f,
            z * cellSize + (height * cellSize) / 2f
        );
    }

    /// <summary>Dünya pozisyonundan grid koordinatına.</summary>
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
    }

    // ═══════════════════════════════════════════════════════════════
    // HÜCRE SORGULAMA
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Koordinat harita sınırları içinde mi?</summary>
    public bool IsValidCoordinate(int x, int z)
    {
        return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
    }

    /// <summary>Hücre boş mu?</summary>
    public bool IsCellEmpty(int x, int z)
    {
        if (!IsValidCoordinate(x, z)) return false;
        return grid[x, z] == CellType.Empty;
    }

    /// <summary>Multi-tile alan tamamen boş mu? (Bina yerleşimi kontrolü)</summary>
    public bool IsAreaEmpty(int startX, int startZ, int width, int height)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + height; z++)
            {
                if (!IsCellEmpty(x, z)) return false;
            }
        }
        return true;
    }

    /// <summary>Hücre tipini al.</summary>
    public CellType GetCellType(int x, int z)
    {
        if (!IsValidCoordinate(x, z)) return CellType.Empty;
        return grid[x, z];
    }

    /// <summary>Hücredeki bina referansını al.</summary>
    public GameObject GetBuildingAt(int x, int z)
    {
        if (!IsValidCoordinate(x, z)) return null;
        return buildingReferences[x, z];
    }

    // ═══════════════════════════════════════════════════════════════
    // HÜCRE GÜNCELLEME
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Tek hücre durumunu güncelle.</summary>
    public void SetCell(int x, int z, CellType type, GameObject reference = null)
    {
        if (!IsValidCoordinate(x, z)) return;
        grid[x, z] = type;
        buildingReferences[x, z] = reference;
    }

    /// <summary>Multi-tile alanı güncelle (bina yerleşimi için).</summary>
    public void SetArea(int startX, int startZ, int width, int height, CellType type, GameObject reference = null)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + height; z++)
            {
                SetCell(x, z, type, reference);
            }
        }
    }

    /// <summary>Alanı temizle (bina yıkımı için).</summary>
    public void ClearArea(int startX, int startZ, int width, int height)
    {
        SetArea(startX, startZ, width, height, CellType.Empty, null);
    }

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME GRİD ÇİZGİLERİ (GL Rendering)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// GL ile grid çizgilerini çizer. OnRenderObject kameranın render döngüsünde çağrılır.
    /// Bu sayede Game View'da da grid çizgileri görünür.
    /// Not: 100x100 grid için 202 çizgi — performans açısından sorun yok.
    /// </summary>
    private void OnRenderObject()
    {
        if (!showGridLines || gridLineMaterial == null) return;

        gridLineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);

        float y = 0.02f; // Zeminin hafif üzerinde

        // Dikey çizgiler (X ekseni boyunca)
        for (int x = 0; x <= gridWidth; x++)
        {
            GL.Vertex3(x * cellSize, y, 0);
            GL.Vertex3(x * cellSize, y, gridHeight * cellSize);
        }

        // Yatay çizgiler (Z ekseni boyunca)
        for (int z = 0; z <= gridHeight; z++)
        {
            GL.Vertex3(0, y, z * cellSize);
            GL.Vertex3(gridWidth * cellSize, y, z * cellSize);
        }

        GL.End();
        GL.PopMatrix();
    }

    // ═══════════════════════════════════════════════════════════════
    // GIZMOS (Editör Scene View)
    // ═══════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Editörde seçiliyken harita sınırlarını göster
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(gridWidth * cellSize / 2f, 0f, gridHeight * cellSize / 2f);
        Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
        Gizmos.DrawWireCube(center, size);
    }
}
