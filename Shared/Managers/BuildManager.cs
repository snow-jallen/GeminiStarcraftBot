using BWAPI.NET;
using Shared.Utils;

namespace Shared.Managers;

public class BuildTask
{
    public UnitType BuildingType { get; set; }
    public TilePosition? Location { get; set; }
    public int BuilderUnitId { get; set; }
    public int Priority { get; set; }
    public bool IsStarted { get; set; }
    public int QueuedFrame { get; set; }
}

public class BuildManager
{
    private Queue<BuildTask> _buildQueue = new();
    private Dictionary<UnitType, int> _inProgressCount = new();
    private List<BuildTask> _activeTasks = new();

    public void Update(Game game, Player self, object? workerManager = null)
    {
        // Update in-progress counts
        UpdateInProgressCounts(self);

        // Process build queue
        ProcessBuildQueue(game, self);

        // Clean up completed tasks
        CleanupCompletedTasks(self);
    }

    private void UpdateInProgressCounts(Player self)
    {
        _inProgressCount.Clear();

        var allUnits = self.GetUnits();
        foreach (var unit in allUnits)
        {
            var unitType = unit.GetUnitType();

            // Count incomplete buildings
            if (unitType.IsBuilding() && !unit.IsCompleted())
            {
                if (!_inProgressCount.ContainsKey(unitType))
                {
                    _inProgressCount[unitType] = 0;
                }
                _inProgressCount[unitType]++;
            }

            // Count workers actively building
            if (unitType.IsWorker() && unit.IsConstructing())
            {
                var buildType = unit.GetBuildType();
                if (buildType != UnitType.None)
                {
                    if (!_inProgressCount.ContainsKey(buildType))
                    {
                        _inProgressCount[buildType] = 0;
                    }
                    _inProgressCount[buildType]++;
                }
            }
        }
    }

    private void ProcessBuildQueue(Game game, Player self)
    {
        if (!_buildQueue.Any())
            return;

        // Process highest priority task
        var task = _buildQueue.Peek();

        if (!task.IsStarted)
        {
            // Try to start the build
            if (ExecuteBuildTask(game, self, task))
            {
                task.IsStarted = true;
                _activeTasks.Add(task);
                _buildQueue.Dequeue();
            }
        }
    }

    private bool ExecuteBuildTask(Game game, Player self, BuildTask task)
    {
        var building = task.BuildingType;

        // Check if we have resources
        if (self.Minerals() < building.MineralPrice() ||
            self.Gas() < building.GasPrice())
        {
            return false;
        }

        // Get a worker
        Unit? worker = GetAvailableBuilder(self);
        if (worker == null)
            return false;

        // Find or use build location
        TilePosition? buildLoc = task.Location;
        if (buildLoc == null)
        {
            buildLoc = FindBuildLocation(game, self, building);
            if (buildLoc == null)
                return false;
        }

        // Issue build command
        worker.Build(building, buildLoc.Value);
        task.BuilderUnitId = worker.GetID();
        return true;
    }

    private Unit? GetAvailableBuilder(Player self)
    {
        // Prefer idle workers
        var idleWorker = self.GetIdleWorkers().FirstOrDefault();
        if (idleWorker != null)
            return idleWorker;

        // Use mineral worker if needed
        return self.GetMineralWorkers().FirstOrDefault();
    }

    private TilePosition? FindBuildLocation(Game game, Player self, UnitType building)
    {
        var bases = self.GetCompletedBases();
        if (!bases.Any())
            bases = self.GetBases();

        if (!bases.Any())
            return null;

        // Try near each base
        foreach (var baseUnit in bases)
        {
            var location = FindBuildLocationNearPosition(game, building, baseUnit.GetTilePosition());
            if (location != null)
                return location;
        }

        return null;
    }

