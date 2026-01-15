using BWAPI.NET;
using Shared.Utils;
using Shared.Intelligence;

namespace Shared.Managers;

public class WorkerManager
{
    private Dictionary<int, int> _workerAssignments = new(); // workerId -> baseId
    private HashSet<int> _gasWorkers = new();
    private HashSet<int> _builders = new();
    private HashSet<int> _scouts = new();
    private int _lastBalanceFrame = 0;
    private bool _defendingBase = false;

    public void Update(Game game, Player self, ThreatAssessment threats, EconomyManager economy)
    {
        // Assign idle workers
        AssignIdleWorkers(game, self);

        // Balance workers periodically
        if (game.GetFrameCount() - _lastBalanceFrame > GameConstants.WORKER_BALANCE_INTERVAL)
        {
            BalanceWorkers(game, self);
            AssignGasWorkers(game, self);
            _lastBalanceFrame = game.GetFrameCount();
        }

        // Handle defense
        if (threats.IsUnderAttack())
        {
            if (threats.ShouldPullWorkers())
            {
                var defensePos = threats.GetDefensePosition();
                if (defensePos != null)
                {
                    DefendBase(game, self, defensePos.Value);
                }
            }
        }
        else
        {
            // Resume normal mining if we were defending
            if (_defendingBase)
            {
                ResumeNormalMining(game, self);
                _defendingBase = false;
            }
        }
    }

    private void AssignIdleWorkers(Game game, Player self)
    {
        var idleWorkers = self.GetIdleWorkers();

        foreach (var worker in idleWorkers)
        {
            int workerId = worker.GetID();

            // Skip scouts and builders
            if (_scouts.Contains(workerId) || _builders.Contains(workerId))
                continue;

            // Find closest mineral
            var closestMineral = game.GetMinerals()
                .OrderBy(m => worker.GetDistance(m))
                .FirstOrDefault();

            if (closestMineral != null)
            {
                worker.Gather(closestMineral);
            }
        }
    }

    private void BalanceWorkers(Game game, Player self)
    {
        var bases = self.GetCompletedBases();
        if (bases.Count < 2)
            return; // No need to balance with only one base

        var workers = self.GetAllWorkers();

        // Calculate workers per base
        var workersPerBase = new Dictionary<int, int>();
        foreach (var baseUnit in bases)
        {
            int baseId = baseUnit.GetID();
            int workerCount = workers.Count(w => w.GetDistance(baseUnit) < 400);
            workersPerBase[baseId] = workerCount;
        }

        // Find oversaturated and undersaturated bases
        int optimalPerBase = GameConstants.OPTIMAL_WORKERS_PER_BASE;

        var oversaturated = workersPerBase.Where(kvp => kvp.Value > optimalPerBase + 4).ToList();
        var undersaturated = workersPerBase.Where(kvp => kvp.Value < optimalPerBase - 4).ToList();

        if (!oversaturated.Any() || !undersaturated.Any())
            return;

        // Transfer workers from oversaturated to undersaturated bases
        var sourceBase = bases.FirstOrDefault(b => b.GetID() == oversaturated.First().Key);
        var targetBase = bases.FirstOrDefault(b => b.GetID() == undersaturated.First().Key);

        if (sourceBase != null && targetBase != null)
        {
            // Find a worker to transfer
            var workerToTransfer = workers
                .Where(w => w.GetDistance(sourceBase) < 400 && w.IsGatheringMinerals())
                .FirstOrDefault();

            if (workerToTransfer != null)
            {
                // Send to target base
                var closestMineral = game.GetMinerals()
                    .Where(m => m.GetDistance(targetBase) < 300)
                    .OrderBy(m => workerToTransfer.GetDistance(m))
                    .FirstOrDefault();

                if (closestMineral != null)
                {
                    workerToTransfer.Gather(closestMineral);
                }
            }
        }
    }

