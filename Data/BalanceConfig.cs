using System.Text.Json;
using GrandStrategyGame.Models;

namespace GrandStrategyGame.Data;

/// <summary>
/// Laedt Gameplay-Balance-Werte aus balance-config.json.
/// Zentralisiert alle Magic Numbers die vorher ueber mehrere Dateien verstreut waren.
/// </summary>
public static class BalanceConfig
{
    public static EconomyBalance Economy { get; private set; } = new();
    public static MilitaryBalance Military { get; private set; } = new();
    public static Dictionary<string, UnitStats> Units { get; private set; } = new();
    public static ProductionBalance Production { get; private set; } = new();
    public static TradeBalance Trade { get; private set; } = new();
    public static Dictionary<string, ResourceInfo> Resources { get; private set; } = new();
    public static double[] GameSpeeds { get; private set; } = { 0, 1.0 / 6.0, 1, 8, 24, 72 };
    public static int StartYear { get; private set; } = 2024;

    public static void Load(string basePath)
    {
        string path = Path.Combine(basePath, "Data", "balance-config.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[BalanceConfig] WARNUNG: {path} nicht gefunden, verwende Standardwerte!");
            return;
        }

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("economy", out var eco))
            Economy = ParseEconomy(eco);

        if (root.TryGetProperty("military", out var mil))
            Military = ParseMilitary(mil);

        if (root.TryGetProperty("units", out var units))
            Units = ParseUnits(units);

        if (root.TryGetProperty("production", out var prod))
            Production = ParseProduction(prod);

        if (root.TryGetProperty("trade", out var trade))
            Trade = ParseTrade(trade);

        if (root.TryGetProperty("resources", out var res))
            Resources = ParseResources(res);

        if (root.TryGetProperty("gameSpeeds", out var speeds))
        {
            var list = new List<double>();
            foreach (var s in speeds.EnumerateArray())
                list.Add(s.GetDouble());
            GameSpeeds = list.ToArray();
        }

        if (root.TryGetProperty("startYear", out var year))
            StartYear = year.GetInt32();

        Console.WriteLine($"[BalanceConfig] Geladen: {Units.Count} Einheitentypen, {Resources.Count} Ressourcen");
    }

    private static EconomyBalance ParseEconomy(JsonElement e)
    {
        return new EconomyBalance
        {
            BaseInflation = GetDouble(e, "baseInflation", 0.02),
            DefaultTaxRate = GetDouble(e, "defaultTaxRate", 0.25),
            BaseGrowthRate = GetDouble(e, "baseGrowthRate", 0.02),
            EducationBonusMultiplier = GetDouble(e, "educationBonusMultiplier", 0.01),
            PopulationBonusMultiplier = GetDouble(e, "populationBonusMultiplier", 0.5),
            PopulationBonusClamp = GetDouble(e, "populationBonusClamp", 0.005),
            InfraBonusMultiplier = GetDouble(e, "infraBonusMultiplier", 0.02),
            TaxNeutralRate = GetDouble(e, "taxNeutralRate", 0.25),
            TaxPenaltyMultiplier = GetDouble(e, "taxPenaltyMultiplier", 0.03),
            DebtThreshold = GetDouble(e, "debtThreshold", 0.6),
            DebtPenaltyMultiplier = GetDouble(e, "debtPenaltyMultiplier", 0.01),
            InflationChangeMultiplier = GetDouble(e, "inflationChangeMultiplier", 0.1),
            RandomFactor = GetDouble(e, "randomFactor", 0.01),
            GrowthMin = GetDouble(e, "growthMin", -0.05),
            GrowthMax = GetDouble(e, "growthMax", 0.10),
            InflationMin = GetDouble(e, "inflationMin", -0.05),
            InflationMax = GetDouble(e, "inflationMax", 0.20),
        };
    }

    private static MilitaryBalance ParseMilitary(JsonElement e)
    {
        return new MilitaryBalance
        {
            ActivePersonnelPercent = GetDouble(e, "activePersonnelPercent", 0.005),
            ReservePersonnelPercent = GetDouble(e, "reservePersonnelPercent", 0.02),
            BudgetPercentGDP = GetDouble(e, "budgetPercentGDP", 0.02),
            DefenderBonus = GetDouble(e, "defenderBonus", 1.25),
            EngagingAttackerPenalty = GetDouble(e, "engagingAttackerPenalty", 0.75),
            BaseDamageMultiplier = GetDouble(e, "baseDamageMultiplier", 0.1),
            DamageVarianceMin = GetDouble(e, "damageVarianceMin", 0.8),
            DamageVarianceRange = GetDouble(e, "damageVarianceRange", 0.4),
            DefeatOrgThreshold = GetFloat(e, "defeatOrgThreshold", 0.15f),
            MinOrganization = GetFloat(e, "minOrganization", 0.05f),
            MinMorale = GetFloat(e, "minMorale", 0.1f),
            ExperienceGainPerBattle = GetFloat(e, "experienceGainPerBattle", 0.05f),
            SoftHardAttackWeight = GetDouble(e, "softHardAttackWeight", 0.5),
            OrgDamageMultiplier = GetDouble(e, "orgDamageMultiplier", 0.15),
            OrgDamageDefenseBase = GetDouble(e, "orgDamageDefenseBase", 10),
            ManpowerLossMultiplier = GetDouble(e, "manpowerLossMultiplier", 0.5),
            ManpowerDefenseDivisor = GetDouble(e, "manpowerDefenseDivisor", 100.0),
            MoraleDamageMultiplier = GetFloat(e, "moraleDamageMultiplier", 0.3f),
            DailyWarExhaustion = GetDouble(e, "dailyWarExhaustion", 0.01),
            PeaceExhaustionThreshold = GetDouble(e, "peaceExhaustionThreshold", 0.8),
            MovementCompleteThreshold = GetFloat(e, "movementCompleteThreshold", 0.95f),
            DailyOrgRecovery = GetFloat(e, "dailyOrgRecovery", 0.05f),
            DailyMoraleRecovery = GetFloat(e, "dailyMoraleRecovery", 0.02f),
            CombatReadyOrgThreshold = GetFloat(e, "combatReadyOrgThreshold", 0.5f),
            CombatEffectivenessExpBonus = GetFloat(e, "combatEffectivenessExpBonus", 0.5f),
            StartMorale = GetFloat(e, "startMorale", 0.8f),
        };
    }

    private static Dictionary<string, UnitStats> ParseUnits(JsonElement e)
    {
        var result = new Dictionary<string, UnitStats>();
        foreach (var unit in e.EnumerateObject())
        {
            var v = unit.Value;
            result[unit.Name] = new UnitStats
            {
                Name = GetString(v, "name", unit.Name),
                MaxManpower = GetInt(v, "maxManpower", 5000),
                Attack = GetInt(v, "attack", 20),
                Defense = GetInt(v, "defense", 20),
                SoftAttack = GetInt(v, "softAttack", 10),
                HardAttack = GetInt(v, "hardAttack", 10),
                RecruitmentDays = GetInt(v, "recruitmentDays", 30),
                RecruitCostManpower = GetInt(v, "recruitCostManpower", 5000),
                RecruitCostMoney = GetDouble(v, "recruitCostMoney", 100000),
            };
        }
        return result;
    }

    private static ProductionBalance ParseProduction(JsonElement e)
    {
        var result = new ProductionBalance();

        if (e.TryGetProperty("civilianRecipes", out var civ))
            result.CivilianRecipes = ParseRecipes(civ);

        if (e.TryGetProperty("militaryRecipes", out var mil))
            result.MilitaryRecipes = ParseRecipes(mil);

        if (e.TryGetProperty("populationConsumption", out var pop))
        {
            foreach (var kvp in pop.EnumerateObject())
            {
                if (Enum.TryParse<ResourceType>(kvp.Name, out var type))
                    result.PopulationConsumption[type] = kvp.Value.GetDouble();
            }
        }

        return result;
    }

    private static Dictionary<ResourceType, RecipeConfig> ParseRecipes(JsonElement e)
    {
        var result = new Dictionary<ResourceType, RecipeConfig>();
        foreach (var recipe in e.EnumerateObject())
        {
            if (!Enum.TryParse<ResourceType>(recipe.Name, out var outputType))
                continue;

            var inputs = new List<(ResourceType, double)>();
            if (recipe.Value.TryGetProperty("inputs", out var inputsArray))
            {
                foreach (var input in inputsArray.EnumerateArray())
                {
                    string? resName = GetString(input, "resource", "");
                    double amount = GetDouble(input, "amount", 0);
                    if (Enum.TryParse<ResourceType>(resName, out var inputType))
                        inputs.Add((inputType, amount));
                }
            }

            double output = GetDouble(recipe.Value, "output", 1.0);
            result[outputType] = new RecipeConfig(inputs.ToArray(), output);
        }
        return result;
    }

    private static TradeBalance ParseTrade(JsonElement e)
    {
        var result = new TradeBalance
        {
            EuTradeBonus = GetDouble(e, "euTradeBonus", 1.5),
            AseanTradeBonus = GetDouble(e, "aseanTradeBonus", 1.2),
        };

        if (e.TryGetProperty("regionalGroups", out var groups))
        {
            foreach (var group in groups.EnumerateObject())
            {
                var countries = new List<string>();
                if (group.Value.TryGetProperty("countries", out var arr))
                {
                    foreach (var c in arr.EnumerateArray())
                    {
                        var s = c.GetString();
                        if (s != null) countries.Add(s);
                    }
                }
                double bonus = GetDouble(group.Value, "bonus", 1.0);
                result.RegionalGroups[group.Name] = (countries.ToArray(), bonus);
            }
        }

        return result;
    }

    private static Dictionary<string, ResourceInfo> ParseResources(JsonElement e)
    {
        var result = new Dictionary<string, ResourceInfo>();
        foreach (var res in e.EnumerateObject())
        {
            result[res.Name] = new ResourceInfo
            {
                Name = GetString(res.Value, "name", res.Name),
                BasePrice = GetDouble(res.Value, "basePrice", 100),
            };
        }
        return result;
    }

    // Hilfsmethoden
    private static double GetDouble(JsonElement e, string prop, double fallback)
    {
        return e.TryGetProperty(prop, out var v) ? v.GetDouble() : fallback;
    }

    private static float GetFloat(JsonElement e, string prop, float fallback)
    {
        return e.TryGetProperty(prop, out var v) ? (float)v.GetDouble() : fallback;
    }

    private static int GetInt(JsonElement e, string prop, int fallback)
    {
        return e.TryGetProperty(prop, out var v) ? v.GetInt32() : fallback;
    }

    private static string GetString(JsonElement e, string prop, string fallback)
    {
        return e.TryGetProperty(prop, out var v) ? v.GetString() ?? fallback : fallback;
    }
}

