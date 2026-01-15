using BWAPI.NET;
using Shared.Utils;

namespace Shared.Intelligence;

public enum ThreatLevel
{
    None,
    Scouting,    // 1-2 units
    Harassment,  // 3-6 units
    Attack,      // 7-15 units
    AllIn        // 16+ units
}

public class ActiveThreat
{
    public ThreatLevel Level { get; set; }
    public List<int> EnemyUnitIds { get; set; } = new();
    public Position CenterPosition { get; set; }
    public int FirstDetectedFrame { get; set; }
    public int ThreatSupply { get; set; }
    public TilePosition? NearestBaseLocation { get; set; }
}

public class UnitCluster
{
    public List<Unit> Units { get; set; } = new();
    public Position Center { get; set; }
    public int TotalSupply { get; set; }
}

public class ThreatAssessment
{
    private List<ActiveThreat> _activeThreats = new();
    private Dictionary<TilePosition, int> _lastEnemySeenFrame = new();

    public void Update(Game game, Player self, ScoutingIntelligence intel)
    {
        _activeThreats.Clear();

        var myBases = self.GetBases();
        var enemy = game.Enemy();
        if (enemy == null) return;

        // Get all visible enemy combat units
        var enemyUnits = enemy.GetUnits()
            .Where(u => u.IsVisible() &&
                       !u.GetUnitType().IsWorker() &&
                       !u.GetUnitType().IsBuilding() &&
                       u.GetUnitType() != UnitType.Zerg_Overlord &&
                       u.GetUnitType() != UnitType.Zerg_Larva)
            .ToList();

        if (!enemyUnits.Any())
            return;

        // Cluster enemy units by proximity
        var clusters = ClusterEnemyUnits(enemyUnits, GameConstants.THREAT_CLUSTER_RADIUS);

        // Evaluate each cluster as a potential threat
        foreach (var cluster in clusters)
        {
            // Find nearest base to this cluster
            Unit? nearestBase = null;
            int minDistance = int.MaxValue;

            foreach (var baseUnit in myBases)
            {
                int distance = baseUnit.GetDistance(cluster.Center);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestBase = baseUnit;
                }
            }

            // Only consider it a threat if it's close to one of our bases
            if (nearestBase != null && minDistance < GameConstants.THREAT_DETECTION_RANGE)
            {
                var threat = new ActiveThreat
                {
                    Level = CalculateThreatLevel(cluster.Units.Count, cluster.TotalSupply),
                    EnemyUnitIds = cluster.Units.Select(u => u.GetID()).ToList(),
                    CenterPosition = cluster.Center,
                    FirstDetectedFrame = game.GetFrameCount(),
                    ThreatSupply = cluster.TotalSupply,
                    NearestBaseLocation = nearestBase.GetTilePosition()
                };

                _activeThreats.Add(threat);

                // Update last seen frame for this location
                var tilePos = new TilePosition(cluster.Center.X / 32, cluster.Center.Y / 32);
                _lastEnemySeenFrame[tilePos] = game.GetFrameCount();
            }
        }
    }

    private List<UnitCluster> ClusterEnemyUnits(List<Unit> units, int clusterRadius)
    {
        var clusters = new List<UnitCluster>();
        var processed = new HashSet<int>();

        foreach (var unit in units)
        {
            if (processed.Contains(unit.GetID()))
                continue;

            // Start a new cluster
            var cluster = new UnitCluster
            {
                Units = new List<Unit> { unit }
            };
            processed.Add(unit.GetID());

            // Find all units within cluster radius
            foreach (var otherUnit in units)
            {
                if (processed.Contains(otherUnit.GetID()))
                    continue;

                if (unit.GetDistance(otherUnit) <= clusterRadius)
                {
                    cluster.Units.Add(otherUnit);
                    processed.Add(otherUnit.GetID());
                }
            }

            // Calculate cluster center and supply
            if (cluster.Units.Any())
            {
                int avgX = (int)cluster.Units.Average(u => u.GetPosition().X);
                int avgY = (int)cluster.Units.Average(u => u.GetPosition().Y);
                cluster.Center = new Position(avgX, avgY);
                cluster.TotalSupply = cluster.Units.Sum(u => u.GetUnitType().SupplyRequired());
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    private ThreatLevel CalculateThreatLevel(int unitCount, int supply)
    {
        if (unitCount <= GameConstants.THREAT_SCOUTING_MAX)
            return ThreatLevel.Scouting;

        if (unitCount <= GameConstants.THREAT_HARASSMENT_MAX)
            return ThreatLevel.Harassment;

        if (unitCount <= GameConstants.THREAT_ATTACK_MAX || supply <= 30)
            return ThreatLevel.Attack;

        return ThreatLevel.AllIn;
    }

    // Query methods
    public List<ActiveThreat> GetActiveThreats()
    {
        return new List<ActiveThreat>(_activeThreats);
    }

    public ThreatLevel GetThreatLevelAtBase(Position basePos)
    {
        var threatsNearBase = _activeThreats
            .Where(t => t.CenterPosition.GetDistance(basePos) < GameConstants.THREAT_DETECTION_RANGE)
            .OrderByDescending(t => t.Level)
            .ToList();

        return threatsNearBase.Any() ? threatsNearBase.First().Level : ThreatLevel.None;
    }

    public bool IsUnderAttack()
    {
        return _activeThreats.Any(t => t.Level >= ThreatLevel.Harassment);
    }

    public bool ShouldPullWorkers()
    {
        // Pull workers only for harassment or small attacks (not all-ins)
        return _activeThreats.Any(t =>
            t.Level == ThreatLevel.Harassment &&
            t.ThreatSupply < GameConstants.THREAT_PULL_WORKERS_MAX_SUPPLY);
    }

    public Position? GetDefensePosition()
    {
        // Return the position of the highest priority threat
        var highestThreat = _activeThreats
            .OrderByDescending(t => t.Level)
            .ThenByDescending(t => t.ThreatSupply)
            .FirstOrDefault();

        return highestThreat?.CenterPosition;
    }

    public ActiveThreat? GetMostSeriousThreat()
    {
        return _activeThreats
            .OrderByDescending(t => t.Level)
            .ThenByDescending(t => t.ThreatSupply)
            .FirstOrDefault();
    }

    public int GetTotalThreateningSupply()
    {
        return _activeThreats.Sum(t => t.ThreatSupply);
    }

    public bool HasRecentEnemyActivity(TilePosition location, int frameCount, int threshold = 1200)
    {
        // Check if enemies were seen at this location recently (within last 50 seconds)
        if (_lastEnemySeenFrame.TryGetValue(location, out int lastFrame))
        {
            return frameCount - lastFrame < threshold;
        }
        return false;
    }
}
