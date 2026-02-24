using System.Text.RegularExpressions;
using DotnetBackend.Models;

namespace DotnetBackend.Services;

/// <summary>
/// Centralises all business-rule validation so the rules are enforced consistently
/// whether the callers are HTTP endpoints or the DataStore itself.
/// </summary>
public static class ValidationService
{
    public static readonly string[] ValidStatuses = ["pending", "in-progress", "completed"];
    public static readonly string[] ValidRoles = ["developer", "designer", "manager", "admin"];

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>
    /// Validates fields required to create a user.
    /// </summary>
    public static ValidationResult ValidateCreateUser(string name, string email, string role)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(email))
            errors.Add("Email is required");
        else if (!EmailRegex.IsMatch(email))
            errors.Add("Invalid email format");

        if (string.IsNullOrWhiteSpace(role))
            errors.Add("Role is required");
        else if (!ValidRoles.Contains(role))
            errors.Add("Role must be one of: developer, designer, manager, admin");

        return errors.Count > 0 ? ValidationResult.Fail(errors) : ValidationResult.Ok();
    }

    /// <summary>
    /// Validates fields required to create a task.
    /// <paramref name="userExists"/> is injected so the check works both in HTTP handlers
    /// and inside DataStore without a circular dependency.
    /// </summary>
    public static ValidationResult ValidateCreateTask(
        string title, string status, int userId, Func<int, bool> userExists)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
            errors.Add("Title is required");

        if (string.IsNullOrWhiteSpace(status))
            errors.Add("Status is required");
        else if (!ValidStatuses.Contains(status))
            errors.Add("Status must be one of: pending, in-progress, completed");

        if (userId <= 0)
            errors.Add("UserId is required");
        else if (!userExists(userId))
            errors.Add("User not found");

        return errors.Count > 0 ? ValidationResult.Fail(errors) : ValidationResult.Ok();
    }

    /// <summary>
    /// Validates the optional fields supplied to a partial task update.
    /// </summary>
    public static ValidationResult ValidateUpdateTask(
        string? title, string? status, int? userId, Func<int, bool> userExists)
    {
        var errors = new List<string>();

        if (status is not null && !ValidStatuses.Contains(status))
            errors.Add("Status must be one of: pending, in-progress, completed");

        if (userId is not null && !userExists(userId.Value))
            errors.Add("User not found");

        return errors.Count > 0 ? ValidationResult.Fail(errors) : ValidationResult.Ok();
    }
}
