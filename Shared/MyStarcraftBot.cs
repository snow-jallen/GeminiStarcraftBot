using BWAPI.NET;
using System.Linq;
using Shared.Managers;
using Shared.Intelligence;
using Shared.BuildOrders;
using Shared.Utils;

namespace Shared;

// library from https://www.nuget.org/packages/BWAPI.NET

public class MyStarcraftBot : DefaultBWListener
{
    private BWClient? _bwClient = null;
    public Game? Game => _bwClient?.Game;

    public bool IsRunning { get; private set; } = false;
    public bool InGame { get; private set; } = false;
    public int? GameSpeedToSet { get; set; } = null;

    // Manager instances
    private ScoutingIntelligence? _scoutingIntel;
    private ThreatAssessment? _threatAssessment;
    private MapAnalysis? _mapAnalysis;
    private BuildOrderManager? _buildOrderManager;
    private EconomyManager? _economyManager;
    private TechManager? _techManager;
    private BuildManager? _buildManager;
    private WorkerManager? _workerManager;
    private ArmyManager? _armyManager;

    // Scout tracking
    private int _scoutUnitId = -1;

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

        // Set game speed
        Game?.SetLocalSpeed(20);

        // Initialize all managers
        _scoutingIntel = new ScoutingIntelligence();
        _threatAssessment = new ThreatAssessment();
        _mapAnalysis = new MapAnalysis();
        _buildOrderManager = new BuildOrderManager();
        _economyManager = new EconomyManager();
        _techManager = new TechManager();
        _buildManager = new BuildManager();
        _workerManager = new WorkerManager();
        _armyManager = new ArmyManager();

        _scoutUnitId = -1;

        Console.WriteLine("Bot Started with Modular Architecture!");
        Console.WriteLine("=== GeminiAscendant v2.0 ===");
        Console.WriteLine("Born from Gemini, Elevated by Claude");
        Console.WriteLine("Features: Scouting, Tech Tree, Defense, Micro, Adaptive Builds");
    }

    public override void OnEnd(bool isWinner)
    {
        InGame = false;
        StatusChanged?.Invoke();
        Console.WriteLine(isWinner ? "We Won!" : "We Lost.");
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
            // ===== 1. INTELLIGENCE GATHERING =====
            _scoutingIntel!.Update(Game);
            _threatAssessment!.Update(Game, self, _scoutingIntel);
            _mapAnalysis!.Update(Game, self);

            // ===== 2. STRATEGIC PLANNING =====
            _buildOrderManager!.Update(Game, self, _scoutingIntel, _mapAnalysis);
            _economyManager!.Update(Game, self, _scoutingIntel, _buildOrderManager);
            _techManager!.Update(Game, self, _scoutingIntel, _buildOrderManager);

            // ===== 3. TACTICAL EXECUTION =====
            _buildManager!.Update(Game, self, _workerManager);
            _workerManager!.Update(Game, self, _threatAssessment, _economyManager);
            _armyManager!.Update(Game, self, _threatAssessment, _scoutingIntel, _techManager);

            // ===== 4. SUPPORT SYSTEMS =====
            ManageScout(self);
            DrawDebugInfo(self);
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR in OnFrame: {e.Message}");
            Console.WriteLine($"Stack trace: {e.StackTrace}");
            Game.DrawTextScreen(10, 200, $"ERROR: {e.Message}");
        }
    }

    private void ManageScout(Player self)
    {
        var scout = self.GetUnits().FirstOrDefault(u => u.GetID() == _scoutUnitId);

        // Assign new scout if needed
        if (scout == null || !scout.Exists())
        {
            var newScout = self.GetIdleWorkers().FirstOrDefault();
            if (newScout != null)
            {
                _scoutUnitId = newScout.GetID();
                _workerManager!.AssignScout(_scoutUnitId);
                scout = newScout;
            }
        }

        // Send scout to next location
        if (scout != null && Game != null)
        {
            var targetLocation = _scoutingIntel!.GetNextScoutLocation(Game);
            if (targetLocation != null)
            {
                var targetPos = new Position(targetLocation.Value.X * 32, targetLocation.Value.Y * 32);
                if (scout.GetDistance(targetPos) > 100)
                {
                    if (!scout.IsMoving() && !scout.IsGatheringMinerals())
                    {
                        scout.Move(targetPos);
                    }
                }
                else
                {
                    // Mark location as explored
                    _scoutingIntel.MarkLocationExplored(targetLocation.Value);
                }
            }
        }
    }

    private void DrawDebugInfo(Player self)
    {
        if (Game == null) return;

        var allUnits = self.GetUnits();
        var army = self.GetCombatUnits();

        // Basic stats
        Game.DrawTextScreen(10, 10, $"Supply: {self.SupplyUsed()/2} / {self.SupplyTotal()/2}");
        Game.DrawTextScreen(10, 20, $"Minerals: {self.Minerals()}  Gas: {self.Gas()}");
        Game.DrawTextScreen(10, 30, $"Workers: {allUnits.Count(u => u.GetUnitType().IsWorker())}");
        Game.DrawTextScreen(10, 40, $"Army: {army.Count} (Supply: {army.GetTotalSupplyUsed()/2})");
        Game.DrawTextScreen(10, 50, $"Bases: {self.GetCompletedBases().Count}");

        // Tech and strategy
        Game.DrawTextScreen(10, 70, $"Tech Tier: {_techManager!.CurrentTier}");
        Game.DrawTextScreen(10, 80, $"Army State: {_armyManager!.GetStateDescription()}");
        Game.DrawTextScreen(10, 90, $"Build Order: {_buildOrderManager!.GetCurrentBuildOrderName()}");
        Game.DrawTextScreen(10, 100, $"Opening: {(!_buildOrderManager.IsOpeningComplete() ? "Active" : "Complete")}");

        // Threat warnings
        if (_threatAssessment!.IsUnderAttack())
        {
            Game.DrawTextScreen(200, 10, "!!! UNDER ATTACK !!!");
            var threat = _threatAssessment.GetMostSeriousThreat();
            if (threat != null)
            {
                Game.DrawTextScreen(200, 20, $"Threat: {threat.Level} ({threat.ThreatSupply/2} supply)");
            }
        }

        // Enemy intel
        if (_scoutingIntel!.HasDiscoveredEnemy())
        {
            Game.DrawTextScreen(10, 120, $"Enemy Army: {_scoutingIntel.EnemyArmySupply/2} supply");
            Game.DrawTextScreen(10, 130, $"Enemy Bases: {_scoutingIntel.GetEnemyBases().Count}");

            if (_scoutingIntel.ObservedTechBuildings.Any())
            {
                Game.DrawTextScreen(10, 140, $"Enemy Tech: {_scoutingIntel.ObservedTechBuildings.Count} buildings");
            }
        }

        // Map info
        Game.DrawTextScreen(10, 160, $"Map: {_mapAnalysis!.GetMapSizeDescription()}");
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
