using Insgram_Manager.Data;

namespace Insgram_Manager.Repositories.Interfaces;

/// <summary>
/// Unit of Work interface for transaction management.
/// Coordinates repository operations within a single transaction boundary.
/// Repositories will use this in future phases.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<T> GetRepository<T>() where T : Domain.Entities.BaseEntity;

    Task<int> SaveChangesAsync();

    Task BeginTransactionAsync();

    Task CommitTransactionAsync();

    Task RollbackTransactionAsync();
}
