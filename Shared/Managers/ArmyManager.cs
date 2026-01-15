using BWAPI.NET;
using Shared.Utils;
using Shared.Intelligence;

namespace Shared.Managers;

public enum ArmyState
{
    Rallying,
    Defending,
    Attacking,
    Retreating
}

public class ArmyManager
{
    public ArmyState CurrentState { get; private set; } = ArmyState.Rallying;
    private Position _rallyPoint;
    private Position? _attackTarget;
    private Dictionary<int, int> _unitLastMicroFrame = new();
    private Dictionary<int, int> _unitLastAttackFrame = new();

    public void Update(Game game, Player self, ThreatAssessment threats, ScoutingIntelligence intel, TechManager tech)
    {
        var army = self.GetCombatUnits();

        // Update rally point
        UpdateRallyPoint(self);

        // Train units
        TrainUnits(game, self, tech);

        // Update army state
        UpdateArmyState(game, self, army, threats, intel);

        // Command army based on state
        CommandArmy(game, self, army, threats, intel);
    }

    private void UpdateRallyPoint(Player self)
    {
        var mainBase = self.GetCompletedBases().FirstOrDefault();
        if (mainBase != null)
        {
            var baseTile = mainBase.GetTilePosition();
            _rallyPoint = new Position(baseTile.X * 32, baseTile.Y * 32);
        }
    }

    private void TrainUnits(Game game, Player self, TechManager tech)
    {
        var race = self.GetRace();

        // Check resources and supply
        if (self.SupplyUsed() >= self.SupplyTotal())
            return;

        // Get preferred combat unit for current tech tier
        var unitToTrain = tech.GetPreferredCombatUnit(race, null!);

        if (unitToTrain == UnitType.None)
            return;

        // Check if we have required tech
        if (!tech.HasRequiredTech(unitToTrain, self))
            return;

        // Check resources
        if (self.Minerals() < unitToTrain.MineralPrice() ||
            self.Gas() < unitToTrain.GasPrice())
            return;

        // Train based on race
        if (race == Race.Zerg)
        {
            TrainZergUnit(self, unitToTrain);
        }
        else
        {
            TrainNonZergUnit(self, unitToTrain);
        }
    }

    private void TrainZergUnit(Player self, UnitType unit)
    {
        // Find a larva
        var larva = self.GetUnits()
            .FirstOrDefault(u => u.GetUnitType() == UnitType.Zerg_Larva);

        if (larva != null)
        {
            larva.Train(unit);
        }
    }

    private void TrainNonZergUnit(Player self, UnitType unit)
    {
        // Find production building for this unit
        var productionBuildings = self.GetProductionBuildings(self.GetRace());

        foreach (var building in productionBuildings)
        {
            if (!building.IsTraining() && building.IsCompleted())
            {
                // Check if this building can train this unit
                if (CanBuildingTrainUnit(building.GetUnitType(), unit))
                {
                    building.Train(unit);
                    return; // Only train one per frame
                }
            }
        }
    }

    private bool CanBuildingTrainUnit(UnitType building, UnitType unit)
    {
        // Terran
        if (building == UnitType.Terran_Barracks)
            return unit == UnitType.Terran_Marine || unit == UnitType.Terran_Firebat || unit == UnitType.Terran_Medic;

        if (building == UnitType.Terran_Factory)
            return unit == UnitType.Terran_Vulture || unit == UnitType.Terran_Siege_Tank_Tank_Mode || unit == UnitType.Terran_Goliath;

        if (building == UnitType.Terran_Starport)
            return unit == UnitType.Terran_Wraith || unit == UnitType.Terran_Dropship;

        // Protoss
        if (building == UnitType.Protoss_Gateway)
            return unit == UnitType.Protoss_Zealot || unit == UnitType.Protoss_Dragoon || unit == UnitType.Protoss_High_Templar || unit == UnitType.Protoss_Dark_Templar;

        if (building == UnitType.Protoss_Robotics_Facility)
            return unit == UnitType.Protoss_Observer || unit == UnitType.Protoss_Reaver;

        if (building == UnitType.Protoss_Stargate)
            return unit == UnitType.Protoss_Scout || unit == UnitType.Protoss_Carrier;

        return false;
    }

