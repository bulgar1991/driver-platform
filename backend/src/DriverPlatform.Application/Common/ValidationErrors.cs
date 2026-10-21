namespace DriverPlatform.Application.Common;

/// <summary>Collects field errors and throws them together.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool Any => _errors.Count > 0;

    public void Add(string field, string error)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            _errors[field] = list = [];
        }
        list.Add(error);
    }

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new RequestValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
