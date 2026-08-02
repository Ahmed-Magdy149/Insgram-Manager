using System.Linq.Expressions;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Repositories;

/// <summary>
/// Abstract base class for repositories providing common CRUD implementation.
/// Derived classes can override or extend specific operations.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public abstract class RepositoryBase<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext Context;
    protected readonly DbSet<T> DbSet;

    protected RepositoryBase(DbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id) =>
        await DbSet.FindAsync(id);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync() =>
        await DbSet.AsNoTracking().ToListAsync();

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await DbSet.AsNoTracking().Where(predicate).ToListAsync();

    public virtual async Task AddAsync(T entity) =>
        await DbSet.AddAsync(entity);

    public virtual void Update(T entity) =>
        DbSet.Update(entity);

    public virtual void Remove(T entity) =>
        DbSet.Remove(entity);

    public virtual async Task<int> CountAsync() =>
        await DbSet.CountAsync();

    public virtual async Task<bool> ExistsAsync(int id) =>
        await DbSet.AnyAsync(e => e.Id == id);
}
