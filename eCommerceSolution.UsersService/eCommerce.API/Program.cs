using eCommerce.API.Middlewares;
using eCommerce.Core;
using eCommerce.Core.Mappers;
using eCommerce.Infrastructure;
using FluentValidation.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);  //webapplication è x hosting aspnetcore, args sono e.g.gli argomenti passati all'avvio tramite riga di comando

//yours
builder.Services.AddInfrastructure();
builder.Services.AddCore();

// Add controllers to the service collection
builder.Services.AddControllers().AddJsonOptions(options => {
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});  //registra yours controllers nel DI, e permette di configurare le opzioni di serializzazione JSON, in questo caso aggiungendo un convertitore per serializzare gli enum come stringhe invece che come valori numerici.  quindi e.g. invece di "gender": 0 ottieni "gender": "Male"

builder.Services.AddAutoMapper(typeof(ApplicationUserMappingProfile).Assembly);  //registra ur automapper nel DI

//FluentValidations
builder.Services.AddFluentValidationAutoValidation();


//Add API explorer services
builder.Services.AddEndpointsApiExplorer();  //registra i servizi che permettono di esplorare e descrivere gli endpoint dell'applicazione, utili e.g. x swagger

//Add swagger generation services to create swagger specification
builder.Services.AddSwaggerGen();

//Add CORS(Cross-Origin Resource Sharing) services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder => {
        builder.WithOrigins("http://localhost:4200")
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

//Build the web application
var app = builder.Build();

app.UseExceptionHandlingMiddleware();

//Routing
app.UseRouting();
app.UseSwagger(); //Adds endpoint that can serve the swagger.json
app.UseSwaggerUI(); //Adds swagger UI (interactive page to explore and test API endpoints)
app.UseCors();

//Auth
app.UseAuthentication();  //e.g. se utilizzi JWT Bearer allora con questo puoi leggere l'header
app.UseAuthorization();  //x usare e.g. [Authorize] sui controllers

//Controller routes
app.MapControllers();

app.Run();