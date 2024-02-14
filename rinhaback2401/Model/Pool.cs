#if POOL_OBJECTS
using System.Diagnostics;
using System.Threading.Channels;

namespace RinhaBack2401.Model;

public sealed class Pool<T> : IAsyncDisposable where T : class, IAsyncDisposable
{
    private readonly int poolSize;
#if !EXTRAOPTIMIZE
    private readonly string typeName;
    private readonly ILogger<Pool<T>> logger;
#endif
    private readonly Action<T> disposeAction;
    private int waitingRenters;
    private readonly Channel<T> queue = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = true,
        SingleReader = false,
        SingleWriter = false
    });

    public Pool(
        ICollection<T> items,
#if !EXTRAOPTIMIZE
        ILogger<Pool<T>> logger,
#endif
        Action<T> disposeAction
        )
    {
#if !EXTRAOPTIMIZE
        this.logger = logger;
#endif
        this.disposeAction = disposeAction;
        poolSize = items.Count;
        Debug.Assert(poolSize > 0);
        foreach (var item in items)
        {
            if (!queue.Writer.TryWrite(item))
                throw new ApplicationException("Failed to enqueue starting item on Pool.");
        }
#if !EXTRAOPTIMIZE
        typeName = typeof(T).Name;
        logger.PoolCreated(typeName, poolSize);
#endif
    }

    public async ValueTask<PoolItem<T>> RentAsync(CancellationToken cancellationToken)
    {
        T? item = null;
        Interlocked.Increment(ref waitingRenters);
        try
        {
#if !EXTRAOPTIMIZE
            logger.PoolRentingItem(typeName, queue.Reader.Count);
#endif
            item = await queue.Reader.ReadAsync(cancellationToken);
            var poolItem = new PoolItem<T>(item, ReturnPoolItemAsync);
            return poolItem;
        }
        catch
        {
            if (item != null)
                await queue.Writer.WriteAsync(item, cancellationToken);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref waitingRenters);
        }
    }

    public async ValueTask<List<T>> ReturnAllAsync(CancellationToken cancellationToken)
    {
#if !EXTRAOPTIMIZE
        logger.PoolReturningAllItems(typeName, queue.Reader.Count);
#endif
        var items = new List<T>();
        await foreach (var item in queue.Reader.ReadAllAsync(cancellationToken))
            items.Add(item);
        return items;
    }

    private async ValueTask ReturnPoolItemAsync(PoolItem<T> poolItem)
    {
        disposeAction(poolItem.Value);
        await queue.Writer.WriteAsync(poolItem.Value);
#if !EXTRAOPTIMIZE
        logger.PoolReturnedItem(typeName, queue.Reader.Count);
#endif
    }

    public async ValueTask DisposeAsync()
    {
        var items = await ReturnAllAsync(CancellationToken.None);
        await Parallel.ForEachAsync(items, (item, _) => item.DisposeAsync());
    }

#if !EXTRAOPTIMIZE
    public int QuantityAvailable => queue.Reader.Count;

    public int QuantityRented => poolSize - queue.Reader.Count;

    public int WaitingRenters => waitingRenters;
#endif

}

public readonly struct PoolItem<TItem>(TItem value, Func<PoolItem<TItem>, ValueTask> returnPoolItemAsync)
{
    public TItem Value { get; } = value;

    public ValueTask DisposeAsync() => returnPoolItemAsync(this);
}
#endif