    private void AssignGasWorkers(Game game, Player self)
    {
        var refineries = self.GetCompletedRefineries();

        _gasWorkers.Clear(); // Rebuild gas worker set

        foreach (var refinery in refineries)
        {
            // Count workers currently on this refinery
            var workersOnGas = self.GetGasWorkers()
                .Where(w => w.GetDistance(refinery) < 100)
                .ToList();

            int currentCount = workersOnGas.Count;

            // Assign more workers if needed
            if (currentCount < GameConstants.WORKERS_PER_GAS)
            {
                int needed = GameConstants.WORKERS_PER_GAS - currentCount;

                // Find nearby idle or mineral workers
                var nearbyWorkers = self.GetUnits()
                    .Where(u => u.GetUnitType().IsWorker() &&
                               (u.IsIdle() || u.IsGatheringMinerals()) &&
                               u.GetDistance(refinery) < 400)
                    .OrderBy(w => w.GetDistance(refinery))
                    .Take(needed)
                    .ToList();

                foreach (var worker in nearbyWorkers)
                {
                    worker.Gather(refinery);
                    _gasWorkers.Add(worker.GetID());
                }
            }

            // Track all gas workers
            foreach (var worker in workersOnGas)
            {
                _gasWorkers.Add(worker.GetID());
            }
        }
    }

    private void DefendBase(Game game, Player self, Position threatPosition)
    {
        _defendingBase = true;

        // Pull up to 12 mineral workers to defend
        var mineralWorkers = self.GetMineralWorkers()
            .Where(w => !_scouts.Contains(w.GetID()) && !_builders.Contains(w.GetID()))
            .OrderBy(w => w.GetDistance(threatPosition))
            .Take(GameConstants.DEFEND_WORKER_COUNT)
            .ToList();

        foreach (var worker in mineralWorkers)
        {
            worker.Attack(threatPosition);
        }
    }

    private void ResumeNormalMining(Game game, Player self)
    {
        // Send all workers back to mining
        var allWorkers = self.GetAllWorkers();

        foreach (var worker in allWorkers)
        {
            int workerId = worker.GetID();

            // Skip scouts, builders, and gas workers
            if (_scouts.Contains(workerId) || _builders.Contains(workerId) || _gasWorkers.Contains(workerId))
                continue;

            // If worker is idle or not gathering, send to minerals
            if (worker.IsIdle() || worker.IsAttacking())
            {
                var closestMineral = game.GetMinerals()
                    .OrderBy(m => worker.GetDistance(m))
                    .FirstOrDefault();

                if (closestMineral != null)
                {
                    worker.Gather(closestMineral);
                }
            }
        }
    }

    // Public methods for other managers to use
    public Unit? GetAvailableBuilder(Game game, Player self)
    {
        // Prefer idle workers
        var idleWorker = self.GetIdleWorkers()
            .Where(w => !_scouts.Contains(w.GetID()))
            .FirstOrDefault();

        if (idleWorker != null)
        {
            _builders.Add(idleWorker.GetID());
            return idleWorker;
        }

        // Use mineral worker
        var mineralWorker = self.GetMineralWorkers()
            .Where(w => !_scouts.Contains(w.GetID()) && !_gasWorkers.Contains(w.GetID()))
            .FirstOrDefault();

        if (mineralWorker != null)
        {
            _builders.Add(mineralWorker.GetID());
            return mineralWorker;
        }

        return null;
    }

    public void AssignScout(int workerUnitId)
    {
        _scouts.Add(workerUnitId);
    }

    public void RemoveScout(int workerUnitId)
    {
        _scouts.Remove(workerUnitId);
    }

    public void MarkBuilderComplete(int workerUnitId)
    {
        _builders.Remove(workerUnitId);
    }

    public bool IsScout(int workerUnitId)
    {
        return _scouts.Contains(workerUnitId);
    }

    public bool IsBuilder(int workerUnitId)
    {
        return _builders.Contains(workerUnitId);
    }

    public int GetGasWorkerCount()
    {
        return _gasWorkers.Count;
    }

    public int GetMineralWorkerCount(Player self)
    {
        return self.GetMineralWorkers().Count(w =>
            !_scouts.Contains(w.GetID()) &&
            !_builders.Contains(w.GetID()));
    }
}
