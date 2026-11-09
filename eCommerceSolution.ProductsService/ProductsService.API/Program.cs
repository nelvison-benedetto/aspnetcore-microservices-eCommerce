using BusinessLogicLayer;
using DataAccessLayer;
using eCommerce.ProductsMicroService.API.APIEndpoints;
using eCommerce.ProductsMicroService.API.Middleware;
using FluentValidation.AspNetCore;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

//Add DAL and BLL services
builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddBusinessLogicLayer();

builder.Services.AddControllers();

//FluentValidations
builder.Services.AddFluentValidationAutoValidation();

//Add model binder to read values from JSON to enum
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

//Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Cors
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(builder => {
        builder.WithOrigins("http://localhost:4200")
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});


var app = builder.Build();

app.UseExceptionHandlingMiddleware();
app.UseRouting();

//Cors
app.UseCors();

//Swagger
app.UseSwagger();
app.UseSwaggerUI();

//Auth (no authentication scheme is registered, so UseAuthentication is not needed)
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapProductAPIEndpoints();

app.Run();


