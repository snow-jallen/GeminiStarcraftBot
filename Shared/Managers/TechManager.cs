using BWAPI.NET;
using Shared.Utils;
using Shared.Intelligence;
using Shared.BuildOrders;

namespace Shared.Managers;

public enum TechTier
{
    Tier1,  // Basic units
    Tier2,  // Advanced units
    Tier3   // Elite units
}

public class TechGoal
{
    public UnitType? BuildingRequired { get; set; }
    public UnitType? AddonRequired { get; set; }
    public TechType? ResearchRequired { get; set; }
    public UpgradeType? UpgradeRequired { get; set; }
    public int Priority { get; set; }
    public bool IsCompleted { get; set; }
}

public class TechManager
{
    public TechTier CurrentTier { get; private set; } = TechTier.Tier1;
    private Queue<TechGoal> _techQueue = new();
    private int _lastTechCheckFrame = 0;

    public void Update(Game game, Player self, ScoutingIntelligence intel, BuildOrderManager buildOrder)
    {
        // Only check tech progression periodically
        if (game.GetFrameCount() - _lastTechCheckFrame < GameConstants.TECH_DECISION_INTERVAL)
            return;

        _lastTechCheckFrame = game.GetFrameCount();

        // Don't tech during opening
        if (!buildOrder.IsOpeningComplete())
            return;

        // Update tech tier based on conditions
        UpdateTechTier(game, self, intel);

        // Process tech queue
        ProcessTechQueue(game, self);
    }

    private void UpdateTechTier(Game game, Player self, ScoutingIntelligence intel)
    {
        var race = self.GetRace();
        int baseCount = self.GetCompletedBases().Count;
        int minerals = self.Minerals();
        int gas = self.Gas();

        // Check for tier advancement
        if (CurrentTier == TechTier.Tier1)
        {
            if (baseCount >= GameConstants.TIER2_MIN_BASES &&
                minerals > GameConstants.TECH_MINERAL_THRESHOLD &&
                gas > GameConstants.TIER2_MIN_GAS)
            {
                AdvanceToTier2(race, intel);
            }
        }
        else if (CurrentTier == TechTier.Tier2)
        {
            if (baseCount >= GameConstants.TIER3_MIN_BASES &&
                gas > GameConstants.TIER3_MIN_GAS &&
                HasTier2Units(self))
            {
                AdvanceToTier3(race, intel);
            }
        }
    }

    private void AdvanceToTier2(Race race, ScoutingIntelligence intel)
    {
        Console.WriteLine("Advancing to Tier 2!");
        CurrentTier = TechTier.Tier2;

        // Queue tier 2 buildings
        if (race == Race.Terran)
        {
            QueueTechBuilding(UnitType.Terran_Factory, priority: 80);
            QueueTechBuilding(UnitType.Terran_Academy, priority: 70);
        }
        else if (race == Race.Protoss)
        {
            QueueTechBuilding(UnitType.Protoss_Cybernetics_Core, priority: 80);
            QueueTechBuilding(UnitType.Protoss_Forge, priority: 60);
        }
        else if (race == Race.Zerg)
        {
            QueueTechBuilding(UnitType.Zerg_Hydralisk_Den, priority: 80);
            QueueTechBuilding(UnitType.Zerg_Lair, priority: 70);
        }
    }

    private void AdvanceToTier3(Race race, ScoutingIntelligence intel)
    {
        Console.WriteLine("Advancing to Tier 3!");
        CurrentTier = TechTier.Tier3;

        // Queue tier 3 buildings
        if (race == Race.Terran)
        {
            QueueTechBuilding(UnitType.Terran_Starport, priority: 80);
            QueueTechBuilding(UnitType.Terran_Armory, priority: 70);
        }
        else if (race == Race.Protoss)
        {
            QueueTechBuilding(UnitType.Protoss_Templar_Archives, priority: 80);
            QueueTechBuilding(UnitType.Protoss_Robotics_Facility, priority: 70);
        }
        else if (race == Race.Zerg)
        {
            QueueTechBuilding(UnitType.Zerg_Spire, priority: 80);
            QueueTechBuilding(UnitType.Zerg_Queens_Nest, priority: 70);
        }
    }

    private bool HasTier2Units(Player self)
    {
        var units = self.GetUnits();
        var tier2Units = new HashSet<UnitType>
        {
            // Terran
            UnitType.Terran_Vulture, UnitType.Terran_Siege_Tank_Tank_Mode,
            UnitType.Terran_Goliath,

            // Protoss
            UnitType.Protoss_Dragoon, UnitType.Protoss_High_Templar,

            // Zerg
            UnitType.Zerg_Hydralisk, UnitType.Zerg_Mutalisk
        };

        return units.Any(u => tier2Units.Contains(u.GetUnitType()));
    }

