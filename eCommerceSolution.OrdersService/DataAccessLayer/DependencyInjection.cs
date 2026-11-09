using DataAccessLayer.Repositories;
using DataAccessLayer.RepositoryContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace DataAccessLayer
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
        {
            //TO DO: Add data access layer services into the IoC container
            string connectionStringTemplate = configuration.GetConnectionString("MongoDB")!;
            string connectionString = connectionStringTemplate
              .Replace("$MONGO_HOST", Environment.GetEnvironmentVariable("MONGODB_HOST"))
              .Replace("$MONGO_PORT", Environment.GetEnvironmentVariable("MONGODB_PORT"));

            services.AddSingleton<IMongoClient>(new MongoClient(connectionString));  //Singleton: un'unica istanza condivisa per tutta la vita del container DI
            services.AddScoped<IMongoDatabase>(provider =>  //Scoped: un'istanza per ogni scope DI, che normalmente in un'applicazione ASP.NET Core corrisponde a una singola richiesta HTTP
            {
                IMongoClient client = provider.GetRequiredService<IMongoClient>();  //Recupera dal container DI il servizio IMongoClient che ho registrato in precedenza (come Singleton)
                return client.GetDatabase(Environment.GetEnvironmentVariable("MONGODB_DATABASE"));  //
            });
            services.AddScoped<IOrdersRepository, OrdersRepository>();  
            return services;
        }
    }
}
