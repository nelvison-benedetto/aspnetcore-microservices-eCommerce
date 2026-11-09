using BusinessLogicLayer.Mappers;
using BusinessLogicLayer.RabbitMQ;
using BusinessLogicLayer.ServiceContracts;
using BusinessLogicLayer.Services;
using BusinessLogicLayer.Validators;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogicLayer
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services, IConfiguration configuration)
        {
            //TO DO: Add business logic layer services into the IoC container
            services.AddValidatorsFromAssemblyContaining<OrderAddRequestValidator>();
            services.AddAutoMapper(cfg => { }, typeof(OrderAddRequestToOrderMappingProfile).Assembly);
            services.AddScoped<IOrdersService, OrdersService>();
            services.AddStackExchangeRedisCache(options =>  //AddStackExchangeRedisCache registra implemantazione di IDistributedCache che utilizza 🔥Redis tramite la libreria StackExchange.Redis , cosi il tuo codice puo fare e.g. .GetStringAsync(key) / SetStringAsync(key, value, options) / .RemoveAsync(key). in questo ms uso i consumers rabbitmq per fare queste operaziione sulla cache!
            {
                options.Configuration = $"{configuration["REDIS_HOST"]}:{configuration["REDIS_PORT"]}";
            });  //🔥configurazione REDIS CACHE DISTRIBUITA!!
            services.AddSingleton<IRabbitMQProductNameUpdateConsumer, RabbitMQProductNameUpdateConsumer>();
            services.AddSingleton<IRabbitMQProductDeletionConsumer, RabbitMQProductDeletionConsumer>();
            services.AddHostedService<RabbitMQProductNameUpdateHostedService>();  //!!AddHostedService<T>() registra un servizio in background gestito dal Generic Host di .NET
            services.AddHostedService<RabbitMQProductDeletionHostedService>();
            return services;
        }
    }
}
