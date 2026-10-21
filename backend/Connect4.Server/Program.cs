using Connect4.Server.Game;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCors";

builder.Services.AddSignalR();
builder.Services.AddSingleton<GameManager>();

// Allowed origins come from config (appsettings / env var), with localhost dev defaults
// always included so local development keeps working regardless of configuration.
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var devOrigins = new[] { "http://localhost:4200", "http://localhost:4201" };
var allowedOrigins = devOrigins.Concat(configuredOrigins).Distinct().ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.MapGet("/", () => "Connect 4 server is running.");
app.MapHub<GameHub>("/hubs/game");

app.Run();
