using System.Text.Json;
using Defra.Database.Postgres;
using Lis.Cattle;
using Lis.Cattle.Endpoints;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});
// Add services to the container.
builder.Services.AddPostgresDatabase(builder.Configuration);
builder.Services.AddCattleDatabaseConfigurations();

builder.Services.AddHttpClient<ICadsService, CadsService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CadsApi:BaseUrl"] ?? "http://cads-api/");
});

builder.Services.AddScoped<ICattleService, CattleService>();

var app = builder.Build();

app.UsePostgresDatabase();

if (app.Environment.IsDevelopment())
{
    await app.SeedDevelopmentDatabaseAsync();
}

app.MapCattleEndpoints();
app.MapRegistrationEndpoints();

app.Run();