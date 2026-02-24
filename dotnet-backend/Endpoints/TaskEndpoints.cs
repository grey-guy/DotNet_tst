using DotnetBackend.Data;
using DotnetBackend.Models;
using DotnetBackend.Services;

namespace DotnetBackend.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks")
            .WithTags("Tasks")
            .RequireRateLimiting("fixed");

        group.MapGet("/", (string? status, string? userId, DataStore store) =>
        {
            var tasks = store.GetTasks(status, userId);
            return Results.Json(new TasksResponse { Tasks = tasks, Count = tasks.Count });
        })
        .CacheOutput("Tasks");

        group.MapPost("/", (CreateTaskRequest request, DataStore store) =>
        {
            var validation = ValidationService.ValidateCreateTask(
                request.Title, request.Status, request.UserId,
                uid => store.GetUserById(uid) is not null);
            if (!validation.IsValid)
                return Results.BadRequest(new { errors = validation.Errors });

            var task = store.CreateTask(request.Title.Trim(), request.Status, request.UserId);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapPut("/{id}", (int id, UpdateTaskRequest request, DataStore store) =>
        {
            var validation = ValidationService.ValidateUpdateTask(
                request.Title, request.Status, request.UserId,
                uid => store.GetUserById(uid) is not null);
            if (!validation.IsValid)
                return Results.BadRequest(new { errors = validation.Errors });

            var title = request.Title is not null ? request.Title.Trim() : null;
            var updatedTask = store.UpdateTask(id, title, request.Status, request.UserId);

            if (updatedTask is null)
                return Results.NotFound(new { error = $"Task with id {id} not found" });

            return Results.Ok(updatedTask);
        });
    }
}
