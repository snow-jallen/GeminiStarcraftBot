using BWAPI.NET;

namespace Shared.Utils;

public static class UnitFilters
{
    // Worker filters
    public static List<Unit> GetIdleWorkers(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsWorker() && u.IsIdle())
            .ToList();
    }

    public static List<Unit> GetMineralWorkers(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsWorker() && u.IsGatheringMinerals())
            .ToList();
    }

    public static List<Unit> GetGasWorkers(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsWorker() && u.IsGatheringGas())
            .ToList();
    }

    public static List<Unit> GetAllWorkers(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsWorker())
            .ToList();
    }

    // Army filters
    public static List<Unit> GetCombatUnits(this Player player)
    {
        return player.GetUnits()
            .Where(u => !u.GetUnitType().IsWorker()
                     && !u.GetUnitType().IsBuilding()
                     && !u.GetUnitType().IsRefinery()
                     && u.GetUnitType() != UnitType.Zerg_Overlord
                     && u.GetUnitType() != UnitType.Zerg_Larva)
            .ToList();
    }

    public static List<Unit> GetRangedUnits(this Player player)
    {
        return player.GetCombatUnits()
            .Where(u => u.GetUnitType().GroundWeapon().MaxRange() > 32)
            .ToList();
    }

    public static List<Unit> GetMeleeUnits(this Player player)
    {
        return player.GetCombatUnits()
            .Where(u => u.GetUnitType().GroundWeapon().MaxRange() <= 32)
            .ToList();
    }

    // Building filters
    public static List<Unit> GetBases(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsResourceDepot())
            .ToList();
    }

    public static List<Unit> GetCompletedBases(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsResourceDepot() && u.IsCompleted())
            .ToList();
    }

    public static List<Unit> GetProductionBuildings(this Player player, Race race)
    {
        var productionTypes = new List<UnitType>();

        if (race == Race.Terran)
        {
            productionTypes.AddRange(new[] {
                UnitType.Terran_Barracks,
                UnitType.Terran_Factory,
                UnitType.Terran_Starport
            });
        }
        else if (race == Race.Protoss)
        {
            productionTypes.AddRange(new[] {
                UnitType.Protoss_Gateway,
                UnitType.Protoss_Robotics_Facility,
                UnitType.Protoss_Stargate
            });
        }
        else if (race == Race.Zerg)
        {
            productionTypes.AddRange(new[] {
                UnitType.Zerg_Hatchery,
                UnitType.Zerg_Lair,
                UnitType.Zerg_Hive
            });
        }

        return player.GetUnits()
            .Where(u => productionTypes.Contains(u.GetUnitType()) && u.IsCompleted())
            .ToList();
    }

    public static List<Unit> GetRefineries(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsRefinery())
            .ToList();
    }

    public static List<Unit> GetCompletedRefineries(this Player player)
    {
        return player.GetUnits()
            .Where(u => u.GetUnitType().IsRefinery() && u.IsCompleted())
            .ToList();
    }

    // Tech building checks
    public static bool IsTechBuilding(UnitType type)
    {
        var techBuildings = new HashSet<UnitType>
        {
            // Terran
            UnitType.Terran_Factory, UnitType.Terran_Starport, UnitType.Terran_Academy,
            UnitType.Terran_Engineering_Bay, UnitType.Terran_Armory, UnitType.Terran_Science_Facility,
            UnitType.Terran_Machine_Shop, UnitType.Terran_Control_Tower, UnitType.Terran_Comsat_Station,

            // Protoss
            UnitType.Protoss_Cybernetics_Core, UnitType.Protoss_Citadel_of_Adun,
            UnitType.Protoss_Templar_Archives, UnitType.Protoss_Robotics_Facility,
            UnitType.Protoss_Stargate, UnitType.Protoss_Fleet_Beacon, UnitType.Protoss_Forge,
            UnitType.Protoss_Robotics_Support_Bay, UnitType.Protoss_Observatory,

            // Zerg
            UnitType.Zerg_Spawning_Pool, UnitType.Zerg_Hydralisk_Den, UnitType.Zerg_Spire,
            UnitType.Zerg_Queens_Nest, UnitType.Zerg_Ultralisk_Cavern, UnitType.Zerg_Defiler_Mound,
            UnitType.Zerg_Greater_Spire, UnitType.Zerg_Evolution_Chamber
        };

        return techBuildings.Contains(type);
    }

    public static bool IsDefenseBuilding(UnitType type)
    {
        var defenseBuildings = new HashSet<UnitType>
        {
            UnitType.Terran_Bunker, UnitType.Terran_Missile_Turret,
            UnitType.Protoss_Photon_Cannon, UnitType.Protoss_Shield_Battery,
            UnitType.Zerg_Sunken_Colony, UnitType.Zerg_Spore_Colony, UnitType.Zerg_Creep_Colony
        };

        return defenseBuildings.Contains(type);
    }

    // Unit state filters
    public static List<Unit> GetIdleUnits(this IEnumerable<Unit> units)
    {
        return units.Where(u => u.IsIdle()).ToList();
    }

    public static List<Unit> GetDamagedUnits(this IEnumerable<Unit> units, double hpThreshold = 0.5)
    {
        return units.Where(u =>
            u.GetHitPoints() > 0 &&
            u.GetHitPoints() < u.GetUnitType().MaxHitPoints() * hpThreshold
        ).ToList();
    }

    public static List<Unit> GetUnitsInRange(this IEnumerable<Unit> units, Position center, int range)
    {
        return units.Where(u => u.GetDistance(center) <= range).ToList();
    }

    // Resource queries
    public static int GetTotalSupplyUsed(this IEnumerable<Unit> units)
    {
        return units.Sum(u => u.GetUnitType().SupplyRequired());
    }
}
