using WebApplication1.Modules.Account.Domain;

namespace WebApplication1.Modules.Account.Application;

public interface IAccountDto
{
    Task<List<TelegramLoginSession>> GetAllAsync(CancellationToken cancellationToken);
}
