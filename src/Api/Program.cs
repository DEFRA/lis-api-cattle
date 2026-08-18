using Defra.Database.Postgres;
using Lis.Cattle.Endpoints;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddPostgresDatabase(builder.Configuration);

builder.Services.AddHttpClient<ICadsService, CadsService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CadsApi:BaseUrl"] ?? "http://cads-api/");
});

builder.Services.AddScoped<ICattleService, CattleService>();

var app = builder.Build();

app.UsePostgresDatabase();

app.MapCattleEndpoints();

app.Run();