using BWAPI.NET;
using Shared.Utils;

namespace Shared.Intelligence;

public enum MapSize
{
    Small,
    Medium,
    Large
}

public class MapAnalysis
{
    public MapSize Size { get; private set; }
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public TilePosition? MapCenter { get; private set; }
    public List<TilePosition> StartLocations { get; private set; } = new();
    public List<Position> MineralFields { get; private set; } = new();
    public List<Position> Geysers { get; private set; } = new();

    private bool _initialized = false;

    public void Update(Game game, Player self)
    {
        if (!_initialized)
        {
            Initialize(game, self);
            _initialized = true;
        }

        // Periodic updates could go here (e.g., checking for new resources)
        // For now, map analysis is mostly static after initialization
    }

    private void Initialize(Game game, Player self)
    {
        // Get map dimensions
        MapWidth = game.MapWidth();
        MapHeight = game.MapHeight();
        MapCenter = new TilePosition(MapWidth / 2, MapHeight / 2);

        // Determine map size
        int maxDimension = Math.Max(MapWidth, MapHeight);
        if (maxDimension < GameConstants.SMALL_MAP_TILE_THRESHOLD)
        {
            Size = MapSize.Small;
        }
        else if (maxDimension < GameConstants.MEDIUM_MAP_TILE_THRESHOLD)
        {
            Size = MapSize.Medium;
        }
        else
        {
            Size = MapSize.Large;
        }

        // Get start locations
        StartLocations = game.GetStartLocations().ToList();

        // Get resource locations
        var minerals = game.GetMinerals();
        foreach (var mineral in minerals)
        {
            MineralFields.Add(mineral.GetPosition());
        }

        var geysers = game.GetGeysers();
        foreach (var geyser in geysers)
        {
            Geysers.Add(geyser.GetPosition());
        }
    }

    public bool IsSmallMap()
    {
        return Size == MapSize.Small;
    }

    public bool IsMediumMap()
    {
        return Size == MapSize.Medium;
    }

    public bool IsLargeMap()
    {
        return Size == MapSize.Large;
    }

    public int GetDistanceBetweenStarts(TilePosition start1, TilePosition start2)
    {
        int dx = start1.X - start2.X;
        int dy = start1.Y - start2.Y;
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }

    public TilePosition? GetClosestStartLocation(TilePosition from, List<TilePosition>? exclude = null)
    {
        exclude = exclude ?? new List<TilePosition>();

        return StartLocations
            .Where(loc => !exclude.Any(ex => ex.X == loc.X && ex.Y == loc.Y))
            .OrderBy(loc => GetDistanceBetweenStarts(from, loc))
            .FirstOrDefault();
    }

    public List<TilePosition> GetExpansionLocations(TilePosition myStart)
    {
        // Return start locations other than our own, ordered by distance
        return StartLocations
            .Where(loc => loc.X != myStart.X || loc.Y != myStart.Y)
            .OrderBy(loc => GetDistanceBetweenStarts(myStart, loc))
            .ToList();
    }

    public Position? GetNearestGeyser(Position basePosition)
    {
        return Geysers
            .OrderBy(g => g.GetDistance(basePosition))
            .FirstOrDefault();
    }

    public List<Position> GetGeysersNearBase(Position basePosition, int maxDistance = 400)
    {
        return Geysers
            .Where(g => g.GetDistance(basePosition) <= maxDistance)
            .OrderBy(g => g.GetDistance(basePosition))
            .ToList();
    }

    public int GetStartLocationCount()
    {
        return StartLocations.Count;
    }

    public string GetMapSizeDescription()
    {
        return Size switch
        {
            MapSize.Small => $"Small ({MapWidth}x{MapHeight})",
            MapSize.Medium => $"Medium ({MapWidth}x{MapHeight})",
            MapSize.Large => $"Large ({MapWidth}x{MapHeight})",
            _ => $"Unknown ({MapWidth}x{MapHeight})"
        };
    }
}
