using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

public class TileCacheTests
{
    static readonly Rect ViewRect = new(0, 0, 1000, 500);
    const double Zoom = 1.0;

    static Tile CreateTile(long x, long y, int tileWidth = 1000, int tileHeight = 500) =>
        new(new TileKey(x, y, 0, tileWidth, tileHeight), $"<svg>{x},{y}</svg>", 1.0, Pos.None);

    static TileCache CreateCache() => new(() => { });

    [Fact]
    public void TryGetCached_ShouldReturnCachedTile()
    {
        TileCache cache = CreateCache();
        Tile tile = CreateTile(1, 2);
        cache.SetCached(tile, ViewRect, Zoom);

        Assert.True(cache.TryGetCached(tile.Key, ViewRect, Zoom, out Tile cachedTile));
        Assert.Equal(tile, cachedTile);
    }

    [Fact]
    public void TryGetCached_ShouldReturnEmptyTile_WhenNotCached()
    {
        TileCache cache = CreateCache();

        Assert.False(cache.TryGetCached(new TileKey(1, 2, 0, 1000, 500), ViewRect, Zoom, out Tile tile));
        Assert.Equal(Tile.Empty, tile);
    }

    [Fact]
    public void TryGetLastUsed_ShouldReturnTile_WhenViewRectAndZoomAreUnchanged()
    {
        TileCache cache = CreateCache();
        Tile tile = CreateTile(1, 2);
        cache.SetCached(tile, ViewRect, Zoom);

        Assert.True(cache.TryGetLastUsed(ViewRect, Zoom, out Tile lastUsedTile));
        Assert.Equal(tile, lastUsedTile);
    }

    [Fact]
    public void TryGetLastUsed_ShouldMiss_WhenViewRectOrZoomChanged()
    {
        TileCache cache = CreateCache();
        cache.SetCached(CreateTile(1, 2), ViewRect, Zoom);

        Assert.False(cache.TryGetLastUsed(new Rect(10, 10, 1000, 500), Zoom, out Tile _));
        Assert.False(cache.TryGetLastUsed(ViewRect, 2.0, out Tile _));
    }

    [Fact]
    public void TryGetCached_ShouldInvalidateCache_WhenTileSizeChanged()
    {
        TileCache cache = CreateCache();
        Tile tile = CreateTile(1, 2, 1000, 500);
        cache.SetCached(tile, ViewRect, Zoom);

        Assert.False(cache.TryGetCached(new TileKey(1, 2, 0, 800, 400), ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(tile.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void ClearCache_ShouldRemoveCachedAndLastUsedTiles()
    {
        TileCache cache = CreateCache();
        Tile tile = CreateTile(1, 2);
        cache.SetCached(tile, ViewRect, Zoom);

        cache.ClearCache();

        Assert.False(cache.TryGetLastUsed(ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(tile.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void SetCached_ShouldEvictLeastRecentlyUsed_WhenCapExceeded()
    {
        TileCache cache = CreateCache();
        Tile firstTile = CreateTile(0, 0);
        for (long x = 0; x < 100; x++)
        {
            cache.SetCached(CreateTile(x, 0), ViewRect, Zoom);
        }
        // Touching the oldest tile makes it recently used, so it survives the eviction.
        Assert.True(cache.TryGetCached(firstTile.Key, ViewRect, Zoom, out Tile _));

        Tile overflowTile = CreateTile(100, 0);
        cache.SetCached(overflowTile, ViewRect, Zoom);

        // The recently used, recently added, and new tiles survive; only the least
        // recently used quarter is evicted.
        Assert.True(cache.TryGetCached(firstTile.Key, ViewRect, Zoom, out Tile _));
        Assert.True(cache.TryGetCached(overflowTile.Key, ViewRect, Zoom, out Tile _));
        Assert.True(cache.TryGetCached(CreateTile(99, 0).Key, ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(CreateTile(1, 0).Key, ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(CreateTile(25, 0).Key, ViewRect, Zoom, out Tile _));
        Assert.True(cache.TryGetCached(CreateTile(26, 0).Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void Dispose_ShouldInvokeDisposeAction()
    {
        bool isDisposed = false;
        using (TileCache cache = new(() => isDisposed = true)) { }

        Assert.True(isDisposed);
    }
}