    private void QueueTechBuilding(UnitType building, int priority)
    {
        _techQueue.Enqueue(new TechGoal
        {
            BuildingRequired = building,
            Priority = priority,
            IsCompleted = false
        });
    }

    private void ProcessTechQueue(Game game, Player self)
    {
        // Process tech goals (building construction handled by BuildManager)
        while (_techQueue.Any())
        {
            var goal = _techQueue.Peek();

            if (goal.IsCompleted)
            {
                _techQueue.Dequeue();
                continue;
            }

            // Check if goal is satisfied
            if (goal.BuildingRequired != null)
            {
                var building = goal.BuildingRequired.Value;
                var hasBuilding = self.GetUnits().Any(u =>
                    u.GetUnitType() == building && u.IsCompleted());

                if (hasBuilding)
                {
                    goal.IsCompleted = true;
                    _techQueue.Dequeue();
                    continue;
                }

                // Building not complete, wait
                break;
            }

            // Other goal types (research, upgrades) can be added here
            _techQueue.Dequeue();
        }
    }

    // Public query methods
    public List<UnitType> GetAvailableCombatUnits(Race race)
    {
        var units = new List<UnitType>();

        if (race == Race.Terran)
        {
            // Tier 1
            units.Add(UnitType.Terran_Marine);

            if (CurrentTier >= TechTier.Tier2)
            {
                units.Add(UnitType.Terran_Medic);
                units.Add(UnitType.Terran_Vulture);
                units.Add(UnitType.Terran_Siege_Tank_Tank_Mode);
            }

            if (CurrentTier >= TechTier.Tier3)
            {
                units.Add(UnitType.Terran_Goliath);
                units.Add(UnitType.Terran_Wraith);
            }
        }
        else if (race == Race.Protoss)
        {
            // Tier 1
            units.Add(UnitType.Protoss_Zealot);

            if (CurrentTier >= TechTier.Tier2)
            {
                units.Add(UnitType.Protoss_Dragoon);
            }

            if (CurrentTier >= TechTier.Tier3)
            {
                units.Add(UnitType.Protoss_High_Templar);
                units.Add(UnitType.Protoss_Dark_Templar);
            }
        }
        else if (race == Race.Zerg)
        {
            // Tier 1
            units.Add(UnitType.Zerg_Zergling);

            if (CurrentTier >= TechTier.Tier2)
            {
                units.Add(UnitType.Zerg_Hydralisk);
            }

            if (CurrentTier >= TechTier.Tier3)
            {
                units.Add(UnitType.Zerg_Mutalisk);
                units.Add(UnitType.Zerg_Lurker);
            }
        }

        return units;
    }

    public UnitType GetPreferredCombatUnit(Race race, ScoutingIntelligence intel)
    {
        var availableUnits = GetAvailableCombatUnits(race);

        // Simple preference: Use highest tier available
        if (availableUnits.Any())
        {
            // Could add logic here to prefer certain units based on enemy composition
            // For now, just return the last (highest tier) unit
            return availableUnits.Last();
        }

        // Fallback to basic unit
        if (race == Race.Terran) return UnitType.Terran_Marine;
        if (race == Race.Protoss) return UnitType.Protoss_Zealot;
        if (race == Race.Zerg) return UnitType.Zerg_Zergling;

        return UnitType.None;
    }

    public bool ShouldBuildTechBuilding(UnitType building)
    {
        return _techQueue.Any(g => g.BuildingRequired == building && !g.IsCompleted);
    }

    public bool HasRequiredTech(UnitType unit, Player self)
    {
        // Check if we have the required buildings to train this unit
        var units = self.GetUnits();

        // Simple checks for common unit requirements
        if (unit == UnitType.Terran_Marine)
        {
            return units.Any(u => u.GetUnitType() == UnitType.Terran_Barracks && u.IsCompleted());
        }
        else if (unit == UnitType.Terran_Medic)
        {
            return units.Any(u => u.GetUnitType() == UnitType.Terran_Academy && u.IsCompleted());
        }
        else if (unit == UnitType.Protoss_Dragoon)
        {
            return units.Any(u => u.GetUnitType() == UnitType.Protoss_Cybernetics_Core && u.IsCompleted());
        }
        else if (unit == UnitType.Zerg_Hydralisk)
        {
            return units.Any(u => u.GetUnitType() == UnitType.Zerg_Hydralisk_Den && u.IsCompleted());
        }

        // Default: assume we can build it
        return true;
    }

    public int GetQueuedTechBuildingCount()
    {
        return _techQueue.Count(g => !g.IsCompleted);
    }
}