// === Datenklassen ===

public class EconomyBalance
{
    public double BaseInflation { get; init; } = 0.02;
    public double DefaultTaxRate { get; init; } = 0.25;
    public double BaseGrowthRate { get; init; } = 0.02;
    public double EducationBonusMultiplier { get; init; } = 0.01;
    public double PopulationBonusMultiplier { get; init; } = 0.5;
    public double PopulationBonusClamp { get; init; } = 0.005;
    public double InfraBonusMultiplier { get; init; } = 0.02;
    public double TaxNeutralRate { get; init; } = 0.25;
    public double TaxPenaltyMultiplier { get; init; } = 0.03;
    public double DebtThreshold { get; init; } = 0.6;
    public double DebtPenaltyMultiplier { get; init; } = 0.01;
    public double InflationChangeMultiplier { get; init; } = 0.1;
    public double RandomFactor { get; init; } = 0.01;
    public double GrowthMin { get; init; } = -0.05;
    public double GrowthMax { get; init; } = 0.10;
    public double InflationMin { get; init; } = -0.05;
    public double InflationMax { get; init; } = 0.20;
}

public class MilitaryBalance
{
    public double ActivePersonnelPercent { get; init; } = 0.005;
    public double ReservePersonnelPercent { get; init; } = 0.02;
    public double BudgetPercentGDP { get; init; } = 0.02;
    public double DefenderBonus { get; init; } = 1.25;
    public double EngagingAttackerPenalty { get; init; } = 0.75;  // Angreifer aus Distanz: 75% Staerke
    public double BaseDamageMultiplier { get; init; } = 0.1;
    public double DamageVarianceMin { get; init; } = 0.8;
    public double DamageVarianceRange { get; init; } = 0.4;
    public float DefeatOrgThreshold { get; init; } = 0.15f;
    public float MinOrganization { get; init; } = 0.05f;
    public float MinMorale { get; init; } = 0.1f;
    public float ExperienceGainPerBattle { get; init; } = 0.05f;
    public double SoftHardAttackWeight { get; init; } = 0.5;
    public double OrgDamageMultiplier { get; init; } = 0.15;
    public double OrgDamageDefenseBase { get; init; } = 10;
    public double ManpowerLossMultiplier { get; init; } = 0.5;
    public double ManpowerDefenseDivisor { get; init; } = 100.0;
    public float MoraleDamageMultiplier { get; init; } = 0.3f;
    public double DailyWarExhaustion { get; init; } = 0.01;
    public double PeaceExhaustionThreshold { get; init; } = 0.8;
    public float MovementCompleteThreshold { get; init; } = 0.95f;
    public float DailyOrgRecovery { get; init; } = 0.05f;
    public float DailyMoraleRecovery { get; init; } = 0.02f;
    public float CombatReadyOrgThreshold { get; init; } = 0.5f;
    public float CombatEffectivenessExpBonus { get; init; } = 0.5f;
    public float StartMorale { get; init; } = 0.8f;
}