    private void UpdateArmyState(Game game, Player self, List<Unit> army, ThreatAssessment threats, ScoutingIntelligence intel)
    {
        int armySupply = army.GetTotalSupplyUsed();

        // State transitions
        switch (CurrentState)
        {
            case ArmyState.Rallying:
                // Transition to attacking if we have enough units
                if (armySupply >= GameConstants.ATTACK_ARMY_SUPPLY && !threats.IsUnderAttack())
                {
                    CurrentState = ArmyState.Attacking;
                }
                // Transition to defending if under attack
                else if (threats.IsUnderAttack())
                {
                    CurrentState = ArmyState.Defending;
                }
                break;

            case ArmyState.Attacking:
                // Transition to defending if under attack
                if (threats.IsUnderAttack())
                {
                    CurrentState = ArmyState.Defending;
                }
                // Transition to retreating if army too small
                else if (armySupply < GameConstants.RETREAT_ARMY_SIZE * 2)
                {
                    CurrentState = ArmyState.Retreating;
                }
                break;

            case ArmyState.Defending:
                // Transition back to rallying if threat cleared
                if (!threats.IsUnderAttack())
                {
                    if (armySupply < GameConstants.RALLY_ARMY_SIZE)
                    {
                        CurrentState = ArmyState.Rallying;
                    }
                    else
                    {
                        CurrentState = ArmyState.Attacking;
                    }
                }
                break;

            case ArmyState.Retreating:
                // Transition to rallying when back at base
                if (army.All(u => u.GetDistance(_rallyPoint) < GameConstants.RALLY_POINT_DISTANCE))
                {
                    CurrentState = ArmyState.Rallying;
                }
                break;
        }
    }

    private void CommandArmy(Game game, Player self, List<Unit> army, ThreatAssessment threats, ScoutingIntelligence intel)
    {
        switch (CurrentState)
        {
            case ArmyState.Rallying:
                CommandRally(game, army);
                break;

            case ArmyState.Defending:
                CommandDefend(game, army, threats);
                break;

            case ArmyState.Attacking:
                CommandAttack(game, self, army, intel);
                break;

            case ArmyState.Retreating:
                CommandRetreat(game, army);
                break;
        }
    }

    private void CommandRally(Game game, List<Unit> army)
    {
        foreach (var unit in army)
        {
            if (unit.GetDistance(_rallyPoint) > GameConstants.RALLY_POINT_DISTANCE)
            {
                if (!unit.IsMoving() && !unit.IsAttacking())
                {
                    unit.Attack(_rallyPoint); // Use attack-move to rally
                }
            }
        }
    }

    private void CommandDefend(Game game, List<Unit> army, ThreatAssessment threats)
    {
        var defensePos = threats.GetDefensePosition();
        if (defensePos == null)
            return;

        // Get visible enemies
        var enemy = game.Enemy();
        if (enemy == null)
            return;

        var visibleEnemies = enemy.GetUnits()
            .Where(u => u.IsVisible() && !u.GetUnitType().IsBuilding())
            .ToList();

        if (visibleEnemies.Any())
        {
            ExecuteMicro(game, army, visibleEnemies);
        }
        else
        {
            // Move to defense position
            foreach (var unit in army)
            {
                if (unit.GetDistance(defensePos.Value) > 200)
                {
                    unit.Attack(defensePos.Value);
                }
            }
        }
    }

    private void CommandAttack(Game game, Player self, List<Unit> army, ScoutingIntelligence intel)
    {
        var enemy = game.Enemy();
        if (enemy == null)
            return;

        // Priority 1: Attack visible enemies
        var visibleEnemies = enemy.GetUnits()
            .Where(u => u.IsVisible() && u.GetHitPoints() > 0)
            .ToList();

        if (visibleEnemies.Any())
        {
            ExecuteMicro(game, army, visibleEnemies);
            return;
        }

        // Priority 2: Attack known enemy base
        var enemyBase = intel.GetEnemyMainBase();
        if (enemyBase != null)
        {
            _attackTarget = new Position(enemyBase.Location.X * 32, enemyBase.Location.Y * 32);
        }
        else
        {
            // Priority 3: Explore enemy start locations
            var enemyStart = game.GetStartLocations()
                .Where(loc => loc != self.GetStartLocation())
                .OrderBy(loc => (game.GetFrameCount() / 2000) % game.GetStartLocations().Count)
                .FirstOrDefault();

            if (enemyStart != null)
            {
                _attackTarget = new Position(enemyStart.X * 32, enemyStart.Y * 32);
            }
        }

        // Move army to attack target
        if (_attackTarget != null)
        {
            foreach (var unit in army)
            {
                if (unit.IsIdle() || (game.GetFrameCount() % GameConstants.ATTACK_COMMAND_SPAM_INTERVAL == 0))
                {
                    unit.Attack(_attackTarget.Value);
                }
            }
        }
    }

    private void CommandRetreat(Game game, List<Unit> army)
    {
        foreach (var unit in army)
        {
            unit.Move(_rallyPoint);
        }
    }

    // ===== COMBAT MICRO =====

    private void ExecuteMicro(Game game, List<Unit> army, List<Unit> enemies)
    {
        // Separate ranged and melee units
        var rangedUnits = army.Where(u => IsRangedUnit(u)).ToList();
        var meleeUnits = army.Where(u => !IsRangedUnit(u)).ToList();

        // Execute kiting for ranged units
        foreach (var unit in rangedUnits)
        {
            ExecuteKiting(game, unit, enemies);
        }

        // Execute focus fire for all units
        ExecuteFocusFire(game, army, enemies);

        // Retreat damaged units
        foreach (var unit in army)
        {
            if (ShouldRetreat(unit))
            {
                ExecuteRetreat(unit);
            }
        }
    }

