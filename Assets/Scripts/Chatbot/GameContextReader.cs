/// <summary>
/// Anlık şehir verilerini çeken bağlam okuyucu.
/// GDD AI-CSDD Bölüm 4: Şehir Veri Entegrasyonu
/// Chatbot kararlarını beslemek için tüm sistemlerden snapshot alır.
/// </summary>
public struct CitySnapshot
{
    // Finans
    public float money;
    public float netIncome;

    // Enerji & Su
    public float energyProduction;
    public float energyConsumption;
    public bool isBlackout;
    public float waterProduction;
    public float waterConsumption;
    public bool isWaterShortage;

    // Nüfus
    public int population;
    public int populationCapacity;

    // Çevre
    public float airPollution;
    public float pollutedAreaPct;
    public float carbonEmission;
    public float averageNoise;

    // Vatandaş
    public float satisfaction;
    public float health;
    public float happiness;
    public int lastMigration;

    // Trafik
    public int totalRoads;
    public float avgCongestion;

    // Oyun
    public int currentDay;

    // ─── Durum Sınıflandırması ──────────────────────────────────
    public CrisisLevel GetCrisisLevel()
    {
        if (money < -5000f || satisfaction < 30f || isBlackout || isWaterShortage)
            return CrisisLevel.Critical;
        if (airPollution > 3f || satisfaction < 50f || money < 0 || avgCongestion > 0.7f)
            return CrisisLevel.Warning;
        return CrisisLevel.Stable;
    }
}

public enum CrisisLevel { Stable, Warning, Critical }

public static class GameContextReader
{
    /// <summary>Tüm singleton sistemlerden anlık şehir verisi çeker.</summary>
    public static CitySnapshot ReadCityData()
    {
        CitySnapshot s = new CitySnapshot();

        if (ResourceSystem.Instance != null)
        {
            var rs = ResourceSystem.Instance;
            s.money = rs.Money;
            s.netIncome = rs.NetIncomePerTick;
            s.energyProduction = rs.EnergyProduction;
            s.energyConsumption = rs.EnergyConsumption;
            s.isBlackout = rs.IsBlackout;
            s.waterProduction = rs.WaterProduction;
            s.waterConsumption = rs.WaterConsumption;
            s.isWaterShortage = rs.IsWaterShortage;
            s.population = rs.Population;
            s.populationCapacity = rs.PopulationCapacity;
        }

        if (PollutionSystem.Instance != null)
        {
            s.airPollution = PollutionSystem.Instance.AverageAirPollution;
            s.pollutedAreaPct = PollutionSystem.Instance.PollutedAreaPercentage;
            s.carbonEmission = PollutionSystem.Instance.GlobalCarbonEmission;
        }

        if (NoiseSystem.Instance != null)
        {
            s.averageNoise = NoiseSystem.Instance.AverageNoise;
        }

        if (CitizenSystem.Instance != null)
        {
            s.satisfaction = CitizenSystem.Instance.GlobalSatisfaction;
            s.health = CitizenSystem.Instance.GlobalHealth;
            s.happiness = CitizenSystem.Instance.GlobalHappiness;
            s.lastMigration = CitizenSystem.Instance.LastMigration;
        }

        if (RoadSystem.Instance != null)
        {
            s.totalRoads = RoadSystem.Instance.TotalRoads;
            s.avgCongestion = RoadSystem.Instance.AverageCongestion;
        }

        if (GameManager.Instance != null)
        {
            s.currentDay = GameManager.Instance.CurrentDay;
        }

        return s;
    }
}
