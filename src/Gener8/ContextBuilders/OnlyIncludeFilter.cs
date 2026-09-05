using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Gener8.ContextBuilders;

// The parsed OnlyInclude whitelist.
//
// A path with dots ("Customer.FullName") contributes its first segment as the key and the
// remaining path as a sub-path, which is handed to the auto-generated child DTO for that
// property. A plain name maps to no sub-paths (include the whole property unrestricted).
//
// `None` is the inactive filter: it includes everything, so callers never null-check.
internal sealed class OnlyIncludeFilter
{
    public static readonly OnlyIncludeFilter None = new(null);

    private readonly Dictionary<string, List<string>?>? _paths;

    private OnlyIncludeFilter(Dictionary<string, List<string>?>? paths) => _paths = paths;

    public bool IsActive => _paths is not null;

    public IEnumerable<string> RootNames => _paths is null ? System.Array.Empty<string>() : _paths.Keys;

    public bool Includes(string propertyName) => _paths is null || _paths.ContainsKey(propertyName);

    public IReadOnlyCollection<string>? SubPathsFor(string propertyName)
        => _paths is not null && _paths.TryGetValue(propertyName, out var subPaths) ? subPaths : null;

    // Explicit paths (passed down to an auto-generated child DTO) take precedence over the
    // attribute, which a synthesised DTO does not have.
    public static OnlyIncludeFilter Parse(AttributeData? attribute, IReadOnlyCollection<string>? explicitPaths)
    {
        IReadOnlyCollection<string>? paths = explicitPaths;

        if (paths is null)
        {
            var fromAttribute = AttributeReader.GetStringList(attribute, "OnlyInclude");
            if (fromAttribute.Count > 0) paths = fromAttribute;
        }

        if (paths is null) return None;

        var result = new Dictionary<string, List<string>?>();
        foreach (var path in paths)
        {
            var dotIndex = path.IndexOf('.');

            if (dotIndex < 0)
            {
                // A plain name overrides any previously accumulated sub-paths for the same key.
                result[path] = null;
                continue;
            }

            var head = path.Substring(0, dotIndex);
            var tail = path.Substring(dotIndex + 1);

            if (result.TryGetValue(head, out var existing))
            {
                if (existing is null) continue;  // plain name already set — ignore dotted paths for this key
            }
            else
            {
                result[head] = existing = [];
            }

            existing.Add(tail);
        }

        return result.Count > 0 ? new(result) : None;
    }
}
