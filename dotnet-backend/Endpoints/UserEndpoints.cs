using System.Text.RegularExpressions;
using DotnetBackend.Data;
using DotnetBackend.Models;

namespace DotnetBackend.Endpoints;

public static class UserEndpoints
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapGet("/", (DataStore store) =>
        {
            var users = store.GetUsers();
            return Results.Json(new UsersResponse { Users = users, Count = users.Count });
        });

        group.MapGet("/{id:int}", (int id, DataStore store) =>
        {
            var user = store.GetUserById(id);
            return user is null
                ? Results.NotFound(new { error = "User not found" })
                : Results.Json(user);
        });

        group.MapPost("/", (CreateUserRequest request, DataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return Results.BadRequest(new { error = "Name, email, and role are required" });
            }

            if (!EmailRegex.IsMatch(request.Email))
            {
                return Results.BadRequest(new { error = "Invalid email format" });
            }

            var user = store.CreateUser(request.Name.Trim(), request.Email.Trim(), request.Role.Trim());
            return Results.Created($"/api/users/{user.Id}", user);
        });
    }
}
