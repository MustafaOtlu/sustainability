using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Oyun kaydetme ve yükleme sistemi.
/// JSON tabanlı serileştirme ile tüm şehir durumunu dosyaya kaydeder.
/// GDD Bölüm 11: Save/Load (Basitleştirilmiş)
/// </summary>
public class SaveLoadSystem : MonoBehaviour
{
    public static SaveLoadSystem Instance { get; private set; }

    [Header("Kayıt Ayarları")]
    [SerializeField] private string saveFileName = "sustainability_save.json";

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    // ═══════════════════════════════════════════════════════════════
    // KAYIT VERİ YAPISI
    // ═══════════════════════════════════════════════════════════════

    [Serializable]
    public class SaveData
    {
        // Meta
        public string saveDate;
        public int version = 1;

        // GameManager
        public int currentDay;
        public float speedMultiplier;

        // Resources
        public float money;
        public int population;

        // Buildings
        public List<BuildingSaveData> buildings = new List<BuildingSaveData>();

        // Roads
        public List<Vector2IntData> roads = new List<Vector2IntData>();
    }

    [Serializable]
    public class BuildingSaveData
    {
        public string buildingName;
        public int gridX;
        public int gridZ;
        public int level;
        public bool isActive;
    }

    [Serializable]
    public class Vector2IntData
    {
        public int x;
        public int z;
    }

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

    private void Update()
    {
        // Kısayollar
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            LoadGame();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // KAYDETME
    // ═══════════════════════════════════════════════════════════════

    public bool SaveGame()
    {
        try
        {
            SaveData data = new SaveData();
            data.saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // GameManager
            if (GameManager.Instance != null)
            {
                data.currentDay = GameManager.Instance.CurrentDay;
                data.speedMultiplier = GameManager.Instance.SpeedMultiplier;
            }

            // Resources
            if (ResourceSystem.Instance != null)
            {
                data.money = ResourceSystem.Instance.Money;
                data.population = ResourceSystem.Instance.Population;
            }

            // Buildings
            if (BuildingPlacer.Instance != null)
            {
                foreach (var building in BuildingPlacer.Instance.PlacedBuildings)
                {
                    if (building == null || building.Data == null) continue;

                    data.buildings.Add(new BuildingSaveData
                    {
                        buildingName = building.Data.buildingName,
                        gridX = building.GridX,
                        gridZ = building.GridZ,
                        level = building.Level,
                        isActive = building.IsActive
                    });
                }
            }

            // Roads
            if (RoadSystem.Instance != null)
            {
                // Grid'deki tüm yol hücrelerini tara
                if (GridSystem.Instance != null)
                {
                    for (int x = 0; x < GridSystem.Instance.GridWidth; x++)
                    {
                        for (int z = 0; z < GridSystem.Instance.GridHeight; z++)
                        {
                            if (GridSystem.Instance.GetCellType(x, z) == GridSystem.CellType.Road)
                            {
                                data.roads.Add(new Vector2IntData { x = x, z = z });
                            }
                        }
                    }
                }
            }

            // JSON'a çevir ve kaydet
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);

            Debug.Log($"[SaveLoad] ✅ Oyun kaydedildi: {SaveFilePath}");
            Debug.Log($"[SaveLoad]    Gün: {data.currentDay} | Bina: {data.buildings.Count} | Yol: {data.roads.Count}");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoad] ❌ Kaydetme hatası: {e.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YÜKLEME
    // ═══════════════════════════════════════════════════════════════

    public bool LoadGame()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.LogWarning("[SaveLoad] ⚠ Kayıt dosyası bulunamadı!");
                return false;
            }

            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
            {
                Debug.LogError("[SaveLoad] ❌ Kayıt dosyası okunamadı!");
                return false;
            }

            Debug.Log($"[SaveLoad] 📂 Kayıt yükleniyor: {data.saveDate}");

            // Oyunu duraklat
            GameManager.Instance?.Pause();

            // Mevcut binaları temizle
            ClearCurrentState();

            // GameManager durumu
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetDay(data.currentDay);
                GameManager.Instance.SetSpeed(data.speedMultiplier);
            }

            // Resources
            if (ResourceSystem.Instance != null)
            {
                ResourceSystem.Instance.AddMoney(data.money - ResourceSystem.Instance.Money);
                ResourceSystem.Instance.SetPopulation(data.population);
            }

            // Binaları yeniden oluştur
            if (BuildingPlacer.Instance != null)
            {
                foreach (var bSave in data.buildings)
                {
                    BuildingData buildingData = FindBuildingDataByName(bSave.buildingName);
                    if (buildingData == null)
                    {
                        Debug.LogWarning($"[SaveLoad] ⚠ Bina tipi bulunamadı: {bSave.buildingName}");
                        continue;
                    }

                    BuildingInstance instance = BuildingPlacer.Instance.PlaceBuilding(
                        bSave.gridX, bSave.gridZ, buildingData
                    );

                    if (instance != null)
                    {
                        // Seviye uygula
                        for (int i = 1; i < bSave.level; i++)
                            instance.TryUpgrade();

                        instance.SetActive(bSave.isActive);
                    }
                }
            }

            // Yolları yeniden oluştur
            if (RoadSystem.Instance != null)
            {
                foreach (var road in data.roads)
                {
                    RoadSystem.Instance.PlaceRoad(road.x, road.z);
                }
            }

            Debug.Log($"[SaveLoad] ✅ Oyun yüklendi! Gün: {data.currentDay} | " +
                      $"Bina: {data.buildings.Count} | Yol: {data.roads.Count}");

            // Devam et
            GameManager.Instance?.Resume();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoad] ❌ Yükleme hatası: {e.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // YARDIMCILAR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Mevcut tüm binaları ve yolları temizler.</summary>
    private void ClearCurrentState()
    {
        // Binaları temizle
        if (BuildingPlacer.Instance != null)
        {
            var buildings = new List<BuildingInstance>(BuildingPlacer.Instance.PlacedBuildings);
            foreach (var building in buildings)
            {
                if (building != null)
                    BuildingPlacer.Instance.DemolishBuilding(building);
            }
        }

        // Yolları temizle
        if (RoadSystem.Instance != null && GridSystem.Instance != null)
        {
            List<Vector2Int> roadPositions = new List<Vector2Int>();
            for (int x = 0; x < GridSystem.Instance.GridWidth; x++)
            {
                for (int z = 0; z < GridSystem.Instance.GridHeight; z++)
                {
                    if (GridSystem.Instance.GetCellType(x, z) == GridSystem.CellType.Road)
                        roadPositions.Add(new Vector2Int(x, z));
                }
            }

            foreach (var pos in roadPositions)
                RoadSystem.Instance.RemoveRoad(pos.x, pos.y);
        }
    }

    /// <summary>İsimle BuildingData ScriptableObject'ini bulur.</summary>
    private BuildingData FindBuildingDataByName(string name)
    {
        if (BuildingPlacer.Instance?.AvailableBuildings == null) return null;

        foreach (var b in BuildingPlacer.Instance.AvailableBuildings)
        {
            if (b != null && b.buildingName == name) return b;
        }
        return null;
    }

    /// <summary>Kayıt dosyası mevcut mu?</summary>
    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>Kayıt dosyasını siler.</summary>
    public void DeleteSave()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log("[SaveLoad] 🗑️ Kayıt dosyası silindi.");
        }
    }
}
