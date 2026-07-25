using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

public class TileCacheTests
{
    static readonly Rect ViewRect = new(0, 0, 1000, 500);
    const double Zoom = 1.0;

    // A small budget keeps the tests fast: they can cross it with a few KB of svg instead of
    // allocating megabytes to reach the production budget.
    const long MaxCachedBytes = 10_000;
    const int BigTileBytes = (int)(MaxCachedBytes / 4);

    static Tile CreateTile(long x, long y, int tileWidth = 1000, int tileHeight = 500, int byteSize = 0) =>
        new(new TileKey(x, y, 0, tileWidth, tileHeight), CreateSvg(x, y, byteSize), 1.0, Pos.None);

    // Svg content stays unique per (x,y) so tiles remain distinguishable, padded out to roughly
    // byteSize bytes (2 bytes per UTF-16 char) so tests can fill the cache byte budget.
    static string CreateSvg(long x, long y, int byteSize)
    {
        string svg = $"<svg>{x},{y}</svg>";
        int padding = byteSize / 2 - svg.Length;
        return padding <= 0 ? svg : svg + new string(' ', padding);
    }

    static TileCache CreateCache() => new(() => { }, MaxCachedBytes);

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
    public void SetCached_ShouldEvictLeastRecentlyUsed_WhenByteBudgetExceeded()
    {
        TileCache cache = CreateCache();
        Tile firstTile = CreateTile(0, 0, byteSize: BigTileBytes);
        cache.SetCached(firstTile, ViewRect, Zoom);

        Tile lastTile = firstTile;
        for (long x = 1; x <= 10; x++)
        {
            lastTile = CreateTile(x, 0, byteSize: BigTileBytes);
            cache.SetCached(lastTile, ViewRect, Zoom);
            // Touching the oldest tile makes it recently used, so it survives the eviction.
            cache.TryGetCached(firstTile.Key, ViewRect, Zoom, out Tile _);
        }

        Assert.True(cache.TryGetCached(firstTile.Key, ViewRect, Zoom, out Tile _));
        Assert.True(cache.TryGetCached(lastTile.Key, ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(CreateTile(1, 0).Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void SetCached_ShouldEvictDownToTarget_SoEvictionStaysRare()
    {
        TileCache cache = CreateCache();
        for (long x = 0; x < 10; x++)
        {
            cache.SetCached(CreateTile(x, 0, byteSize: BigTileBytes), ViewRect, Zoom);
        }

        // Eviction frees headroom, so the tiles added right before it are not evicted again by
        // the next add.
        Tile survivor = CreateTile(9, 0, byteSize: BigTileBytes);
        cache.SetCached(CreateTile(10, 0, byteSize: BigTileBytes), ViewRect, Zoom);

        Assert.True(cache.TryGetCached(survivor.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void SetCached_ShouldNotDoubleCountBytes_WhenSameKeyIsReplaced()
    {
        TileCache cache = CreateCache();
        Tile smallTile = CreateTile(0, 0);
        cache.SetCached(smallTile, ViewRect, Zoom);

        // Re-caching one key replaces its bytes; counting them cumulatively would cross the
        // budget after a few adds and evict the small tile.
        for (long y = 0; y < 20; y++)
        {
            cache.SetCached(CreateTile(1, 1, byteSize: BigTileBytes), ViewRect, Zoom);
        }

        Assert.True(cache.TryGetCached(smallTile.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void SetCached_ShouldCacheTile_WhenLargerThanWholeBudget()
    {
        TileCache cache = CreateCache();
        Tile hugeTile = CreateTile(0, 0, byteSize: (int)(MaxCachedBytes * 2));
        cache.SetCached(hugeTile, ViewRect, Zoom);

        Assert.True(cache.TryGetCached(hugeTile.Key, ViewRect, Zoom, out Tile cachedTile));
        Assert.Equal(hugeTile, cachedTile);

        // The cache degrades to holding only the newest tile, but is never left empty.
        Tile nextHugeTile = CreateTile(1, 0, byteSize: (int)(MaxCachedBytes * 2));
        cache.SetCached(nextHugeTile, ViewRect, Zoom);

        Assert.True(cache.TryGetCached(nextHugeTile.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void SetCached_ShouldEvictLeastRecentlyUsed_WhenTileCountCapExceeded()
    {
        // A budget large enough that the tiny tiles never reach it, so the secondary tile count
        // guard is what evicts here.
        TileCache cache = new(() => { }, 10_000_000);
        Tile lastTile = Tile.Empty;
        for (long x = 0; x <= 500; x++)
        {
            lastTile = CreateTile(x, 0);
            cache.SetCached(lastTile, ViewRect, Zoom);
        }

        Assert.True(cache.TryGetCached(lastTile.Key, ViewRect, Zoom, out Tile _));
        Assert.False(cache.TryGetCached(CreateTile(0, 0).Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void ClearCache_ShouldResetCachedBytes()
    {
        TileCache cache = CreateCache();
        for (long x = 0; x < 10; x++)
        {
            cache.SetCached(CreateTile(x, 0, byteSize: BigTileBytes), ViewRect, Zoom);
        }

        cache.ClearCache();

        // Without the reset the stale total would evict these right away.
        Tile firstTile = CreateTile(0, 1, byteSize: BigTileBytes);
        cache.SetCached(firstTile, ViewRect, Zoom);
        cache.SetCached(CreateTile(1, 1, byteSize: BigTileBytes), ViewRect, Zoom);

        Assert.True(cache.TryGetCached(firstTile.Key, ViewRect, Zoom, out Tile _));
    }

    [Fact]
    public void Dispose_ShouldInvokeDisposeAction()
    {
        bool isDisposed = false;
        using (TileCache cache = new(() => isDisposed = true)) { }

        Assert.True(isDisposed);
    }
}
