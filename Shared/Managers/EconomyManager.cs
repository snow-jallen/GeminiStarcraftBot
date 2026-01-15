using BWAPI.NET;
using Shared.Utils;
using Shared.Intelligence;

namespace Shared.Managers;

public class EconomyManager
{
    private int _targetWorkerCount = GameConstants.MIN_WORKER_COUNT;
    private int _desiredGasGeysers = 0;
    private int _mineralReservations = 0;
    private int _gasReservations = 0;
    private int _lastWorkerBalanceFrame = 0;

    // Dependencies (will be injected via Update)
    private ScoutingIntelligence? _scoutingIntel;
    private object? _buildOrderManager; // Will be properly typed later

    public void Update(Game game, Player self, ScoutingIntelligence intel, object? buildOrderManager = null)
    {
        _scoutingIntel = intel;
        _buildOrderManager = buildOrderManager;

        // Update target worker count based on bases and economy
        UpdateTargetWorkerCount(self);

        // Execute economy functions
        TrainWorkers(game, self);
        ManageSupply(game, self);
        ExpandIfNeeded(game, self);
        BuildRefineries(game, self);

        // Clear reservations at end of frame (they'll be re-reserved next frame)
        ClearReservations();
    }

    private void UpdateTargetWorkerCount(Player self)
    {
        var bases = self.GetCompletedBases();
        int baseCount = bases.Count;

        // Target 20 workers per base, capped at max
        _targetWorkerCount = Math.Min(
            baseCount * 20,
            GameConstants.MAX_WORKER_COUNT
        );

        // Minimum workers
        _targetWorkerCount = Math.Max(_targetWorkerCount, GameConstants.MIN_WORKER_COUNT);
    }

    public void TrainWorkers(Game game, Player self)
    {
        var currentWorkerCount = self.GetAllWorkers().Count;

        // Stop training if we have enough workers
        if (currentWorkerCount >= _targetWorkerCount)
            return;

        // Also train extra workers if we have excess minerals
        bool trainExtra = self.Minerals() > 200;
        if (!trainExtra && currentWorkerCount >= GameConstants.MIN_WORKER_COUNT)
            return;

        // Check if we have supply
        if (self.SupplyUsed() >= self.SupplyTotal())
            return;

        // Check if we have minerals
        if (GetAvailableMinerals(self) < 50)
            return;

        // Train from all completed bases
        var bases = self.GetCompletedBases();
        var race = self.GetRace();
        var workerType = race.GetWorker();

        foreach (var baseUnit in bases)
        {
            if (!baseUnit.IsTraining())
            {
                if (self.Minerals() >= 50)
                {
                    baseUnit.Train(workerType);
                    ReserveMinerals(50);
                    break; // Only train one per frame to avoid over-training
                }
            }
        }
    }

    public void ManageSupply(Game game, Player self)
    {
        var race = self.GetRace();
        int supplyRemaining = self.SupplyTotal() - self.SupplyUsed();

        // Don't build supply if we're at max
        if (self.SupplyTotal() >= GameConstants.MAX_SUPPLY)
            return;

        // Build supply when we're close to cap
        if (supplyRemaining <= GameConstants.SUPPLY_BUFFER)
        {
            var supplyProvider = race.GetSupplyProvider();
            int cost = supplyProvider.MineralPrice();

            if (GetAvailableMinerals(self) < cost)
                return;

            // Check if we're already building supply
            var allUnits = self.GetUnits();
            bool alreadyBuilding = allUnits.Any(u =>
                (u.GetUnitType() == supplyProvider && !u.IsCompleted()) ||
                (u.GetUnitType().IsWorker() && u.GetBuildType() == supplyProvider));

            if (alreadyBuilding)
                return;

            // Build supply
            if (race == Race.Zerg)
            {
                // Train overlord from larva
                var larva = allUnits.FirstOrDefault(u => u.GetUnitType() == UnitType.Zerg_Larva);
                if (larva != null && self.Minerals() >= 100)
                {
                    larva.Train(UnitType.Zerg_Overlord);
                    ReserveMinerals(100);
                }
            }
            else
            {
                // Terran/Protoss: Queue supply building (BuildManager will handle this)
                // For now, we'll handle it here directly
                QueueSupplyBuilding(game, self, supplyProvider);
            }
        }
    }

    private void QueueSupplyBuilding(Game game, Player self, UnitType supplyProvider)
    {
        // Get a worker to build
        var worker = self.GetIdleWorkers().FirstOrDefault();
        if (worker == null)
        {
            worker = self.GetMineralWorkers().FirstOrDefault();
        }

        if (worker == null)
            return;

        // Find build location
        var buildLocation = FindBuildLocationNearBase(game, self, supplyProvider);
        if (buildLocation != null)
        {
            worker.Build(supplyProvider, buildLocation.Value);
            ReserveMinerals(supplyProvider.MineralPrice());
        }
    }