    private bool IsRangedUnit(Unit unit)
    {
        var unitType = unit.GetUnitType();
        var weapon = unitType.GroundWeapon();

        return weapon != null && weapon.MaxRange() > 64; // More than 2 tiles
    }

    private void ExecuteKiting(Game game, Unit unit, List<Unit> enemies)
    {
        int unitId = unit.GetID();
        int currentFrame = game.GetFrameCount();

        // Get enemies in weapon range
        var weaponRange = unit.GetUnitType().GroundWeapon().MaxRange();
        var enemiesInRange = enemies
            .Where(e => unit.GetDistance(e) <= weaponRange + 32)
            .OrderBy(e => unit.GetDistance(e))
            .ToList();

        if (!enemiesInRange.Any())
            return;

        var closestEnemy = enemiesInRange.First();

        // Check if we recently attacked
        int framesSinceLastAttack = _unitLastAttackFrame.ContainsKey(unitId)
            ? currentFrame - _unitLastAttackFrame[unitId]
            : 1000;

        // Kite: Move away while weapon cooling down, attack when ready
        if (framesSinceLastAttack < 15) // Weapon cooldown (approximate)
        {
            // Move away from enemy
            int dx = unit.GetPosition().X - closestEnemy.GetPosition().X;
            int dy = unit.GetPosition().Y - closestEnemy.GetPosition().Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance > 0)
            {
                int retreatX = unit.GetPosition().X + (int)(dx / distance * GameConstants.KITE_RETREAT_DISTANCE);
                int retreatY = unit.GetPosition().Y + (int)(dy / distance * GameConstants.KITE_RETREAT_DISTANCE);
                var retreatPos = new Position(retreatX, retreatY);

                unit.Move(retreatPos);
            }
        }
        else
        {
            // Attack
            unit.Attack(closestEnemy);
            _unitLastAttackFrame[unitId] = currentFrame;
        }
    }

    private void ExecuteFocusFire(Game game, List<Unit> army, List<Unit> enemies)
    {
        if (!enemies.Any())
            return;

        // Select best target
        var target = SelectBestTarget(army, enemies);
        if (target == null)
            return;

        // Focus fire with all units
        foreach (var unit in army)
        {
            int unitId = unit.GetID();
            int currentFrame = game.GetFrameCount();

            // Update target periodically or if idle
            if (unit.IsIdle() || (currentFrame % GameConstants.MICRO_UPDATE_INTERVAL == 0))
            {
                if (!ShouldRetreat(unit)) // Don't attack if retreating
                {
                    unit.Attack(target);
                }
            }
        }
    }

    private Unit? SelectBestTarget(List<Unit> army, List<Unit> enemies)
    {
        if (!enemies.Any())
            return null;

        // Target priority:
        // 1. Enemy workers
        // 2. Low HP units
        // 3. High value units (tanks, templars, etc.)
        // 4. Closest unit

        // Check for workers
        var workers = enemies.Where(e => e.GetUnitType().IsWorker()).ToList();
        if (workers.Any())
        {
            return workers.OrderBy(w => army.First().GetDistance(w)).First();
        }

        // Check for damaged units
        var damagedUnits = enemies
            .Where(e => e.GetHitPoints() < e.GetUnitType().MaxHitPoints() * 0.5)
            .ToList();

        if (damagedUnits.Any())
        {
            return damagedUnits.OrderBy(u => u.GetHitPoints()).First();
        }

        // Check for high value units
        var highValueTypes = new HashSet<UnitType>
        {
            UnitType.Terran_Siege_Tank_Tank_Mode, UnitType.Terran_Siege_Tank_Siege_Mode,
            UnitType.Protoss_High_Templar, UnitType.Protoss_Reaver,
            UnitType.Zerg_Defiler, UnitType.Zerg_Lurker
        };

        var highValueTargets = enemies
            .Where(e => highValueTypes.Contains(e.GetUnitType()))
            .ToList();

        if (highValueTargets.Any())
        {
            return highValueTargets.OrderBy(u => army.First().GetDistance(u)).First();
        }

        // Default: Closest enemy
        return enemies.OrderBy(e => army.First().GetDistance(e)).FirstOrDefault();
    }

    private bool ShouldRetreat(Unit unit)
    {
        int currentHP = unit.GetHitPoints();
        int maxHP = unit.GetUnitType().MaxHitPoints();

        return currentHP > 0 && currentHP < maxHP * GameConstants.RETREAT_HP_THRESHOLD_PERCENT / 100;
    }

    private void ExecuteRetreat(Unit unit)
    {
        // Move toward rally point
        unit.Move(_rallyPoint);
    }

    // Public query methods
    public int GetArmySupply(List<Unit> army)
    {
        return army.GetTotalSupplyUsed();
    }

    public string GetStateDescription()
    {
        return CurrentState switch
        {
            ArmyState.Rallying => "Rallying at base",
            ArmyState.Defending => "Defending base",
            ArmyState.Attacking => "Attacking enemy",
            ArmyState.Retreating => "Retreating",
            _ => "Unknown"
        };
    }
}
