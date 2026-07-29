using Defra.Database.Postgres;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring
//
 builder.Services.AddPostgresDatabase(builder.Configuration);

var app = builder.Build();
app.UsePostgresDatabase();


