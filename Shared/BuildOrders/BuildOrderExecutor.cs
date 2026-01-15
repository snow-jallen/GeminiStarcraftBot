using BWAPI.NET;
using Shared.Managers;

namespace Shared.BuildOrders;

public class BuildOrderExecutor
{
    public bool ExecuteCurrentStep(Game game, Player self, BuildOrder buildOrder, BuildManager? buildManager = null)
    {
        if (buildOrder.CurrentStepIndex >= buildOrder.Steps.Count)
        {
            // Build order complete
            return true;
        }

        var step = buildOrder.Steps[buildOrder.CurrentStepIndex];

        // Check if we've reached the required supply for this step
        if (self.SupplyUsed() < step.Supply)
        {
            // Not ready for this step yet
            return false;
        }

        // Try to execute the step
        bool stepCompleted = ExecuteStep(game, self, step, buildManager);

        if (stepCompleted)
        {
            step.IsCompleted = true;
            buildOrder.CurrentStepIndex++;
        }

        return buildOrder.CurrentStepIndex >= buildOrder.Steps.Count;
    }

    private bool ExecuteStep(Game game, Player self, BuildStep step, BuildManager? buildManager)
    {
        if (step.BuildingToBuild != null)
        {
            return ExecuteBuildingStep(game, self, step, buildManager);
        }
        else if (step.UnitToTrain != null)
        {
            return ExecuteUnitStep(self, step);
        }

        // Unknown step type, mark as complete
        return true;
    }

    private bool ExecuteBuildingStep(Game game, Player self, BuildStep step, BuildManager? buildManager)
    {
        var building = step.BuildingToBuild!.Value;
        var currentCount = GetBuildingCount(self, building, includeIncomplete: true);

        // Check if we need to build more
        if (currentCount >= step.Count)
        {
            return true; // Step complete
        }

        // Check if already building
        if (IsAlreadyBuilding(self, building))
        {
            return false; // Wait for current build to finish
        }

        // Try to queue the building
        if (buildManager != null)
        {
            buildManager.QueueBuilding(building, priority: 100, currentFrame: game.GetFrameCount());
            return false; // Wait for building to be built
        }
        else
        {
            // Fallback: Try to build directly
            return TryBuildDirectly(game, self, building);
        }
    }

    private bool ExecuteUnitStep(Player self, BuildStep step)
    {
        var unit = step.UnitToTrain!.Value;
        var currentCount = GetUnitCount(self, unit);

        // Check if we have enough units
        if (currentCount >= step.Count)
        {
            return true; // Step complete
        }

        // Unit training is handled by ArmyManager
        // Just check if we have the required count
        return false;
    }

    private int GetBuildingCount(Player self, UnitType building, bool includeIncomplete)
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

    private int GetUnitCount(Player self, UnitType unit)
    {
        return self.GetUnits().Count(u => u.GetUnitType() == unit);
    }

    private bool IsAlreadyBuilding(Player self, UnitType building)
    {
        var units = self.GetUnits();
        return units.Any(u =>
            (u.GetUnitType() == building && !u.IsCompleted()) ||
            (u.GetUnitType().IsWorker() && u.GetBuildType() == building));
    }

    private bool TryBuildDirectly(Game game, Player self, UnitType building)
    {
        // Check resources
        if (self.Minerals() < building.MineralPrice() ||
            self.Gas() < building.GasPrice())
        {
            return false;
        }

        // Get a worker
        var worker = self.GetUnits()
            .Where(u => u.GetUnitType().IsWorker() && (u.IsIdle() || u.IsGatheringMinerals()))
            .FirstOrDefault();

        if (worker == null)
            return false;

        // Find build location
        var buildLocation = FindBuildLocation(game, self, building);
        if (buildLocation == null)
            return false;

        // Issue build command
        worker.Build(building, buildLocation.Value);
        return false; // Return false because building isn't complete yet
    }

    private TilePosition? FindBuildLocation(Game game, Player self, UnitType building)
    {
        var bases = self.GetUnits().Where(u => u.GetUnitType().IsResourceDepot()).ToList();
        if (!bases.Any())
            return null;

        var mainBase = bases.First();
        var baseTile = mainBase.GetTilePosition();

        // Simple spiral search
        for (int radius = 5; radius < 20; radius++)
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
}
