using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Repositories;

/// <summary>
/// Concrete generic repository implementation.
/// Can be used directly for any entity or as a base for specialized repositories.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class GenericRepository<T> : RepositoryBase<T> where T : BaseEntity
{
    public GenericRepository(DbContext context) : base(context)
    {
    }
}
