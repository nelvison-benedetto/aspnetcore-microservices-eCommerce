using eCommerce.Core.RepositoryContracts;
using eCommerce.Infrastructure.DbContext;
using eCommerce.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;  //x usare IServiceCollection e metodi come AddTransient()

namespace eCommerce.Infrastructure;

public static class DependencyInjection
{

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)  //extension method (aggiungi this method ai 'services'), quindi puoi fare services.AddInfrastructure() in Program.cs
    {
        //TO DO: Add services to the IoC container
        //Infrastructure services often include data access, caching and other low-level components.

        services.AddTransient<IUsersRepository, UsersRepository>();  //quando chiamerai IUsersRepository, ti verra iniettata UsersRepository. AddTransient() means that a new instance of the service will be created each time it is requested. This is suitable for lightweight, stateless services.
        services.AddTransient<DapperDbContext>();

        return services;
    }
}