using Dispatch.Api.Data;
using Dispatch.Api.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("Cosmos")
    ?? throw new InvalidOperationException("ConnectionStrings:Cosmos is required.");
var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "DispatchBoard";

builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseCosmos(connectionString, databaseName, cosmosOptions =>
    {
        cosmosOptions.ConnectionMode(ConnectionMode.Gateway);
    }));
builder.Services.AddScoped<IMovementService, MovementService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ready" }));

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
    var recreate = builder.Configuration.GetValue("Cosmos:RecreateAndSeedOnStartup", true);
    await SeedData.InitialiseAsync(dbContext, recreate, CancellationToken.None);
}

await app.RunAsync();

public partial class Program;