public class UnitStats
{
    public string Name { get; init; } = "";
    public int MaxManpower { get; init; } = 5000;
    public int Attack { get; init; } = 20;
    public int Defense { get; init; } = 20;
    public int SoftAttack { get; init; } = 10;
    public int HardAttack { get; init; } = 10;
    public int RecruitmentDays { get; init; } = 30;
    public int RecruitCostManpower { get; init; } = 5000;
    public double RecruitCostMoney { get; init; } = 100000;
}

public class ProductionBalance
{
    public Dictionary<ResourceType, RecipeConfig> CivilianRecipes { get; set; } = new();
    public Dictionary<ResourceType, RecipeConfig> MilitaryRecipes { get; set; } = new();
    public Dictionary<ResourceType, double> PopulationConsumption { get; set; } = new();
}

public class RecipeConfig
{
    public (ResourceType Type, double Amount)[] Inputs { get; }
    public double Output { get; }

    public RecipeConfig((ResourceType, double)[] inputs, double output)
    {
        Inputs = inputs;
        Output = output;
    }
}

public class TradeBalance
{
    public double EuTradeBonus { get; init; } = 1.5;
    public double AseanTradeBonus { get; init; } = 1.2;
    public Dictionary<string, (string[] Countries, double Bonus)> RegionalGroups { get; set; } = new();
}

public class ResourceInfo
{
    public string Name { get; init; } = "";
    public double BasePrice { get; init; } = 100;
}
