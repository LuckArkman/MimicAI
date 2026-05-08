using Database.Postgres;

namespace Repositorys.Interfaces;

public interface IUserRepository
{
    Task<UserEntity?> GetByIdAsync(Guid id);
    Task<UserEntity?> GetByEmailAsync(string email);
    Task<UserEntity> CreateAsync(UserEntity user);
    Task<bool> EmailExistsAsync(string email);
}
