using Academic.Services.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Academic.Tests;

/// <summary>Cache-aside của <see cref="AcademicCache"/> (BE-21), không cần Redis.</summary>
public sealed class AcademicCacheTests
{
    private static AcademicCache NewCache(IDistributedCache? store = null) =>
        new(store ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<AcademicCache>.Instance);

    [Fact]
    public async Task Second_read_comes_from_cache_until_invalidated()
    {
        var cache = NewCache();
        var loads = 0;
        Task<List<string>> Load(CancellationToken _) => Task.FromResult(new List<string> { $"lần {++loads}" });

        Assert.Equal(["lần 1"], await cache.GetOrLoadAsync("k", Load, default));
        Assert.Equal(["lần 1"], await cache.GetOrLoadAsync("k", Load, default));

        await cache.InvalidateAsync("k", default);
        Assert.Equal(["lần 2"], await cache.GetOrLoadAsync("k", Load, default));
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task Broken_cache_falls_back_to_loader()
    {
        var cache = NewCache(new BrokenCache());
        var loads = 0;

        Assert.Equal(1, await cache.GetOrLoadAsync("k", _ => Task.FromResult(++loads), default));
        Assert.Equal(2, await cache.GetOrLoadAsync("k", _ => Task.FromResult(++loads), default));
        await cache.InvalidateAsync("k", default); // không ném lỗi
    }

    /// <summary>Giả Redis không kết nối được.</summary>
    private sealed class BrokenCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}
