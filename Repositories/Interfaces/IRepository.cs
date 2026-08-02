using System.Linq.Expressions;
using Insgram_Manager.Domain.Entities;

namespace Insgram_Manager.Repositories.Interfaces;

/// <summary>
/// Generic repository interface for CRUD operations on entities.
/// All entities inheriting from BaseEntity can use this contract.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);

    Task<IReadOnlyList<T>> GetAllAsync();

    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);

    void Update(T entity);

    void Remove(T entity);

    Task<int> CountAsync();

    Task<bool> ExistsAsync(int id);
}
