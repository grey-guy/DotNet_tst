using DotnetBackend.Data;
using DotnetBackend.Models;

namespace DotnetBackend.Endpoints;

public static class TaskEndpoints
{
    private static readonly string[] ValidStatuses = ["pending", "in-progress", "completed"];

    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks");

        group.MapGet("/", (string? status, string? userId, DataStore store) =>
        {
            var tasks = store.GetTasks(status, userId);
            return Results.Json(new TasksResponse { Tasks = tasks, Count = tasks.Count });
        });

        group.MapPost("/", (CreateTaskRequest request, DataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Status) ||
                request.UserId == 0)
            {
                return Results.BadRequest(new { error = "Title, status, and userId are required" });
            }

            if (!ValidStatuses.Contains(request.Status))
            {
                return Results.BadRequest(new { error = "Status must be one of: pending, in-progress, completed" });
            }

            if (store.GetUserById(request.UserId) is null)
            {
                return Results.BadRequest(new { error = "User not found" });
            }

            var task = store.CreateTask(request.Title.Trim(), request.Status, request.UserId);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapPut("/{id}", (int id, UpdateTaskRequest request, DataStore store) =>
        {
            if (request.Status is not null && !ValidStatuses.Contains(request.Status))
            {
                return Results.BadRequest(new { error = "Status must be one of: pending, in-progress, completed" });
            }

            if (request.UserId is not null && store.GetUserById(request.UserId.Value) is null)
            {
                return Results.BadRequest(new { error = "User not found" });
            }

            var title = request.Title is not null ? request.Title.Trim() : null;
            var updatedTask = store.UpdateTask(id, title, request.Status, request.UserId);

            if (updatedTask is null)
            {
                return Results.NotFound(new { error = $"Task with id {id} not found" });
            }

            return Results.Ok(updatedTask);
        });
    }
}
