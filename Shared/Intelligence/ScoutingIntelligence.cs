using BWAPI.NET;
using Shared.Utils;

namespace Shared.Intelligence;

public class EnemyUnitInfo
{
    public int UnitId { get; set; }
    public UnitType Type { get; set; }
    public Position LastSeenPosition { get; set; }
    public int LastSeenFrame { get; set; }
    public int LastSeenHitPoints { get; set; }
    public bool IsAlive { get; set; } = true;
}

public class EnemyBuildingInfo
{
    public int BuildingId { get; set; }
    public UnitType Type { get; set; }
    public TilePosition Position { get; set; }
    public int FirstSeenFrame { get; set; }
    public int LastSeenFrame { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsDestroyed { get; set; }
}

public class EnemyBaseInfo
{
    public TilePosition Location { get; set; }
    public int DiscoveredFrame { get; set; }
    public int LastSeenFrame { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> BuildingIds { get; set; } = new();
    public int EstimatedWorkerCount { get; set; }
}

public class ScoutingIntelligence
{
    private Dictionary<int, EnemyUnitInfo> _enemyUnits = new();
    private Dictionary<int, EnemyBuildingInfo> _enemyBuildings = new();
    private List<EnemyBaseInfo> _enemyBases = new();
    private HashSet<TilePosition> _exploredLocations = new();

    // Tech tracking
    public HashSet<UnitType> ObservedTechBuildings { get; } = new();
    public HashSet<UnitType> ObservedUnitTypes { get; } = new();

    // Army composition
    public int EnemyArmySupply { get; private set; }
    public Dictionary<UnitType, int> EnemyArmyComposition { get; } = new();

    // Statistics
    public int TotalEnemyUnitsDiscovered { get; private set; }
    public int TotalEnemyBuildingsDiscovered { get; private set; }

    public void Update(Game game)
    {
        var enemy = game.Enemy();
        if (enemy == null) return;

        var currentFrame = game.GetFrameCount();

        // Clear frame-based data
        EnemyArmyComposition.Clear();
        int armySupply = 0;

        // Update visible enemy units
        foreach (var enemyUnit in enemy.GetUnits())
        {
            if (!enemyUnit.Exists()) continue;

            if (enemyUnit.IsVisible())
            {
                int unitId = enemyUnit.GetID();
                var unitType = enemyUnit.GetUnitType();

                // Track all visible units
                UpdateEnemyUnit(enemyUnit, currentFrame);

                // Track tech buildings
                if (unitType.IsBuilding() && Utils.UnitFilters.IsTechBuilding(unitType))
                {
                    ObservedTechBuildings.Add(unitType);
                }

                // Track army composition (exclude workers, buildings, overlords)
                if (!unitType.IsWorker() && !unitType.IsBuilding() &&
                    unitType != UnitType.Zerg_Overlord && unitType != UnitType.Zerg_Larva)
                {
                    ObservedUnitTypes.Add(unitType);

                    // Update composition count
                    if (!EnemyArmyComposition.ContainsKey(unitType))
                    {
                        EnemyArmyComposition[unitType] = 0;
                    }
                    EnemyArmyComposition[unitType]++;

                    // Add to army supply
                    armySupply += unitType.SupplyRequired();
                }

                // Track enemy bases
                if (unitType.IsResourceDepot())
                {
                    UpdateEnemyBase(enemyUnit, currentFrame);
                }

                // Track buildings
                if (unitType.IsBuilding())
                {
                    UpdateEnemyBuilding(enemyUnit, currentFrame);
                }
            }
        }

        EnemyArmySupply = armySupply;

        // Cleanup stale data - mark units as dead if not seen for 10 seconds
        CleanupStaleData(currentFrame);
    }

    private void UpdateEnemyUnit(Unit unit, int frame)
    {
        int id = unit.GetID();

        if (!_enemyUnits.ContainsKey(id))
        {
            _enemyUnits[id] = new EnemyUnitInfo
            {
                UnitId = id,
                Type = unit.GetUnitType(),
                LastSeenPosition = unit.GetPosition(),
                LastSeenFrame = frame,
                LastSeenHitPoints = unit.GetHitPoints(),
                IsAlive = true
            };
            TotalEnemyUnitsDiscovered++;
        }
        else
        {
            var info = _enemyUnits[id];
            info.LastSeenPosition = unit.GetPosition();
            info.LastSeenFrame = frame;
            info.LastSeenHitPoints = unit.GetHitPoints();
            info.IsAlive = true;
        }
    }

    private void UpdateEnemyBuilding(Unit building, int frame)
    {
        int id = building.GetID();

        if (!_enemyBuildings.ContainsKey(id))
        {
            _enemyBuildings[id] = new EnemyBuildingInfo
            {
                BuildingId = id,
                Type = building.GetUnitType(),
                Position = building.GetTilePosition(),
                FirstSeenFrame = frame,
                LastSeenFrame = frame,
                IsCompleted = building.IsCompleted(),
                IsDestroyed = false
            };
            TotalEnemyBuildingsDiscovered++;
        }
        else
        {
            var info = _enemyBuildings[id];
            info.LastSeenFrame = frame;
            info.IsCompleted = building.IsCompleted();
        }
    }