    public void ExpandIfNeeded(Game game, Player self)
    {
        var race = self.GetRace();
        int expansionCost = race == Race.Zerg ?
            GameConstants.ZERG_EXPANSION_MINERAL_THRESHOLD :
            GameConstants.EXPANSION_MINERAL_THRESHOLD;

        // Check if we have enough minerals
        if (GetAvailableMinerals(self) < expansionCost)
            return;

        var resourceDepot = race.GetResourceDepot();

        // Check if we're already expanding
        var allUnits = self.GetUnits();
        bool alreadyExpanding = allUnits.Any(u =>
            (u.GetUnitType() == resourceDepot && !u.IsCompleted()) ||
            (u.GetUnitType().IsWorker() && u.GetBuildType() == resourceDepot));

        if (alreadyExpanding)
            return;

        // Find expansion location
        var myStart = self.GetStartLocation();
        var potentialExpansions = game.GetStartLocations()
            .Where(sl => sl.X != myStart.X || sl.Y != myStart.Y)
            .OrderBy(sl => Math.Sqrt(Math.Pow(sl.X - myStart.X, 2) + Math.Pow(sl.Y - myStart.Y, 2)))
            .ToList();

        foreach (var expLoc in potentialExpansions)
        {
            // Check if location is occupied by our base
            bool occupied = allUnits.Any(u =>
                u.GetUnitType().IsResourceDepot() &&
                Math.Abs(u.GetTilePosition().X - expLoc.X) < 10 &&
                Math.Abs(u.GetTilePosition().Y - expLoc.Y) < 10);

            if (!occupied)
            {
                if (game.CanBuildHere(expLoc, resourceDepot))
                {
                    // Get builder
                    var worker = self.GetIdleWorkers().FirstOrDefault();
                    if (worker == null)
                    {
                        worker = self.GetMineralWorkers().FirstOrDefault();
                    }

                    if (worker != null && self.Minerals() >= expansionCost)
                    {
                        worker.Build(resourceDepot, expLoc);
                        ReserveMinerals(expansionCost);
                        return;
                    }
                }
            }
        }
    }

    public void BuildRefineries(Game game, Player self)
    {
        var race = self.GetRace();
        var bases = self.GetCompletedBases();
        var refineries = self.GetRefineries();
        int currentRefineryCount = refineries.Count;

        // Update desired gas count based on bases and tech needs
        _desiredGasGeysers = Math.Min(bases.Count, GameConstants.MAX_GAS_GEYSERS);

        // Don't build if we have enough
        if (currentRefineryCount >= _desiredGasGeysers)
            return;

        // Check if we have minerals
        var refineryType = race.GetRefinery();
        if (GetAvailableMinerals(self) < 100)
            return;

        // Check if we're already building a refinery
        var allUnits = self.GetUnits();
        bool alreadyBuilding = allUnits.Any(u =>
            (u.GetUnitType() == refineryType && !u.IsCompleted()) ||
            (u.GetUnitType().IsWorker() && u.GetBuildType() == refineryType));

        if (alreadyBuilding)
            return;

        // Find a geyser near a completed base
        foreach (var baseUnit in bases)
        {
            var basePos = baseUnit.GetPosition();
            var nearbyGeysers = game.GetGeysers()
                .Where(g => g.GetDistance(basePos) < 400)
                .ToList();

            foreach (var geyser in nearbyGeysers)
            {
                // Check if geyser already has refinery
                bool hasRefinery = refineries.Any(r =>
                    r.GetDistance(geyser) < 100);

                if (!hasRefinery)
                {
                    // Get builder
                    var worker = self.GetIdleWorkers().FirstOrDefault();
                    if (worker == null)
                    {
                        worker = self.GetMineralWorkers().FirstOrDefault();
                    }

                    if (worker != null && self.Minerals() >= 100)
                    {
                        worker.Build(refineryType, geyser.GetTilePosition());
                        ReserveMinerals(100);
                        return;
                    }
                }
            }
        }
    }

    private TilePosition? FindBuildLocationNearBase(Game game, Player self, UnitType building)
    {
        var bases = self.GetCompletedBases();
        if (!bases.Any())
            return null;

        var mainBase = bases.First();
        var baseTile = mainBase.GetTilePosition();

        // Simple spiral search
        for (int radius = GameConstants.BUILD_LOCATION_SEARCH_RADIUS_MIN;
             radius < GameConstants.BUILD_LOCATION_SEARCH_RADIUS_MAX;
             radius++)
        {
            for (int i = 0; i < 8; i++)
            {
                int dx = (i % 3 - 1) * radius;
                int dy = (i / 3 - 1) * radius;
                var testPos = new TilePosition(baseTile.X + dx, baseTile.Y + dy);

                if (testPos.X >= 0 && testPos.Y >= 0 &&
                    testPos.X < game.MapWidth() && testPos.Y < game.MapHeight())
                {
                    if (game.CanBuildHere(testPos, building))
                    {
                        return testPos;
                    }
                }
            }
        }

        return null;
    }

    // Resource budgeting
    public int GetAvailableMinerals(Player self)
    {
        return Math.Max(0, self.Minerals() - _mineralReservations);
    }

    public int GetAvailableGas(Player self)
    {
        return Math.Max(0, self.Gas() - _gasReservations);
    }

    public void ReserveMinerals(int amount)
    {
        _mineralReservations += amount;
    }

    public void ReserveGas(int amount)
    {
        _gasReservations += amount;
    }

    public void ClearReservations()
    {
        _mineralReservations = 0;
        _gasReservations = 0;
    }

    // Query methods
    public int GetTargetWorkerCount()
    {
        return _targetWorkerCount;
    }

    public int GetDesiredGasGeysers()
    {
        return _desiredGasGeysers;
    }

    public bool WantsGas()
    {
        // Want gas if we're planning to build refineries
        return _desiredGasGeysers > 0;
    }
}
