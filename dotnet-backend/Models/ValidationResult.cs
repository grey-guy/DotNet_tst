namespace DotnetBackend.Models;

public record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Ok() => new([]);
    public static ValidationResult Fail(IEnumerable<string> errors) => new(errors.ToList());
}
