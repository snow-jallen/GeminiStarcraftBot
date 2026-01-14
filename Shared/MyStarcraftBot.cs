using BWAPI.NET;
using System.Linq;

namespace Shared;

// library from https://www.nuget.org/packages/BWAPI.NET

public class MyStarcraftBot : DefaultBWListener
{
    private BWClient? _bwClient = null;
    public Game? Game => _bwClient?.Game;

    public bool IsRunning { get; private set; } = false;
    public bool InGame { get; private set; } = false;
    public int? GameSpeedToSet { get; set; } = null;
    
    // Build Order Fields
    private bool _openingComplete = false;
    private int _scoutUnitId = -1; // Keep for future use or if we re-enable scouting later
    private bool _isAttacking = false;
    private int _currentScoutLocationIndex = 0;
    
    public event Action? StatusChanged;

    public void Connect()
    {
        _bwClient = new BWClient(this);
        var _ = Task.Run(() => _bwClient.StartGame());
        IsRunning = true;
        StatusChanged?.Invoke();
    }

    public void Disconnect()
    {
        if (_bwClient != null)
        {
            (_bwClient as IDisposable)?.Dispose();
        }
        _bwClient = null;
        IsRunning = false;
        InGame = false;
        StatusChanged?.Invoke();
    }

    // Bot Callbacks below
    public override void OnStart()
    {
        InGame = true;
        StatusChanged?.Invoke();
        Game?.EnableFlag(Flag.UserInput); // let human control too
        
        // Set game speed to match fastest
        Game?.SetLocalSpeed(20);
        _openingComplete = false;
        _scoutUnitId = -1;
        _currentScoutLocationIndex = 0;
        _isAttacking = false;
        Console.WriteLine("Bot Started!");
    }

    public override void OnEnd(bool isWinner)
    {
        InGame = false;
        StatusChanged?.Invoke();
        Console.WriteLine(isWinner ? "We Won!" : "We Lost.");
    }

    // Returns true if we are in the middle of an opening build order (and should block other "smart" logic)
    private bool ExecuteOpening(Player self)
    {
        if (_openingComplete) return false;

        var units = self.GetUnits();
        var race = self.GetRace();
        
        // Supply values are doubled in BWAPI (9 supply = 18)
        int supply = self.SupplyUsed();

        if (race == Race.Terran)
        {
            // 8 Depot (16 supply)
            if (supply >= 16 && units.Count(u => u.GetUnitType() == UnitType.Terran_Supply_Depot) < 1)
            {
                BuildStructure(self, UnitType.Terran_Supply_Depot);
                return true; // Blocking
            }
            // 10 Barracks (20 supply)
            if (supply >= 20 && units.Count(u => u.GetUnitType() == UnitType.Terran_Barracks) < 1)
            {
                BuildStructure(self, UnitType.Terran_Barracks);
                return true;
            }
            // 12 Barracks (24 supply)
            if (supply >= 24 && units.Count(u => u.GetUnitType() == UnitType.Terran_Barracks) < 2)
            {
                BuildStructure(self, UnitType.Terran_Barracks);
                return true;
            }
            // 14 Depot (28 supply)
            if (supply >= 28 && units.Count(u => u.GetUnitType() == UnitType.Terran_Supply_Depot) < 2)
            {
                BuildStructure(self, UnitType.Terran_Supply_Depot);
                return true;
            }
            // If we have 2 Barracks and 2 Depots, opening is done
            if (units.Count(u => u.GetUnitType() == UnitType.Terran_Barracks) >= 2 && 
                units.Count(u => u.GetUnitType() == UnitType.Terran_Supply_Depot) >= 2)
            {
                _openingComplete = true;
                return false;
            }
            return true; // Still in opening phase
        }
        else if (race == Race.Protoss)
        {
            // 8 Pylon (16 supply)
            if (supply >= 16 && units.Count(u => u.GetUnitType() == UnitType.Protoss_Pylon) < 1)
            {
                BuildStructure(self, UnitType.Protoss_Pylon);
                return true;
            }
            // 10 Gateway (20 supply)
            if (supply >= 20 && units.Count(u => u.GetUnitType() == UnitType.Protoss_Gateway) < 1)
            {
                BuildStructure(self, UnitType.Protoss_Gateway);
                return true;
            }
            // 12 Gateway (24 supply)
            if (supply >= 24 && units.Count(u => u.GetUnitType() == UnitType.Protoss_Gateway) < 2)
            {
                BuildStructure(self, UnitType.Protoss_Gateway);
                return true;
            }
            // 14 Pylon (28 supply)
            if (supply >= 28 && units.Count(u => u.GetUnitType() == UnitType.Protoss_Pylon) < 2)
            {
                BuildStructure(self, UnitType.Protoss_Pylon);
                return true;
            }
            
            if (units.Count(u => u.GetUnitType() == UnitType.Protoss_Gateway) >= 2 && 
                units.Count(u => u.GetUnitType() == UnitType.Protoss_Pylon) >= 2)
            {
                _openingComplete = true;
                return false;
            }
            return true;
        }
        else if (race == Race.Zerg)
        {
            // 9 Pool (18 supply)
            if (supply >= 18 && units.Count(u => u.GetUnitType() == UnitType.Zerg_Spawning_Pool) < 1)
            {
                BuildStructure(self, UnitType.Zerg_Spawning_Pool);
                return true;
            }
            // 9 Pool usually implies getting lings immediately, but simple completion check is fine.

            if (units.Count(u => u.GetUnitType() == UnitType.Zerg_Spawning_Pool) >= 1)
            {
                _openingComplete = true;
                return false;
            }
            return true;
        }

        _openingComplete = true; // Default fallback
        return false;
    }

