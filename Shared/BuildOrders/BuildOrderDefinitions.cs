using BWAPI.NET;

namespace Shared.BuildOrders;

public enum BuildOrderType
{
    // Terran
    Terran_2RaxPressure,
    Terran_1FactoryExpand,
    Terran_BioTech,

    // Protoss
    Protoss_2GateZealot,
    Protoss_FastExpand,
    Protoss_DarkTemplar,

    // Zerg
    Zerg_9Pool,
    Zerg_12Hatch,
    Zerg_MutaliskRush
}

public class BuildStep
{
    public int Supply { get; set; }               // Execute at this supply (BWAPI doubled: 16 = 8 supply)
    public UnitType? BuildingToBuild { get; set; }
    public UnitType? UnitToTrain { get; set; }
    public int Count { get; set; } = 1;
    public bool IsCompleted { get; set; }
}

public class BuildOrder
{
    public BuildOrderType Type { get; set; }
    public Race Race { get; set; }
    public List<BuildStep> Steps { get; set; } = new();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CurrentStepIndex { get; set; } = 0;
}

public static class BuildOrderDefinitions
{
    // ===== TERRAN BUILD ORDERS =====

    public static BuildOrder GetTerran2RaxPressure()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Terran_2RaxPressure,
            Race = Race.Terran,
            Name = "2 Rax Pressure",
            Description = "Aggressive 2 Barracks Marine push",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 20, BuildingToBuild = UnitType.Terran_Barracks },
                new() { Supply = 22, BuildingToBuild = UnitType.Terran_Barracks },
                new() { Supply = 24, BuildingToBuild = UnitType.Terran_Refinery },
                new() { Supply = 26, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 30, BuildingToBuild = UnitType.Terran_Academy },
            }
        };
    }

    public static BuildOrder GetTerran1FactoryExpand()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Terran_1FactoryExpand,
            Race = Race.Terran,
            Name = "1 Factory Expand",
            Description = "Economic expand with Factory for tanks",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 20, BuildingToBuild = UnitType.Terran_Barracks },
                new() { Supply = 24, BuildingToBuild = UnitType.Terran_Refinery },
                new() { Supply = 26, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 28, BuildingToBuild = UnitType.Terran_Factory },
                new() { Supply = 32, BuildingToBuild = UnitType.Terran_Command_Center },  // Expand
            }
        };
    }

    public static BuildOrder GetTerranBioTech()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Terran_BioTech,
            Race = Race.Terran,
            Name = "Bio + Tech",
            Description = "Standard bio with tech upgrades",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 20, BuildingToBuild = UnitType.Terran_Barracks },
                new() { Supply = 24, BuildingToBuild = UnitType.Terran_Supply_Depot },
                new() { Supply = 26, BuildingToBuild = UnitType.Terran_Barracks },
                new() { Supply = 28, BuildingToBuild = UnitType.Terran_Refinery },
                new() { Supply = 32, BuildingToBuild = UnitType.Terran_Academy },
                new() { Supply = 36, BuildingToBuild = UnitType.Terran_Command_Center },  // Expand
            }
        };
    }

    // ===== PROTOSS BUILD ORDERS =====

    public static BuildOrder GetProtoss2GateZealot()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Protoss_2GateZealot,
            Race = Race.Protoss,
            Name = "2 Gate Zealot",
            Description = "Aggressive early zealot pressure",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Protoss_Pylon },
                new() { Supply = 20, BuildingToBuild = UnitType.Protoss_Gateway },
                new() { Supply = 24, BuildingToBuild = UnitType.Protoss_Gateway },
                new() { Supply = 28, BuildingToBuild = UnitType.Protoss_Pylon },
                new() { Supply = 32, BuildingToBuild = UnitType.Protoss_Assimilator },
            }
        };
    }

    public static BuildOrder GetProtossFastExpand()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Protoss_FastExpand,
            Race = Race.Protoss,
            Name = "Fast Expand",
            Description = "Economic expand with Gateway defense",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Protoss_Pylon },
                new() { Supply = 20, BuildingToBuild = UnitType.Protoss_Gateway },
                new() { Supply = 24, BuildingToBuild = UnitType.Protoss_Nexus },  // Expand!
                new() { Supply = 26, BuildingToBuild = UnitType.Protoss_Cybernetics_Core },
                new() { Supply = 28, BuildingToBuild = UnitType.Protoss_Assimilator },
                new() { Supply = 32, BuildingToBuild = UnitType.Protoss_Gateway },
                new() { Supply = 36, BuildingToBuild = UnitType.Protoss_Pylon },
            }
        };
    }

    public static BuildOrder GetProtossDarkTemplar()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Protoss_DarkTemplar,
            Race = Race.Protoss,
            Name = "Dark Templar Rush",
            Description = "Fast Dark Templar for surprise attack",
            Steps = new List<BuildStep>
            {
                new() { Supply = 16, BuildingToBuild = UnitType.Protoss_Pylon },
                new() { Supply = 20, BuildingToBuild = UnitType.Protoss_Gateway },
                new() { Supply = 24, BuildingToBuild = UnitType.Protoss_Assimilator },
                new() { Supply = 26, BuildingToBuild = UnitType.Protoss_Cybernetics_Core },
                new() { Supply = 28, BuildingToBuild = UnitType.Protoss_Pylon },
                new() { Supply = 30, BuildingToBuild = UnitType.Protoss_Citadel_of_Adun },
                new() { Supply = 34, BuildingToBuild = UnitType.Protoss_Templar_Archives },
            }
        };
    }

    // ===== ZERG BUILD ORDERS =====

    public static BuildOrder GetZerg9Pool()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Zerg_9Pool,
            Race = Race.Zerg,
            Name = "9 Pool",
            Description = "Fast Zergling aggression",
            Steps = new List<BuildStep>
            {
                new() { Supply = 18, BuildingToBuild = UnitType.Zerg_Spawning_Pool },
                new() { Supply = 20, BuildingToBuild = UnitType.Zerg_Extractor },
                new() { Supply = 22, UnitToTrain = UnitType.Zerg_Zergling, Count = 6 },
                new() { Supply = 34, BuildingToBuild = UnitType.Zerg_Hatchery },  // Expand
            }
        };
    }

    public static BuildOrder GetZerg12Hatch()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Zerg_12Hatch,
            Race = Race.Zerg,
            Name = "12 Hatch",
            Description = "Fast expand with pool after",
            Steps = new List<BuildStep>
            {
                new() { Supply = 24, BuildingToBuild = UnitType.Zerg_Hatchery },  // Expand first!
                new() { Supply = 26, BuildingToBuild = UnitType.Zerg_Spawning_Pool },
                new() { Supply = 28, BuildingToBuild = UnitType.Zerg_Extractor },
                new() { Supply = 30, BuildingToBuild = UnitType.Zerg_Hydralisk_Den },
            }
        };
    }

    public static BuildOrder GetZergMutaliskRush()
    {
        return new BuildOrder
        {
            Type = BuildOrderType.Zerg_MutaliskRush,
            Race = Race.Zerg,
            Name = "Mutalisk Rush",
            Description = "Fast tech to mutalisks",
            Steps = new List<BuildStep>
            {
                new() { Supply = 18, BuildingToBuild = UnitType.Zerg_Spawning_Pool },
                new() { Supply = 20, BuildingToBuild = UnitType.Zerg_Extractor },
                new() { Supply = 22, BuildingToBuild = UnitType.Zerg_Hatchery },  // Expand
                new() { Supply = 26, BuildingToBuild = UnitType.Zerg_Lair },  // Morph to Lair
                new() { Supply = 30, BuildingToBuild = UnitType.Zerg_Spire },
            }
        };
    }

    // ===== BUILD ORDER SELECTION =====

    public static BuildOrder GetBuildOrderForRace(Race race)
    {
        return race switch
        {
            Race.Terran => GetTerran1FactoryExpand(),
            Race.Protoss => GetProtossFastExpand(),
            Race.Zerg => GetZerg12Hatch(),
            _ => GetTerran1FactoryExpand()
        };
    }
}
