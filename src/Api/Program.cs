using Defra.Database.Postgres;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Services;
using Microsoft.AspNetCore.Mvc;

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

app.MapGet("/holdings/{cph}/cattle", async (string cph, [FromServices] ICattleService cattleService) =>
{
    var cattle = await cattleService.GetCattleForHoldingAsync(cph);
    return Results.Ok(cattle);
})
.WithName("GetCattleForHolding");

app.Run();


