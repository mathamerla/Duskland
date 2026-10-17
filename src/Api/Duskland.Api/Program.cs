using Duskland.Api.ExceptionHandling;
using Duskland.Api.Modules.Products.Endpoints;
using Duskland.Modules.Products;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ProductConnection")
    ?? throw new InvalidOperationException("Missing connection string");

ProductModules.RegisterModules(builder.Services, connectionString);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(); 

var app = builder.Build();

app.UseExceptionHandler();


app.MapProductsEndpoints();

app.MapOpenApi();
app.MapScalarApiReference(); 

app.Run();
