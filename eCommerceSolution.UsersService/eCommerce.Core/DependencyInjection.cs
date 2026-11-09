using eCommerce.Core.ServiceContracts;
using eCommerce.Core.Services;
using eCommerce.Core.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace eCommerce.Core;

public static class DependencyInjection
{

    public static IServiceCollection AddCore(this IServiceCollection services)  //extension method
    {

        services.AddTransient<IUsersService, UsersService>();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();  //AddValidatorsFromAssemblyContaining given by fluentvalidator, Serve a individuare le classi che implementano validator FluentValidation e a registrarle nei servizi dell'applicazion, gli devi solo passare un tipo di riferimento, in questo caso LoginRequestValidator, e lui cerca tutte le classi che implementano AbstractValidator<T> in quell'assembly e le registra come servizi transienti.

        return services;
    }


}
