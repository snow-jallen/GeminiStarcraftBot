namespace Shared.Utils;

public static class GameConstants
{
    // Worker management
    public const int WORKERS_PER_MINERAL_PATCH = 2;
    public const int MINERAL_PATCHES_PER_BASE = 8;
    public const int OPTIMAL_WORKERS_PER_BASE = 16;
    public const int WORKERS_PER_GAS = 3;
    public const int MIN_WORKER_COUNT = 20;
    public const int MAX_WORKER_COUNT = 70;

    // Supply management
    public const int SUPPLY_BUFFER = 6;  // Build supply when this close to cap
    public const int MAX_SUPPLY = 400;   // Game maximum (200 displayed supply)

    // Army thresholds
    public const int RALLY_ARMY_SIZE = 20;           // Minimum units to start rallying
    public const int ATTACK_ARMY_SUPPLY = 40;        // Minimum supply to attack
    public const int RETREAT_ARMY_SIZE = 10;         // Retreat if below this
    public const int DEFEND_WORKER_COUNT = 12;       // Max workers to pull for defense
    public const int RETREAT_HP_THRESHOLD_PERCENT = 30;  // Retreat units below this HP

    // Combat distances (in pixels, 1 tile = 32 pixels)
    public const int THREAT_DETECTION_RANGE = 800;   // Detect threats within this range of base
    public const int THREAT_CLUSTER_RADIUS = 300;    // Group enemy units within this radius
    public const int KITE_RETREAT_DISTANCE = 64;     // Distance to retreat while kiting
    public const int RALLY_POINT_DISTANCE = 300;     // How close to rally point before considered "arrived"
    public const int BASE_DEFENSE_RADIUS = 500;      // Pull workers within this radius

    // Economy thresholds
    public const int EXPANSION_MINERAL_THRESHOLD = 400;  // Build expansion at this mineral count
    public const int ZERG_EXPANSION_MINERAL_THRESHOLD = 300;  // Zerg expand cheaper
    public const int TECH_MINERAL_THRESHOLD = 500;   // Start teching at this mineral count
    public const int TECH_GAS_THRESHOLD = 100;       // Start teching at this gas count
    public const int FIRST_GAS_SUPPLY = 16;          // Build first gas at this supply (8 displayed)
    public const int SECOND_GAS_BASE_COUNT = 2;      // Build second gas when we have this many bases
    public const int MAX_GAS_GEYSERS = 3;            // Maximum number of gas geysers to build

    // Build management
    public const int OPENING_WORKER_THROTTLE_MINERALS = 150;  // Stop workers when below this in opening
    public const int BUILD_LOCATION_SEARCH_RADIUS_MIN = 5;    // Minimum tiles from base
    public const int BUILD_LOCATION_SEARCH_RADIUS_MAX = 20;   // Maximum tiles from base
    public const int MAX_PRODUCTION_BUILDINGS = 5;    // Maximum barracks/gateways/pools

    // Timing (frames, game runs at 24 FPS nominally but varies)
    public const int FRAMES_PER_SECOND = 24;
    public const int SCOUT_INTERVAL_FRAMES = 200;     // Re-scout every ~8 seconds
    public const int WORKER_BALANCE_INTERVAL = 100;   // Balance workers every ~4 seconds
    public const int TECH_DECISION_INTERVAL = 300;    // Check tech every ~12 seconds
    public const int MICRO_UPDATE_INTERVAL = 24;      // Update unit micro every second
    public const int ATTACK_COMMAND_SPAM_INTERVAL = 50;  // Reissue attack every ~2 seconds
    public const int UNIT_STALE_THRESHOLD = 240;      // Mark enemy unit dead after 10 seconds not seen
    public const int BASE_RECHECK_INTERVAL = 2400;    // Re-scout enemy base every 100 seconds

    // Tech progression
    public const int TIER2_MIN_BASES = 2;             // Need this many bases for tier 2
    public const int TIER3_MIN_BASES = 3;             // Need this many bases for tier 3
    public const int TIER2_MIN_GAS = 100;             // Need this much gas for tier 2
    public const int TIER3_MIN_GAS = 300;             // Need this much gas for tier 3

    // Threat levels (unit counts)
    public const int THREAT_SCOUTING_MAX = 2;         // 1-2 units = scouting
    public const int THREAT_HARASSMENT_MAX = 6;       // 3-6 units = harassment
    public const int THREAT_ATTACK_MAX = 15;          // 7-15 units = attack
    public const int THREAT_ALLIN_MIN = 16;           // 16+ units = all-in
    public const int THREAT_PULL_WORKERS_MAX_SUPPLY = 20;  // Pull workers only if threat is small

    // Resource reservation
    public const int RESERVE_MINERALS_BUFFER = 50;    // Keep this much in reserve
    public const int RESERVE_GAS_BUFFER = 25;         // Keep this much gas in reserve

    // Map analysis
    public const int SMALL_MAP_TILE_THRESHOLD = 128;  // Maps smaller than this are "small" (in tiles)
    public const int MEDIUM_MAP_TILE_THRESHOLD = 256; // Maps between small and this are "medium"
}