    private TilePosition? FindBuildLocationNearPosition(Game game, UnitType building, TilePosition near)
    {
        // Spiral search
        for (int radius = GameConstants.BUILD_LOCATION_SEARCH_RADIUS_MIN;
             radius < GameConstants.BUILD_LOCATION_SEARCH_RADIUS_MAX;
             radius++)
        {
            // Try multiple positions at this radius
            for (int angle = 0; angle < 8; angle++)
            {
                int dx = 0, dy = 0;
                switch (angle)
                {
                    case 0: dx = radius; dy = 0; break;
                    case 1: dx = radius; dy = radius; break;
                    case 2: dx = 0; dy = radius; break;
                    case 3: dx = -radius; dy = radius; break;
                    case 4: dx = -radius; dy = 0; break;
                    case 5: dx = -radius; dy = -radius; break;
                    case 6: dx = 0; dy = -radius; break;
                    case 7: dx = radius; dy = -radius; break;
                }

                var testPos = new TilePosition(near.X + dx, near.Y + dy);

                if (testPos.X >= 0 && testPos.Y >= 0 &&
                    testPos.X < game.MapWidth() && testPos.Y < game.MapHeight())
                {
                    if (game.CanBuildHere(testPos, building))
                    {
                        // Additional check: don't block minerals/gas
                        if (!IsBlockingResources(game, testPos, building))
                        {
                            return testPos;
                        }
                    }
                }
            }
        }

        return null;
    }

    private bool IsBlockingResources(Game game, TilePosition position, UnitType building)
    {
        // Simple check: if there are minerals or gas within 3 tiles, consider it blocking
        var posPixel = new Position(position.X * 32, position.Y * 32);
        var blockRadius = 3 * 32; // 3 tiles

        var minerals = game.GetMinerals();
        foreach (var mineral in minerals)
        {
            if (mineral.GetDistance(posPixel) < blockRadius)
                return true;
        }

        var geysers = game.GetGeysers();
        foreach (var geyser in geysers)
        {
            if (geyser.GetDistance(posPixel) < blockRadius)
                return true;
        }

        return false;
    }

    private void CleanupCompletedTasks(Player self)
    {
        // Remove tasks where building is completed or builder died
        _activeTasks.RemoveAll(task =>
        {
            // Check if building exists and is complete
            var allUnits = self.GetUnits();
            var building = allUnits.FirstOrDefault(u =>
                u.GetUnitType() == task.BuildingType &&
                task.Location != null &&
                Math.Abs(u.GetTilePosition().X - task.Location.Value.X) < 5 &&
                Math.Abs(u.GetTilePosition().Y - task.Location.Value.Y) < 5);

            if (building != null && building.IsCompleted())
                return true;

            // Check if builder died or gave up (task older than 30 seconds)
            // Note: game.GetFrameCount() would be needed here, but we don't have game in this method
            // For now, keep task active until building completes
            return false;
        });
    }

    // Public methods
    public void QueueBuilding(UnitType building, int priority = 0, TilePosition? location = null, int currentFrame = 0)
    {
        var task = new BuildTask
        {
            BuildingType = building,
            Location = location,
            Priority = priority,
            IsStarted = false,
            QueuedFrame = currentFrame
        };

        // Insert by priority (higher priority first)
        var queueList = _buildQueue.ToList();
        queueList.Add(task);
        queueList = queueList.OrderByDescending(t => t.Priority).ToList();
        _buildQueue = new Queue<BuildTask>(queueList);
    }

    public bool IsBuilding(UnitType building)
    {
        return _inProgressCount.ContainsKey(building) && _inProgressCount[building] > 0;
    }

    public int GetBuildingCount(Player self, UnitType building, bool includeIncomplete = true)
    {
        var units = self.GetUnits();

        if (includeIncomplete)
        {
            return units.Count(u => u.GetUnitType() == building);
        }
        else
        {
            return units.Count(u => u.GetUnitType() == building && u.IsCompleted());
        }
    }

    public int GetInProgressCount(UnitType building)
    {
        return _inProgressCount.ContainsKey(building) ? _inProgressCount[building] : 0;
    }

    public int GetQueuedCount(UnitType building)
    {
        return _buildQueue.Count(t => t.BuildingType == building);
    }

    public List<BuildTask> GetActiveTasks()
    {
        return new List<BuildTask>(_activeTasks);
    }

    public void ClearQueue()
    {
        _buildQueue.Clear();
    }
}
