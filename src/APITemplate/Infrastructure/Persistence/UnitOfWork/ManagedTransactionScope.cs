namespace APITemplate.Infrastructure.Persistence;

internal sealed class ManagedTransactionScope
{
    private int _depth;

    public bool IsActive => Volatile.Read(ref _depth) > 0;

    public IDisposable Enter()
    {
        Interlocked.Increment(ref _depth);
        return new Releaser(this);
    }

    private void Exit() => Interlocked.Decrement(ref _depth);

    private sealed class Releaser(ManagedTransactionScope scope) : IDisposable
    {
        private ManagedTransactionScope? _scope = scope;

        public void Dispose()
        {
            var scope = Interlocked.Exchange(ref _scope, null);
            scope?.Exit();
        }
    }
}
