using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Application.Services;

public class AIPromptService : IAIPromptService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(?<key>[a-zA-Z0-9_]+)\}\}", RegexOptions.Compiled);

    public string Render(string template, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return variables.TryGetValue(key, out var value) && value is not null
                ? value
                : string.Empty;
        });
    }
}
