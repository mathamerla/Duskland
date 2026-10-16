using Duskland.Modules.Products;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ProductConnection")
    ?? throw new NullReferenceException("Missing connection string");

ProductModules.RegisterModules(builder.Services, connectionString);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