    private void BuildStructure(Player self, UnitType building)
    {
        if (self.Minerals() < building.MineralPrice()) return;

        // Check if already building
        bool alreadyBuilding = self.GetUnits().Any(u => 
            (u.GetUnitType() == building && !u.IsCompleted()) || 
            (u.GetUnitType().IsWorker() && u.GetBuildType() == building));
            
        if (alreadyBuilding) return;

        var worker = self.GetUnits().FirstOrDefault(u => u.GetUnitType().IsWorker() && (u.IsIdle() || u.IsGatheringMinerals()));
        if (worker != null)
        {
             var buildLocation = GetBuildLocation(worker.GetTilePosition(), building);
             if (buildLocation != null)
             {
                 worker.Build(building, buildLocation.Value);
             }
        }
    }

    public override void OnFrame()
    {
        if (Game == null)
            return;
        
        if (GameSpeedToSet != null)
        {
            Game.SetLocalSpeed(GameSpeedToSet.Value);
            GameSpeedToSet = null;
        }
        
        var self = Game.Self();
        if (self == null) return;

        try
        {
            var allMyUnits = self.GetUnits();
            var army = allMyUnits.Where(u => !u.GetUnitType().IsWorker() && !u.GetUnitType().IsBuilding() && !u.GetUnitType().IsRefinery() && u.GetUnitType() != UnitType.Zerg_Overlord && u.GetUnitType() != UnitType.Zerg_Larva).ToList();
            int baseCount = allMyUnits.Count(u => u.GetUnitType().IsResourceDepot());
            
            // 0. Build Order Status
            bool isInOpening = ExecuteOpening(self);

            // Use methods for properties
            Game.DrawTextScreen(10, 10, $"Supply: {self.SupplyUsed()} / {self.SupplyTotal()}");
            Game.DrawTextScreen(10, 20, $"Minerals: {self.Minerals()}");
            Game.DrawTextScreen(10, 30, $"Workers: {allMyUnits.Count(u => u.GetUnitType().IsWorker())} (Idle: {allMyUnits.Count(u => u.GetUnitType().IsWorker() && u.IsIdle())})");
            Game.DrawTextScreen(10, 40, $"Army: {army.Count}");
            Game.DrawTextScreen(10, 50, $"Bases: {baseCount}");
            Game.DrawTextScreen(10, 80, $"Opening Active: {isInOpening}");
            
            // Scout Logic...
            var scout = allMyUnits.FirstOrDefault(u => u.GetID() == _scoutUnitId);
            if (scout == null || !scout.Exists())
            {
                var newScout = allMyUnits.FirstOrDefault(u => u.GetUnitType().IsWorker() && u.IsIdle());
                if (newScout != null)
                {
                    _scoutUnitId = newScout.GetID();
                    scout = newScout;
                }
            }

            if (scout != null)
            {
                var startLocations = Game.GetStartLocations();
                if (_currentScoutLocationIndex < startLocations.Count)
                {
                    var target = startLocations[_currentScoutLocationIndex];
                    Position targetPos = new Position(target.X * 32, target.Y * 32);
                    
                    Game.DrawTextScreen(10, 70, $"Scout Status: Moving to Location {_currentScoutLocationIndex + 1}/{startLocations.Count}");
                    
                    if (scout.GetDistance(targetPos) < 100)
                    {
                        _currentScoutLocationIndex = (_currentScoutLocationIndex + 1) % startLocations.Count;
                    }
                    else if (!scout.IsMoving())
                    {
                        scout.Move(targetPos);
                    }
                }
            }

            // 1. Worker Management
            foreach (var myUnit in allMyUnits)
            {
                if (myUnit.GetUnitType().IsWorker() && myUnit.IsIdle() && myUnit.GetID() != _scoutUnitId)
                {
                    var closestMineral = Game.GetMinerals()
                        .OrderBy(m => myUnit.GetDistance(m))
                        .FirstOrDefault();
                        
                    if (closestMineral != null)
                    {
                        myUnit.Gather(closestMineral);
                    }
                }
            }

            // 2. Train Workers
            // Don't train workers if we are in an opening that needs to save money for buildings
            // But for these openings, we usually want constant worker production UNLESS we are waiting for a specific building like Pool/Gateway
            // A simple heuristic: If in opening and Minerals < 150, stop worker production to ensure we hit the building timing.
            bool stopWorkersForOpening = isInOpening && self.Minerals() < 150;
            
            // Original throttle logic: < 20 workers or > 200 minerals
            if (!stopWorkersForOpening && self.Minerals() >= 50 && self.SupplyUsed() < self.SupplyTotal() && (allMyUnits.Count(u => u.GetUnitType().IsWorker()) < 20 || self.Minerals() > 200))
            {
                foreach (var myUnit in allMyUnits)
                {
                    if (myUnit.GetUnitType().IsResourceDepot() && !myUnit.IsTraining())
                    {
                        myUnit.Train(self.GetRace().GetWorker());
                    }
                }
            }

            // 3. Supply Management (SKIP if in opening - opening handles its own supply depots/pylons)
            if (!isInOpening && self.SupplyTotal() - self.SupplyUsed() <= 6 && self.SupplyTotal() < 400)
            {
                if (self.Minerals() >= 100)
                {
                    var supplyProvider = self.GetRace().GetSupplyProvider();
                    bool alreadyBuilding = allMyUnits.Any(u => 
                        (u.GetUnitType() == supplyProvider && !u.IsCompleted()) || 
                        (u.GetUnitType().IsWorker() && u.GetBuildType() == supplyProvider));

                    if (!alreadyBuilding)
                    {
                        if (self.GetRace() == Race.Zerg)
                        {
                            var larva = allMyUnits.FirstOrDefault(u => u.GetUnitType() == UnitType.Zerg_Larva);
                            if (larva != null) larva.Train(UnitType.Zerg_Overlord);
                        }
                        else
                        {
                            var worker = allMyUnits.FirstOrDefault(u => u.GetUnitType().IsWorker() && u.GetID() != _scoutUnitId && (u.IsIdle() || u.IsGatheringMinerals()));
                            if (worker != null)
                            {
                                var buildLocation = GetBuildLocation(worker.GetTilePosition(), supplyProvider);
                                if (buildLocation != null) worker.Build(supplyProvider, buildLocation.Value);
                            }
                        }
                    }
                }
            }

            // 4. Expansion (SKIP if in opening)
            if (!isInOpening)
            {
                int expansionCost = self.GetRace() == Race.Zerg ? 300 : 400;
                if (self.Minerals() >= expansionCost)
                {
                     var resourceDepot = self.GetRace().GetResourceDepot();
                     bool alreadyBuilding = allMyUnits.Any(u => 
                        (u.GetUnitType() == resourceDepot && !u.IsCompleted()) || 
                        (u.GetUnitType().IsWorker() && u.GetBuildType() == resourceDepot));

                     if (!alreadyBuilding)
                     {
                         var myStart = self.GetStartLocation();
                         var potentialExpansions = Game.GetStartLocations()
                             .Where(sl => sl.X != myStart.X || sl.Y != myStart.Y)
                             .OrderBy(sl => Math.Sqrt(Math.Pow(sl.X - myStart.X, 2) + Math.Pow(sl.Y - myStart.Y, 2)))
                             .ToList();

                         foreach (var exp in potentialExpansions)
                         {
                             bool occupied = allMyUnits.Any(u => u.GetUnitType().IsResourceDepot() && 
                                Math.Abs(u.GetTilePosition().X - exp.X) < 10 && 
                                Math.Abs(u.GetTilePosition().Y - exp.Y) < 10);
                             
                             if (!occupied)
                             {
                                 if (Game.CanBuildHere(exp, resourceDepot))
                                 {
                                     var worker = allMyUnits.FirstOrDefault(u => u.GetUnitType().IsWorker() && u.GetID() != _scoutUnitId && (u.IsIdle() || u.IsGatheringMinerals()));
                                     if (worker != null)
                                     {
                                         worker.Build(resourceDepot, exp);
                                         break; 
                                     }
                                 }
                             }
                         }
                     }
                }
            }

            // 5. Build Army Production Buildings
            UnitType productionBuilding = GetBasicProductionBuilding(self.GetRace());
            if (!isInOpening && (baseCount >= 2 || self.Minerals() > 500) && productionBuilding != UnitType.None && self.Minerals() >= 150) 
            {
                 int count = allMyUnits.Count(u => u.GetUnitType() == productionBuilding);
                 if (count < 5)
                 {
                     bool alreadyBuilding = allMyUnits.Any(u => 
                        (u.GetUnitType() == productionBuilding && !u.IsCompleted()) || 
                        (u.GetUnitType().IsWorker() && u.GetBuildType() == productionBuilding));
                     
                     if (!alreadyBuilding)
                     {
                          var worker = allMyUnits.FirstOrDefault(u => u.GetUnitType().IsWorker() && u.GetID() != _scoutUnitId && (u.IsIdle() || u.IsGatheringMinerals()));
                          if (worker != null)
                          {
                              var buildLocation = GetBuildLocation(worker.GetTilePosition(), productionBuilding);
                              if (buildLocation != null) worker.Build(productionBuilding, buildLocation.Value);
                          }
                     }
                 }
            }

            // 6. Train Army
            UnitType basicUnit = GetBasicCombatUnit(self.GetRace());
            bool allowedToTrainArmy = !isInOpening || (self.GetRace() == Race.Zerg); 
            if (isInOpening && self.GetRace() != Race.Zerg)
            {
                 if (self.Minerals() > 200) allowedToTrainArmy = true;
            }

            if (allowedToTrainArmy && ((baseCount >= 2 || self.Minerals() > 500) || isInOpening) && basicUnit != UnitType.None && self.Minerals() >= basicUnit.MineralPrice() && self.SupplyUsed() < self.SupplyTotal())
            {
                if (self.GetRace() == Race.Zerg)
                {
                    var larva = allMyUnits.FirstOrDefault(u => u.GetUnitType() == UnitType.Zerg_Larva);
                     bool hasPool = allMyUnits.Any(u => u.GetUnitType() == UnitType.Zerg_Spawning_Pool && u.IsCompleted());
                     if (larva != null && hasPool) larva.Train(basicUnit);
                }
                else
                {
                    foreach (var building in allMyUnits)
                    {
                        if (building.GetUnitType() == productionBuilding && !building.IsTraining() && building.IsCompleted())
                        {
                            building.Train(basicUnit);
                        }
                    }
                }
            }

            // 7. Improved Attack Logic
            var startLoc = self.GetStartLocation();
            var rallyPoint = new Position(startLoc.X * 32, startLoc.Y * 32); 

            // State Transition
            if (army.Count >= 20) _isAttacking = true;
            if (army.Count < 10) _isAttacking = false;

            if (_isAttacking)
            {
                Game.DrawTextScreen(10, 60, "Status: HUNTING / ATTACKING");
                
                Position attackTarget = new Position(0,0); // Default invalid
                bool targetFound = false;

                // Priority 1: Visible Enemy Units
                var visibleEnemies = Game.Enemy().GetUnits().Where(u => u.IsVisible() && u.GetHitPoints() > 0).ToList();
                if (visibleEnemies.Count > 0)
                {
                    // Target the closest enemy to our first army unit
                    if (army.Count > 0)
                    {
                        var leader = army[0];
                        var closest = visibleEnemies.OrderBy(e => leader.GetDistance(e)).First();
                        attackTarget = closest.GetPosition();
                        targetFound = true;
                    }
                }
                
                // Priority 2: Hunt Enemy Bases
                if (!targetFound)
                {
                    var enemyLocs = Game.GetStartLocations().Where(l => l != startLoc).ToList();
                    if (enemyLocs.Count > 0)
                    {
                        // Cycle through locations over time (change every ~80 seconds / 2000 frames)
                        int index = (Game.GetFrameCount() / 2000) % enemyLocs.Count;
                        var loc = enemyLocs[index];
                        attackTarget = new Position(loc.X * 32, loc.Y * 32);
                        targetFound = true;
                    }
                }

                if (targetFound)
                {
                    foreach (var unit in army)
                    {
                        // Spam command periodically to keep them moving/re-targeting
                        // Or if they are idle
                        if (unit.IsIdle() || (Game.GetFrameCount() % 50 == 0))
                        {
                            unit.Attack(attackTarget);
                        }
                    }
                }
            }
            else
            {
                // Rally at home
                Game.DrawTextScreen(10, 60, $"Status: Rallying (Size: {army.Count}/20)");
                foreach (var unit in army)
                {
                    if (unit.GetDistance(rallyPoint) > 300) 
                    {
                        // Use Attack move to rally point so they fight if intercepted
                        if (!unit.IsMoving() && !unit.IsAttacking())
                        {
                             unit.Attack(rallyPoint); 
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Game.DrawTextScreen(10, 200, $"ERROR: {e.Message}");
        }
    }
    
    // Helper to find build location
    private TilePosition? GetBuildLocation(TilePosition near, UnitType building)
    {
        // Simple spiral search or random search nearby
        // Game.GetBuildLocation is not always available in basic wrappers, so we implement a simple search
        // Check 20x20 area around 'near'
        
        bool[,] map = new bool[100,100]; // Visited
        
        for (int r = 1; r < 20; r++)
        {
             // Try some random spots at distance r
             for(int i=0; i<10; i++)
             {
                 int dx = new Random().Next(-r, r);
                 int dy = new Random().Next(-r, r);
                 TilePosition target = new TilePosition(near.X + dx, near.Y + dy);
                 
                 // Verify bounds (ignoring map size for brevity, but assume positive)
                 if (target.X < 0 || target.Y < 0) continue;

                 if (Game.CanBuildHere(target, building))
                 {
                     return target;
                 }
             }
        }
        return null;
    }

    private UnitType GetBasicProductionBuilding(Race race)
    {
        if (race == Race.Terran) return UnitType.Terran_Barracks;
        if (race == Race.Protoss) return UnitType.Protoss_Gateway;
        if (race == Race.Zerg) return UnitType.Zerg_Spawning_Pool;
        return UnitType.None;
    }

    private UnitType GetBasicCombatUnit(Race race)
    {
        if (race == Race.Terran) return UnitType.Terran_Marine;
        if (race == Race.Protoss) return UnitType.Protoss_Zealot;
        if (race == Race.Zerg) return UnitType.Zerg_Zergling;
        return UnitType.None;
    }

    public override void OnUnitComplete(Unit unit) { }
    public override void OnUnitDestroy(Unit unit) { }
    public override void OnUnitMorph(Unit unit) { }
    public override void OnSendText(string text) { }
    public override void OnReceiveText(Player player, string text) { }
    public override void OnPlayerLeft(Player player) { }
    public override void OnNukeDetect(Position target) { }
    public override void OnUnitEvade(Unit unit) { }
    public override void OnUnitShow(Unit unit) { }
    public override void OnUnitHide(Unit unit) { }
    public override void OnUnitCreate(Unit unit) { }
    public override void OnUnitRenegade(Unit unit) { }
    public override void OnSaveGame(string gameName) { }
    public override void OnUnitDiscover(Unit unit) { }
}
