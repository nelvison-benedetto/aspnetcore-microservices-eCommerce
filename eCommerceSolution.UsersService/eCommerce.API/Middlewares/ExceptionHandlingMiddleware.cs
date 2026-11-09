using Microsoft.AspNetCore.Builder;  //contiene tipi relativi alla configurazione della pipeline HTTP, tra cui IApplicationBuilder
using Microsoft.AspNetCore.Http;  //contiene tipi relativi alla gestione delle richieste e risposte HTTP, tra cui HttpContext
using System.Threading.Tasks;  //contiene tipi relativi alla programmazione asincrona, tra cui Task

namespace eCommerce.API.Middlewares;

//🔥CREATO FILE da template 'MiddlewareClass'

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;  //RequestDelegate rappresenta un delegato che gestisce una richiesta HTTP async. In questo caso, _next rappresenta il middleware successivo nella pipeline HTTP.
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;  //i mexs saranno associati al tipo ExceptionHandlingMiddleware, quindi sarà possibile filtrare i mexs in base al tipo di middleware che li ha generati

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)  //next è l'item successivo della pipeline (lo passa auto asp.net core)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext)  //Invoke è il metodo che viene chiamato per gestire la richiesta HTTP. HttpContext puo darti httpContext.Request httpContext.Response httpContext.User httpContext.RequestServices
    {
        try
        {
            await _next(httpContext);  //continua la pipeline 
        }
        catch (Exception ex)
        {
            //Log the exception type and message
            _logger.LogError($"{ex.GetType().ToString()}: {ex.Message}");

            if (ex.InnerException is not null)  //InnerException check xk un'eccezzione puo racchiudere dentro di se anche un'altra exception
            {
                //Log the inner exception type and message
                _logger.LogError($"{ex.InnerException.GetType().ToString()}: {ex.InnerException.Message}");
            }
            httpContext.Response.StatusCode = 500; //Internal Server Error
            await httpContext.Response.WriteAsJsonAsync(new { Message = ex.Message, Type = ex.GetType().ToString() });  //scrive un obj jsone nel body della response 
        }

    }
}

// Extension method used to add the middleware to the HTTP request pipeline.
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();  //UseMiddleware<T>() è un metodo di ASP.NET Core che aggiunge alla pipeline il middleware indicato dal tipo generico T
    }
}
