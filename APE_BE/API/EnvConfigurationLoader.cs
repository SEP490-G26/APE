using Microsoft.Extensions.Configuration;

namespace API.Configuration;

internal static class EnvConfigurationLoader
{
    public static IConfigurationBuilder AddProjectDotEnv(this IConfigurationBuilder builder, string baseDirectory)
    {
        var envPath = ResolveEnvPath(baseDirectory);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AppendStandardProviderEnvironmentMappings(values);

        if (envPath is null || !File.Exists(envPath))
        {
            if (values.Count > 0)
            {
                builder.AddInMemoryCollection(values);
            }

            return builder;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            values[NormalizeKey(key)] = value;
        }

        if (values.Count > 0)
        {
            builder.AddInMemoryCollection(values);
        }

        return builder;
    }

    private static string? ResolveEnvPath(string baseDirectory)
    {
        var candidates = new[]
        {
            Path.Combine(baseDirectory, ".env"),
            Path.Combine(baseDirectory, "..", ".env")
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Replace("__", ":", StringComparison.Ordinal);
        if (normalized.StartsWith("AIProviders:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"AI:Providers:{normalized["AIProviders:".Length..]}";
        }

        return normalized;
    }

    private static void AppendStandardProviderEnvironmentMappings(IDictionary<string, string?> values)
    {
        MapProviderApiKey(values, "OpenAI", "OPENAI_API_KEY");
        MapProviderApiKey(values, "DeepSeek", "DEEPSEEK_API_KEY");
        MapProviderApiKey(values, "Gemini", "GEMINI_API_KEY");
        MapProviderApiKey(values, "Cohere", "COHERE_API_KEY");
    }

    private static void MapProviderApiKey(IDictionary<string, string?> values, string providerName, string envVarName)
    {
        var apiKey = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            values[$"AI:Providers:{providerName}:ApiKey"] = apiKey;
        }
    }
}
