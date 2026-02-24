using System.Collections.Concurrent;
using DotnetBackend.Models;
using DotnetBackend.Services;

namespace DotnetBackend.Data;

public class DataStore
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly List<User> _users;
    private readonly List<TaskItem> _tasks;

    public DataStore()
    {
        _users = new List<User>
        {
            new() { Id = 1, Name = "John Doe", Email = "john@example.com", Role = "developer" },
            new() { Id = 2, Name = "Jane Smith", Email = "jane@example.com", Role = "designer" },
            new() { Id = 3, Name = "Bob Johnson", Email = "bob@example.com", Role = "manager" }
        };

        _tasks = new List<TaskItem>
        {
            new() { Id = 1, Title = "Implement authentication", Status = "pending", UserId = 1 },
            new() { Id = 2, Title = "Design user interface", Status = "in-progress", UserId = 2 },
            new() { Id = 3, Title = "Review code changes", Status = "completed", UserId = 3 }
        };
    }

    public List<User> GetUsers()
    {
        _lock.EnterReadLock();
        try
        {
            return _users.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public User? GetUserById(int id)
    {
        _lock.EnterReadLock();
        try
        {
            return _users.FirstOrDefault(u => u.Id == id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public List<TaskItem> GetTasks(string? status, string? userId)
    {
        _lock.EnterReadLock();
        try
        {
            IEnumerable<TaskItem> query = _tasks;

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(userId) && int.TryParse(userId, out var uid))
            {
                query = query.Where(t => t.UserId == uid);
            }

            return query.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public StatsResponse GetStats()
    {
        _lock.EnterReadLock();
        try
        {
            var stats = new StatsResponse
            {
                Users = { Total = _users.Count },
                Tasks = { Total = _tasks.Count }
            };

            foreach (var task in _tasks)
            {
                switch (task.Status)
                {
                    case "pending":
                        stats.Tasks.Pending++;
                        break;
                    case "in-progress":
                        stats.Tasks.InProgress++;
                        break;
                    case "completed":
                        stats.Tasks.Completed++;
                        break;
                }
            }

            return stats;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Architectural Decision: We calculate the next ID dynamically by scanning the collection
    /// instead of storing a separate counter in the class. This ensures reliability and
    /// eliminates potential drift between a counter and the actual data stored,
    /// upholding the ID as a calculated property rather than persistent state.
    /// </summary>
    private static int GetNextId<T>(List<T> items, Func<T, int> idSelector)
    {
        return items.Count > 0 ? items.Max(idSelector) + 1 : 1;
    }

    public User CreateUser(string name, string email, string role)
    {
        var validation = ValidationService.ValidateCreateUser(name, email, role);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors));

        _lock.EnterWriteLock();
        try
        {
            var newId = GetNextId(_users, u => u.Id);
            var user = new User { Id = newId, Name = name, Email = email, Role = role };
            _users.Add(user);
            return user;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public TaskItem CreateTask(string title, string status, int userId)
    {
        var validation = ValidationService.ValidateCreateTask(title, status, userId,
            uid => _users.Any(u => u.Id == uid));
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors));

        _lock.EnterWriteLock();
        try
        {
            var newId = GetNextId(_tasks, t => t.Id);
            var task = new TaskItem { Id = newId, Title = title, Status = status, UserId = userId };
            _tasks.Add(task);
            return task;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public TaskItem? UpdateTask(int id, string? title, string? status, int? userId)
    {
        var validation = ValidationService.ValidateUpdateTask(title, status, userId,
            uid => _users.Any(u => u.Id == uid));
        if (!validation.IsValid)
            throw new ArgumentException(string.Join("; ", validation.Errors));

        _lock.EnterWriteLock();
        try
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null) return null;

            if (title is not null) task.Title = title;
            if (status is not null) task.Status = status;
            if (userId is not null) task.UserId = userId.Value;

            return task;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
