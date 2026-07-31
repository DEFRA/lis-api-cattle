using Defra.Database.Postgres;

var builder = WebApplication.CreateBuilder(args);


 builder.Services.AddPostgresDatabase(builder.Configuration);

var app = builder.Build();
app.UsePostgresDatabase();


