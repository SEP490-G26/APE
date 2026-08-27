using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileAiRunHistoryService : IAiRunHistoryService
{
    private readonly string _rootFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileAiRunHistoryService(IHostEnvironment environment)
    {
        _rootFolder = Path.Combine(environment.ContentRootPath, "App_Data", "run-history");
        Directory.CreateDirectory(_rootFolder);
    }

    public async Task<AiRunHistoryRecord> SaveAsync(
        string functionName,
        string routeKey,
        object request,
        object response,
        CancellationToken cancellationToken)
    {
        var normalizedFunction = Normalize(functionName);
        var folder = Path.Combine(_rootFolder, normalizedFunction);
        Directory.CreateDirectory(folder);

        var runId = $"{normalizedFunction}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..Math.Min(60, $"{normalizedFunction}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".Length)];
        var fileName = $"{runId}.json";
        var summary = new AiRunHistorySummary(
            runId,
            normalizedFunction,
            routeKey,
            DateTimeOffset.UtcNow,
            request.GetType().Name,
            response.GetType().Name,
            ExtractStatus(response),
            ExtractModelSummary(request, response),
            ExtractModelFields(request, response),
            ExtractUsageSource(response),
            ExtractError(response),
            ExtractTotals(response),
            fileName);

        var record = new AiRunHistoryRecord(summary, request, response);
        var filePath = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(record, _jsonOptions), cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<AiRunHistorySummary>> ListAsync(
        string? functionName,
        int take,
        CancellationToken cancellationToken)
    {
        var folders = string.IsNullOrWhiteSpace(functionName)
            ? Directory.Exists(_rootFolder) ? Directory.GetDirectories(_rootFolder) : []
            : [Path.Combine(_rootFolder, Normalize(functionName))];

        var items = new List<AiRunHistorySummary>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                await using var stream = File.OpenRead(file);
                var record = await JsonSerializer.DeserializeAsync<AiRunHistoryRecord>(stream, _jsonOptions, cancellationToken);
                if (record is not null)
                {
                    items.Add(record.Summary);
                }
            }
        }

        return items
            .OrderByDescending(static item => item.RecordedAt)
            .Take(Math.Max(1, take))
            .ToList();
    }

    public async Task<AiRunHistoryRecord> GetAsync(
        string functionName,
        string runId,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_rootFolder, Normalize(functionName), $"{runId}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Run history '{runId}' was not found for function '{functionName}'.");
        }

        await using var stream = File.OpenRead(filePath);
        var record = await JsonSerializer.DeserializeAsync<AiRunHistoryRecord>(stream, _jsonOptions, cancellationToken);
        if (record is null)
        {
            throw new InvalidOperationException($"Run history '{runId}' could not be deserialized.");
        }

        return record;
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');

    private static string? ExtractStatus(object response)
    {
        if (TryReadStringProperty(response, "Verdict", out var verdict))
        {
            return verdict;
        }

        if (TryReadNestedStringProperty(response, "Verdict", "Verdict", out var nestedVerdict))
        {
            return nestedVerdict;
        }

        if (TryReadStringProperty(response, "ReviewStatus", out var reviewStatus))
        {
            return reviewStatus;
        }

        if (TryReadNestedStringProperty(response, "Review", "ReviewStatus", out var nestedReviewStatus))
        {
            return nestedReviewStatus;
        }

        if (TryReadNestedStringProperty(response, "Feedback", "Verdict", out var mentorVerdict))
        {
            return mentorVerdict;
        }

        return null;
    }

    private static string? ExtractModelSummary(object request, object response)
    {
        var requestModelParts = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType == typeof(string) && property.Name.Contains("Model", StringComparison.OrdinalIgnoreCase))
            .Select(property => new
            {
                property.Name,
                Value = property.GetValue(request)?.ToString()
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(static item => $"{item.Name}={item.Value}")
            .ToList();

        if (requestModelParts.Count > 0)
        {
            return string.Join("; ", requestModelParts);
        }

        if (TryReadNestedStringProperty(response, "Verdict", "ModelName", out var verdictModel))
        {
            return verdictModel;
        }

        if (TryReadNestedStringProperty(response, "Feedback", "ModelName", out var feedbackModel))
        {
            return feedbackModel;
        }

        return null;
    }

    private static NormalizedModelFields? ExtractModelFields(object request, object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals) && totals?.UsageCapture is not null)
        {
            var usageProvider = InferProviderFromResponse(response) ?? InferProviderFromRequest(request);
            return BuildModelFieldsFromRequest(request, usageProvider);
        }

        var provider = InferProviderFromResponse(response) ?? InferProviderFromRequest(request);
        var modelFields = BuildModelFieldsFromRequest(request, provider);
        return modelFields;
    }

    private static string? ExtractUsageSource(object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals))
        {
            return totals?.UsageCapture?.UsageSource;
        }

        return null;
    }

    private static NormalizedError? ExtractError(object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals) && totals?.Error is not null)
        {
            return totals.Error;
        }

        return null;
    }

    private static TokenCostBreakdown? ExtractTotals(object response)
    {
        var property = response.GetType().GetProperty("Totals", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(response) as TokenCostBreakdown;
    }

    private static bool TryReadPropertyValue<T>(object source, string propertyName, out T? value)
        where T : class
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        value = property?.GetValue(source) as T;
        return value is not null;
    }

    private static bool TryReadStringProperty(object source, string propertyName, out string value)
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType == typeof(string) && property.GetValue(source) is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadNestedStringProperty(object source, string parentPropertyName, string childPropertyName, out string value)
    {
        var parent = source.GetType().GetProperty(parentPropertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
        if (parent is not null && TryReadStringProperty(parent, childPropertyName, out value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? InferProviderFromRequest(object request)
    {
        var modelProperties = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType == typeof(string) && property.Name.Contains("Model", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.GetValue(request)?.ToString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return modelProperties.Count > 0 ? InferProvider(modelProperties[0]!) : null;
    }

    private static string? InferProviderFromResponse(object response)
    {
        if (TryReadNestedStringProperty(response, "Verdict", "ModelName", out var verdictModel))
        {
            return InferProvider(verdictModel);
        }

        if (TryReadNestedStringProperty(response, "Feedback", "ModelName", out var feedbackModel))
        {
            return InferProvider(feedbackModel);
        }

        return null;
    }

    private static NormalizedModelFields BuildModelFieldsFromRequest(object request, string? provider)
    {
        string? Read(string propertyName) => request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(request)?.ToString();

        return new NormalizedModelFields(
            provider,
            Read("Model"),
            Read("GeneratorModel"),
            Read("ReviewerModel"),
            Read("EmbeddingModel"),
            Read("TaggingModel"),
            Read("VisionModel"),
            Read("MentorModel"));
    }

    private static string InferProvider(string model)
    {
        var lowered = model.ToLowerInvariant();
        return lowered switch
        {
            var value when value.Contains("gpt") || value.Contains("text-embedding") => "openai",
            var value when value.Contains("gemini") => "gemini",
            var value when value.Contains("cohere") || value.Contains("embed") || value.Contains("command") => "cohere",
            _ => "custom"
        };
    }
}
