using System.Linq.Expressions;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Persistence;

public class EfRepository<T>(AppDbContext context) : IRepository<T> where T : Entity
{
    private readonly DbSet<T> _entities = context.Set<T>();
    private AppDbContext _context = context;

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _entities.AddAsync(entity, cancellationToken);
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _entities.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default)
    {
        return await _entities.Where(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<T?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[]? includesProperties)
    {
        var query = _entities.AsQueryable();
        query = includesProperties?.Aggregate(query, (current, include) => current.Include(include)) ?? query;
        return await query.Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _entities.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[]? includesProperties)
    {
        var query = _entities.AsQueryable();
        query = includesProperties?.Aggregate(query, (current, include) => current.Include(include)) ?? query;

        if (filter is not null) query = query.Where(filter);

        return await query.ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public void SetContext(AppDbContext newContext)
    {
        _context = newContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}