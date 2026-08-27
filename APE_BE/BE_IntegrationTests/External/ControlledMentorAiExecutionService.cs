using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledMentorAiExecutionService : IAIExecutionService
{
    public Task<AITextResponse> ExecuteTextAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        CancellationToken cancellationToken = default)
    {
        var content = ResolveContent(request);

        return Task.FromResult(new AITextResponse
        {
            Content = content,
            Provider = resolved.Provider,
            Model = resolved.Model,
            InputTokens = 120,
            OutputTokens = 80,
            TotalTokens = 200,
            UsageSource = "controlled",
            ReportedCostUsd = 0.01m,
            CostSource = "controlled",
            RawResponse = content
        });
    }

    public Task<AIEmbeddingResponse> ExecuteEmbeddingAsync(
        AIResolvedExecutionOptions resolved,
        AIEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Embedding is not used by the controlled mentor integration tests.");
    }

    private static string ResolveContent(AITextRequest request)
    {
        var prompt = $"{request.FeatureName}\n{request.SystemPrompt}\n{request.UserPrompt}";
        if (prompt.Contains("question_review", StringComparison.OrdinalIgnoreCase))
        {
            return """
            {
              "reviewStatus": "accepted",
              "issues": [],
              "suggestions": ["Controlled review accepted the generated questions."],
              "score": 0.96,
              "schemaValid": true,
              "contentGrounded": true,
              "needsRevision": false
            }
            """;
        }

        if (prompt.Contains("question_generation_repair", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("question_generation", StringComparison.OrdinalIgnoreCase))
        {
            var sourceChunkId = ResolveChunkId(prompt);
            return BuildQuestionSetJson(sourceChunkId);
        }

        return """
        {
          "question_type": "PE",
          "verdict": "needs_fix",
          "quality_score": {
            "overall": 0.82,
            "correctness": 0.8,
            "robustness": 0.78,
            "code_quality": 0.85,
            "efficiency": 0.79,
            "confidence": 0.92
          },
          "performance_summary": {
            "summary": "The solution is close but still needs refinement.",
            "time_complexity": "O(n)",
            "space_complexity": "O(1)",
            "notes": ["Deterministic mentor response"]
          },
          "error_analysis": [
            {
              "category": "Correctness",
              "severity": "medium",
              "title": "Edge handling",
              "detail": "Consider edge cases around empty input.",
              "failing_scenarios": ["empty input"]
            }
          ],
          "improvement_suggestions": [
            {
              "priority": "high",
              "title": "Strengthen validation",
              "detail": "Add guards before processing.",
              "expected_impact": "Improves correctness."
            }
          ],
          "issue_categories": ["Correctness"],
          "feedback_text": "Refine edge-case handling and rerun tests.",
          "suggested_complexity": "O(n)"
        }
        """;
    }

    private static string ResolveChunkId(string prompt)
    {
        foreach (var chunkId in new[]
                 {
                     "64b00000000000000000011c",
                     "64b00000000000000000011b",
                     "64b00000000000000000011d"
                 })
        {
            if (prompt.Contains(chunkId, StringComparison.OrdinalIgnoreCase))
            {
                return chunkId;
            }
        }

        return "64b00000000000000000011b";
    }

    private static string BuildQuestionSetJson(string sourceChunkId)
    {
        var questions = Enumerable.Range(1, 10)
            .Select(index => $$"""
                {
                  "type": "FE",
                  "topic_tags": ["queues", "stacks", "queue_variant_{{index}}"],
                  "difficulty": "Easy",
                  "title": "Controlled FE Queue Basics {{index}}",
                  "description": "Question {{index}}: In a queue workflow variant {{index}}, which operation inserts a new element while preserving FIFO order?",
                  "source_chunk_ids": ["{{sourceChunkId}}"],
                  "options": ["enqueue", "dequeue", "peek", "operation_{{index}}"],
                  "correct_answer": ["enqueue"],
                  "explanation": "Variant {{index}} remains grounded in the queue chunk: enqueue adds a new element at the tail of the queue before later dequeue operations remove items from the front."
                }
                """)
            .ToArray();

        return "{\n  \"questions\": [\n" + string.Join(",\n", questions) + "\n  ]\n}";
    }
}
