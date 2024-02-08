using Nito.AsyncEx;
using System.Data;
using System.Diagnostics;

namespace RinhaBack2401.Model;

public sealed class Pool<T> : IAsyncDisposable where T : class, IAsyncDisposable
{
    private readonly Queue<T> queue;
    private readonly int poolSize;

    public Pool(IEnumerable<T> items, ILogger<Pool<T>> logger)
    {
        this.logger = logger;
        queue = new Queue<T>(items);
        poolSize = queue.Count;
        Debug.Assert(poolSize > 0);
        newItemEnquedSemaphore = new(poolSize);
        typeName = typeof(T).Name;
        logger.PoolCreated(typeName, poolSize);
    }

    private readonly AsyncLock mutex = new();
    private readonly SemaphoreSlim newItemEnquedSemaphore;
    private readonly string typeName;
    private readonly ILogger<Pool<T>> logger;

    public async ValueTask<PoolItem<T>> RentAsync(CancellationToken cancellationToken)
    {
        T? item = null;
        await newItemEnquedSemaphore.WaitAsync(cancellationToken);
        try
        {
            using (var _ = await mutex.LockAsync(cancellationToken))
            {
                logger.PoolRentingItem(typeName, queue.Count);
                item = queue.Dequeue();
            }
            var poolItem = new PoolItem<T>(item, ReturnPoolItemAsync);
            return poolItem;
        }
        catch
        {
            if (item != null)
            {
                using (var _ = await mutex.LockAsync(cancellationToken))
                    queue.Enqueue(item);
                newItemEnquedSemaphore.Release();
            }
            throw;
        }
    }

    public async ValueTask<List<T>> ReturnAllAsync(CancellationToken cancellationToken)
    {
        logger.PoolReturningAllItems(typeName, queue.Count);
        await Task.WhenAll(Enumerable.Range(1, poolSize).Select(_ => newItemEnquedSemaphore.WaitAsync(cancellationToken)));
        using var _ = await mutex.LockAsync(cancellationToken);
        var items = new List<T>();
        while (queue.TryDequeue(out var item))
            items.Add(item);
        return items;
    }

    private async ValueTask ReturnPoolItemAsync(PoolItem<T> poolItem)
    {
        using (var _ = await mutex.LockAsync())
            queue.Enqueue(poolItem.Value);
        logger.PoolReturnedItem(typeName, queue.Count);
        newItemEnquedSemaphore.Release();
    }

    public async ValueTask DisposeAsync()
    {
        var items = await ReturnAllAsync(CancellationToken.None);
        await Parallel.ForEachAsync(items, (item, _) => item.DisposeAsync());
    }

}

public readonly struct PoolItem<TItem>(TItem value, Func<PoolItem<TItem>, ValueTask> returnPoolItemAsync)
{
    public TItem Value { get; } = value;

    public ValueTask DisposeAsync() => returnPoolItemAsync(this);
}
