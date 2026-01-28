using DotnetBackend.Data;
using DotnetBackend.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DataStore>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const int defaultPort = 8080;
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!int.TryParse(portEnv, out var port))
{
    port = defaultPort;
}

app.MapGet("/health", () =>
{
    return Results.Json(new HealthResponse
    {
        Status = "ok",
        Message = ".NET backend is running"
    });
});

app.MapGet("/api/users", (DataStore store) =>
{
    var users = store.GetUsers();
    var response = new UsersResponse
    {
        Users = users,
        Count = users.Count
    };
    return Results.Json(response);
});

app.MapGet("/api/users/{id:int}", (int id, DataStore store) =>
{
    var user = store.GetUserById(id);
    return user is null
        ? Results.NotFound(new { error = "User not found" })
        : Results.Json(user);
});

app.MapGet("/api/tasks", (string? status, string? userId, DataStore store) =>
{
    var tasks = store.GetTasks(status, userId);
    var response = new TasksResponse
    {
        Tasks = tasks,
        Count = tasks.Count
    };
    return Results.Json(response);
});

app.MapGet("/api/stats", (DataStore store) =>
{
    var stats = store.GetStats();
    return Results.Json(stats);
});

app.Run($"http://0.0.0.0:{port}");
