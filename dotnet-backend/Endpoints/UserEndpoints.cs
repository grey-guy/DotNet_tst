using DotnetBackend.Data;
using DotnetBackend.Models;
using DotnetBackend.Services;

namespace DotnetBackend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireRateLimiting("fixed");

        group.MapGet("/", (DataStore store) =>
        {
            var users = store.GetUsers();
            return Results.Json(new UsersResponse { Users = users, Count = users.Count });
        })
        .CacheOutput("Users");

        group.MapGet("/{id:int}", (int id, DataStore store) =>
        {
            var user = store.GetUserById(id);
            return user is null
                ? Results.NotFound(new { error = "User not found" })
                : Results.Json(user);
        })
        .CacheOutput("Users");

        group.MapPost("/", (CreateUserRequest request, DataStore store) =>
        {
            var validation = ValidationService.ValidateCreateUser(request.Name, request.Email, request.Role);
            if (!validation.IsValid)
                return Results.BadRequest(new { errors = validation.Errors });

            var user = store.CreateUser(request.Name.Trim(), request.Email.Trim(), request.Role.Trim());
            return Results.Created($"/api/users/{user.Id}", user);
        });
    }
}
