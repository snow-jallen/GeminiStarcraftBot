using BWAPI.NET;
using Shared.Intelligence;

namespace Shared.BuildOrders;

public class BuildOrderManager
{
    private BuildOrder? _currentBuildOrder;
    private BuildOrderExecutor _executor = new();
    private bool _buildOrderComplete = false;
    private bool _hasSelectedBuildOrder = false;

    public void Update(Game game, Player self, ScoutingIntelligence intel, MapAnalysis map)
    {
        // Select build order on first update
        if (!_hasSelectedBuildOrder)
        {
            SelectBuildOrder(game, self, intel, map);
            _hasSelectedBuildOrder = true;
        }

        // Execute current build order if not complete
        if (!_buildOrderComplete && _currentBuildOrder != null)
        {
            _buildOrderComplete = _executor.ExecuteCurrentStep(game, self, _currentBuildOrder);
        }

        // Adapt build order if needed
        if (!_buildOrderComplete)
        {
            CheckForAdaptation(game, self, intel);
        }
    }

    private void SelectBuildOrder(Game game, Player self, ScoutingIntelligence intel, MapAnalysis map)
    {
        var race = self.GetRace();

        if (race == Race.Terran)
        {
            _currentBuildOrder = SelectTerranBuild(game, intel, map);
        }
        else if (race == Race.Protoss)
        {
            _currentBuildOrder = SelectProtossBuild(game, intel, map);
        }
        else if (race == Race.Zerg)
        {
            _currentBuildOrder = SelectZergBuild(game, intel, map);
        }
        else
        {
            // Fallback
            _currentBuildOrder = BuildOrderDefinitions.GetBuildOrderForRace(race);
        }

        Console.WriteLine($"Selected build order: {_currentBuildOrder.Name}");
    }

    private BuildOrder SelectTerranBuild(Game game, ScoutingIntelligence intel, MapAnalysis map)
    {
        // If enemy is fast expanding, punish with 2Rax pressure
        if (intel.EnemyIsExpanding(game.GetFrameCount()) && game.GetFrameCount() < 7200)
        {
            return BuildOrderDefinitions.GetTerran2RaxPressure();
        }

        // If small map, be aggressive
        if (map.IsSmallMap())
        {
            return BuildOrderDefinitions.GetTerran2RaxPressure();
        }

        // Default: 1 Factory Expand
        return BuildOrderDefinitions.GetTerran1FactoryExpand();
    }

    private BuildOrder SelectProtossBuild(Game game, ScoutingIntelligence intel, MapAnalysis map)
    {
        // If small map, be aggressive
        if (map.IsSmallMap())
        {
            return BuildOrderDefinitions.GetProtoss2GateZealot();
        }

        // If we detect early aggression, defensive build
        if (intel.EnemyDoingEarlyAggression())
        {
            return BuildOrderDefinitions.GetProtoss2GateZealot();
        }

        // Default: Fast Expand
        return BuildOrderDefinitions.GetProtossFastExpand();
    }

    private BuildOrder SelectZergBuild(Game game, ScoutingIntelligence intel, MapAnalysis map)
    {
        // If enemy is fast expanding, match with 12 hatch
        if (intel.EnemyIsExpanding(game.GetFrameCount()))
        {
            return BuildOrderDefinitions.GetZerg12Hatch();
        }

        // If small map or early aggression detected, 9 pool
        if (map.IsSmallMap() || intel.EnemyDoingEarlyAggression())
        {
            return BuildOrderDefinitions.GetZerg9Pool();
        }

        // Default: 12 Hatch
        return BuildOrderDefinitions.GetZerg12Hatch();
    }

    private void CheckForAdaptation(Game game, Player self, ScoutingIntelligence intel)
    {
        // Don't adapt if we're past supply 50 (opening is over)
        if (self.SupplyUsed() > 100) // 50 displayed supply
        {
            _buildOrderComplete = true;
            return;
        }

        // Check for enemy cheese/all-in
        if (intel.EnemyDoingEarlyAggression() && !IsDefensiveBuild())
        {
            // Switch to defensive build
            AdaptToDefensiveBuild(self.GetRace());
        }
    }

    private bool IsDefensiveBuild()
    {
        if (_currentBuildOrder == null)
            return false;

        // Consider these as defensive builds
        return _currentBuildOrder.Type == BuildOrderType.Terran_2RaxPressure ||
               _currentBuildOrder.Type == BuildOrderType.Protoss_2GateZealot ||
               _currentBuildOrder.Type == BuildOrderType.Zerg_9Pool;
    }

    private void AdaptToDefensiveBuild(Race race)
    {
        Console.WriteLine("Adapting to defensive build!");

        if (race == Race.Terran)
        {
            _currentBuildOrder = BuildOrderDefinitions.GetTerran2RaxPressure();
        }
        else if (race == Race.Protoss)
        {
            _currentBuildOrder = BuildOrderDefinitions.GetProtoss2GateZealot();
        }
        else if (race == Race.Zerg)
        {
            _currentBuildOrder = BuildOrderDefinitions.GetZerg9Pool();
        }

        // Reset step index to current supply
        // This prevents trying to re-execute early steps
        _buildOrderComplete = false;
    }

    // Query methods
    public bool IsOpeningComplete()
    {
        return _buildOrderComplete;
    }

    public string GetCurrentBuildOrderName()
    {
        return _currentBuildOrder?.Name ?? "None";
    }

    public BuildOrderType? GetCurrentBuildOrderType()
    {
        return _currentBuildOrder?.Type;
    }

    public int GetCurrentStepIndex()
    {
        return _currentBuildOrder?.CurrentStepIndex ?? 0;
    }

    public int GetTotalSteps()
    {
        return _currentBuildOrder?.Steps.Count ?? 0;
    }

    public bool WantsGas()
    {
        // Check if current build order includes gas buildings
        if (_currentBuildOrder == null)
            return false;

        return _currentBuildOrder.Steps.Any(s =>
            s.BuildingToBuild == UnitType.Terran_Refinery ||
            s.BuildingToBuild == UnitType.Protoss_Assimilator ||
            s.BuildingToBuild == UnitType.Zerg_Extractor);
    }
}
