namespace DotnetBackend.Endpoints;

public static class UtilityEndpoints
{
    public static void MapUtilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/test")
            .WithTags("Utility");

        // Deliberately throws an unhandled exception to demonstrate the 500 error middleware.
        group.MapGet("/unhandled-exception", () =>
        {
            throw new InvalidOperationException("This is a simulated unhandled exception.");
        });
    }
}
