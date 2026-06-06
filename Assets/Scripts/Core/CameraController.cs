using UnityEngine;

/// <summary>
/// İzometrik ortografik kamera kontrolleri.
/// Sabit açı (rotasyon yok), WASD/kenar kaydırma, scroll zoom, harita sınırına clamp.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    // ─── İzometrik Ayarlar ──────────────────────────────────────
    [Header("İzometrik Açı")]
    [Tooltip("X ekseni etrafındaki eğim açısı (30° klasik izometrik)")]
    [SerializeField] private float isometricAngleX = 30f;
    [Tooltip("Y ekseni etrafındaki dönüş açısı (45° klasik izometrik)")]
    [SerializeField] private float isometricAngleY = 45f;

    // ─── Hareket Ayarları ───────────────────────────────────────
    [Header("Hareket")]
    [SerializeField] private float panSpeed = 25f;
    [SerializeField] private float edgeScrollSpeed = 20f;
    [Tooltip("Ekranın kenarından kaç piksel içeride kaydırma başlar")]
    [SerializeField] private int edgeScrollThreshold = 15;
    [SerializeField] private bool enableEdgeScroll = true;
    [Tooltip("Orta fare tuşuyla sürükleme hassasiyeti")]
    [SerializeField] private float dragSensitivity = 0.04f;

    // ─── Zoom Ayarları ──────────────────────────────────────────
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 60f;
    [SerializeField] private float defaultZoom = 30f;
    [Tooltip("Zoom animasyonu yumuşatma hızı")]
    [SerializeField] private float zoomSmoothSpeed = 10f;

    // ─── Sınır Ayarları ─────────────────────────────────────────
    [Header("Harita Sınırları")]
    [Tooltip("Kameranın harita dışına taşma payı (birim)")]
    [SerializeField] private float boundaryPadding = 10f;

    // ─── Private ────────────────────────────────────────────────
    private Camera cam;
    private Vector3 lastMousePosition;
    private bool isDragging;
    private float targetZoom;

    // Harita sınırları (GridSystem'den okunacak)
    private float mapMinX = 0f;
    private float mapMaxX = 100f;
    private float mapMinZ = 0f;
    private float mapMaxZ = 100f;

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

        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = defaultZoom;
        targetZoom = defaultZoom;

        // Sabit izometrik açıyı uygula
        transform.rotation = Quaternion.Euler(isometricAngleX, isometricAngleY, 0f);
    }

    private void Start()
    {
        // GridSystem'den harita sınırlarını oku
        if (GridSystem.Instance != null)
        {
            mapMaxX = GridSystem.Instance.MapWorldWidth;
            mapMaxZ = GridSystem.Instance.MapWorldHeight;
        }

        // Kamerayı harita merkezine taşı
        Vector3 center = new Vector3(mapMaxX / 2f, 0f, mapMaxZ / 2f);
        PositionCameraLookingAt(center);
    }

    private void LateUpdate()
    {
        HandleKeyboardPan();
        HandleMiddleMouseDrag();
        HandleEdgeScroll();
        HandleZoom();
        ClampToMapBounds();
    }

    // ═══════════════════════════════════════════════════════════════
    // KEYBOARD KAYDIRMA (WASD / Ok Tuşları)
    // ═══════════════════════════════════════════════════════════════

    private void HandleKeyboardPan()
    {
        Vector3 moveDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            moveDir += GetForwardOnPlane();
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            moveDir -= GetForwardOnPlane();
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            moveDir += GetRightOnPlane();
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            moveDir -= GetRightOnPlane();

        if (moveDir.sqrMagnitude > 0.001f)
        {
            // Zoom seviyesine göre hız ölçekleme (uzaklaştıkça daha hızlı)
            float speedScale = cam.orthographicSize / defaultZoom;
            transform.position += moveDir.normalized * (panSpeed * speedScale * Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ORTA FARE SÜRÜKLEME
    // ═══════════════════════════════════════════════════════════════

    private void HandleMiddleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;

            // Kameranın sağ/ileri yönleri XZ düzleminde, ters yöne çek
            float speedScale = cam.orthographicSize * dragSensitivity;
            Vector3 move = (-GetRightOnPlane() * delta.x - GetForwardOnPlane() * delta.y) * speedScale;
            transform.position += move;

            lastMousePosition = Input.mousePosition;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // KENAR KAYDIRMA
    // ═══════════════════════════════════════════════════════════════

    private void HandleEdgeScroll()
    {
        if (!enableEdgeScroll) return;
        if (!Application.isFocused) return;

        Vector3 mousePos = Input.mousePosition;
        Vector3 moveDir = Vector3.zero;

        if (mousePos.x <= edgeScrollThreshold && mousePos.x >= 0)
            moveDir -= GetRightOnPlane();
        else if (mousePos.x >= Screen.width - edgeScrollThreshold && mousePos.x <= Screen.width)
            moveDir += GetRightOnPlane();

        if (mousePos.y <= edgeScrollThreshold && mousePos.y >= 0)
            moveDir -= GetForwardOnPlane();
        else if (mousePos.y >= Screen.height - edgeScrollThreshold && mousePos.y <= Screen.height)
            moveDir += GetForwardOnPlane();

        if (moveDir.sqrMagnitude > 0.001f)
        {
            float speedScale = cam.orthographicSize / defaultZoom;
            transform.position += moveDir.normalized * (edgeScrollSpeed * speedScale * Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ZOOM (Scroll Wheel)
    // ═══════════════════════════════════════════════════════════════

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            targetZoom -= scrollDelta * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // Yumuşak zoom geçişi
        if (!Mathf.Approximately(cam.orthographicSize, targetZoom))
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomSmoothSpeed);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HARİTA SINIRI CLAMP
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Kameranın baktığı noktayı harita sınırları içinde tutar.
    /// İzometrik kamera offset'li olduğundan, ground plane'e ray atarak
    /// gerçek bakış noktasını hesaplar ve clamp eder.
    /// </summary>
    private void ClampToMapBounds()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 lookPoint = ray.GetPoint(distance);

            float clampedX = Mathf.Clamp(lookPoint.x, mapMinX - boundaryPadding, mapMaxX + boundaryPadding);
            float clampedZ = Mathf.Clamp(lookPoint.z, mapMinZ - boundaryPadding, mapMaxZ + boundaryPadding);

            Vector3 correction = new Vector3(clampedX - lookPoint.x, 0f, clampedZ - lookPoint.z);

            if (correction.sqrMagnitude > 0.001f)
            {
                transform.position += correction;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCI METODLAR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Kamerayı belirtilen zemin noktasına bakar şekilde konumlandırır.</summary>
    public void PositionCameraLookingAt(Vector3 targetOnGround)
    {
        float cameraDistance = 80f;
        Vector3 cameraOffset = -transform.forward * cameraDistance;
        transform.position = targetOnGround + cameraOffset;
    }

    /// <summary>Kameranın XZ düzlemindeki "ileri" yönü (Y=0 düzleştirilmiş).</summary>
    private Vector3 GetForwardOnPlane()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    /// <summary>Kameranın XZ düzlemindeki "sağ" yönü (Y=0 düzleştirilmiş).</summary>
    private Vector3 GetRightOnPlane()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        return right.normalized;
    }

    /// <summary>Hedef zoom seviyesini ayarlar (dışarıdan erişim için).</summary>
    public void SetZoom(float zoom)
    {
        targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
    }
}