    private void UpdateEnemyBase(Unit baseUnit, int frame)
    {
        var location = baseUnit.GetTilePosition();

        // Check if we already know about this base
        var existingBase = _enemyBases.FirstOrDefault(b =>
            Math.Abs(b.Location.X - location.X) < 10 &&
            Math.Abs(b.Location.Y - location.Y) < 10);

        if (existingBase == null)
        {
            // New base discovered
            _enemyBases.Add(new EnemyBaseInfo
            {
                Location = location,
                DiscoveredFrame = frame,
                LastSeenFrame = frame,
                IsActive = true,
                BuildingIds = new List<int> { baseUnit.GetID() }
            });

            // Mark location as explored
            _exploredLocations.Add(location);
        }
        else
        {
            // Update existing base
            existingBase.LastSeenFrame = frame;
            existingBase.IsActive = true;
            if (!existingBase.BuildingIds.Contains(baseUnit.GetID()))
            {
                existingBase.BuildingIds.Add(baseUnit.GetID());
            }
        }
    }

    private void CleanupStaleData(int currentFrame)
    {
        // Mark units as dead if not seen for 10 seconds (240 frames)
        foreach (var unit in _enemyUnits.Values)
        {
            if (unit.IsAlive && currentFrame - unit.LastSeenFrame > GameConstants.UNIT_STALE_THRESHOLD)
            {
                unit.IsAlive = false;
            }
        }

        // Mark buildings as destroyed if not seen for a long time
        foreach (var building in _enemyBuildings.Values)
        {
            if (!building.IsDestroyed && currentFrame - building.LastSeenFrame > GameConstants.UNIT_STALE_THRESHOLD * 2)
            {
                building.IsDestroyed = true;
            }
        }
    }

    public TilePosition? GetNextScoutLocation(Game game)
    {
        var startLocations = game.GetStartLocations();
        var self = game.Self();
        if (self == null) return null;

        var myStart = self.GetStartLocation();

        // Priority 1: Unexplored start locations
        foreach (var loc in startLocations)
        {
            // Skip our own start location
            if (loc.X == myStart.X && loc.Y == myStart.Y)
                continue;

            if (!_exploredLocations.Contains(loc))
            {
                return loc;
            }
        }

        // Priority 2: Known enemy bases that haven't been checked recently
        var staleBases = _enemyBases
            .Where(b => b.IsActive && game.GetFrameCount() - b.LastSeenFrame > GameConstants.BASE_RECHECK_INTERVAL)
            .OrderBy(b => b.LastSeenFrame)
            .ToList();

        if (staleBases.Any())
        {
            return staleBases.First().Location;
        }

        // Priority 3: Check middle of map
        var mapCenter = new TilePosition(game.MapWidth() / 2, game.MapHeight() / 2);
        return mapCenter;
    }

    // Query methods
    public EnemyBaseInfo? GetEnemyMainBase()
    {
        return _enemyBases
            .Where(b => b.IsActive)
            .OrderBy(b => b.DiscoveredFrame)
            .FirstOrDefault();
    }

    public List<EnemyBaseInfo> GetEnemyBases()
    {
        return _enemyBases.Where(b => b.IsActive).ToList();
    }

    public List<EnemyBuildingInfo> GetEnemyBuildingsOfType(UnitType type)
    {
        return _enemyBuildings.Values
            .Where(b => b.Type == type && !b.IsDestroyed)
            .ToList();
    }

    public bool HasEnemyTech(UnitType techBuilding)
    {
        return ObservedTechBuildings.Contains(techBuilding);
    }

    public int GetEnemyUnitCount(UnitType type)
    {
        return _enemyUnits.Values.Count(u => u.Type == type && u.IsAlive);
    }

    public int GetLiveEnemyUnitCount()
    {
        return _enemyUnits.Values.Count(u => u.IsAlive);
    }

    public bool HasDiscoveredEnemy()
    {
        return _enemyBases.Any(b => b.IsActive);
    }

    public bool EnemyIsExpanding(int frameCount)
    {
        // Enemy is fast expanding if they have 2+ bases and we've seen them recently
        return _enemyBases.Count(b => b.IsActive && frameCount - b.DiscoveredFrame < 7200) >= 2;
    }

    public bool EnemyDoingEarlyAggression()
    {
        // Early aggression indicators:
        // - Has spawning pool but no expansion (Zerg)
        // - Has multiple barracks/gateways before expansion
        // - Has tech buildings before natural expansion

        if (HasEnemyTech(UnitType.Zerg_Spawning_Pool) && _enemyBases.Count <= 1)
            return true;

        int barracksCount = GetEnemyBuildingsOfType(UnitType.Terran_Barracks).Count;
        int gatewayCount = GetEnemyBuildingsOfType(UnitType.Protoss_Gateway).Count;

        if ((barracksCount >= 2 || gatewayCount >= 2) && _enemyBases.Count <= 1)
            return true;

        return false;
    }

    public void MarkLocationExplored(TilePosition location)
    {
        _exploredLocations.Add(location);
    }
}
