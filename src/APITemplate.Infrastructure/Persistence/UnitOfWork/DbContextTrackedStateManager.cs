using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence;

internal sealed class DbContextTrackedStateManager(AppDbContext dbContext)
{
    public IReadOnlyDictionary<object, TrackedEntitySnapshot> Capture()
    {
        return dbContext.ChangeTracker
            .Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .ToDictionary(
                entry => entry.Entity,
                entry => new TrackedEntitySnapshot(
                    entry.State,
                    entry.CurrentValues.Clone(),
                    entry.OriginalValues.Clone()),
                ReferenceEqualityComparer.Instance);
    }

    public void Restore(IReadOnlyDictionary<object, TrackedEntitySnapshot> snapshot)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (!snapshot.TryGetValue(entry.Entity, out var entitySnapshot))
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(entitySnapshot.CurrentValues);
            entry.OriginalValues.SetValues(entitySnapshot.OriginalValues);
            entry.State = entitySnapshot.State;
        }
    }

    internal sealed record TrackedEntitySnapshot(
        EntityState State,
        PropertyValues CurrentValues,
        PropertyValues OriginalValues);
}
