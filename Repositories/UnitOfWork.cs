using Insgram_Manager.Data;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Insgram_Manager.Repositories;

/// <summary>
/// Unit of Work implementation that manages a DbContext lifetime
/// and transaction boundary for coordinated repository operations.
/// Registered as Scoped — one instance per Blazor Server circuit.
/// Creates its own DbContext from the factory to avoid conflicting
/// with the existing AddDbContextFactory registration.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;

    public UnitOfWork(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _context = contextFactory.CreateDbContext();
    }

    public IRepository<T> GetRepository<T>() where T : BaseEntity
    {
        var type = typeof(T);

        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new GenericRepository<T>(_context);
            _repositories[type] = repository;
        }

        return (IRepository<T>)repository;
    }

    public async Task<int> SaveChangesAsync() =>
        await _context.SaveChangesAsync();

    public async Task BeginTransactionAsync() =>
        _transaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();

        await _context.DisposeAsync();
    }
}
