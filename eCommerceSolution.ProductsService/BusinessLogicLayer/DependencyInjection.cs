using BusinessLogicLayer.Mappers;
using BusinessLogicLayer.RabbitMQ;
using BusinessLogicLayer.ServiceContracts;
using BusinessLogicLayer.Services;
using BusinessLogicLayer.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogicLayer;


public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services)
    {
        //TO DO: Add Business Logic Layer services into the IoC container
        services.AddAutoMapper(cfg => { }, typeof(ProductAddRequestToProductMappingProfile).Assembly);  //registra nel DI  tutti i mappers (tutti queli presenti nell'assembly target), cfg => { } non aggiungi nessuna configurazione personalizzata 

        services.AddValidatorsFromAssemblyContaining<ProductAddRequestValidator>();
        services.AddScoped<IProductsService, ProductsService>();
        services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();

        return services;
    }
}