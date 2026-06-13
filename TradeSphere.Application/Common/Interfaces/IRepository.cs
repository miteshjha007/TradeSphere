using System.Collections.Generic;
using System.Threading.Tasks;
using TradeSphere.Domain.Entities;

namespace TradeSphere.Application.Common.Interfaces
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T> GetByIdAsync(int id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}
