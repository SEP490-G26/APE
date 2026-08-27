using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FilePromptTemplateService : IPromptTemplateService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, PromptTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    public FilePromptTemplateService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "ai-prompts.json");
        LoadDefaultsIfNeeded();
        LoadFromFile();
    }

    public Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PromptTemplate>>(_templates.Values.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<PromptTemplate> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (_templates.TryGetValue(key, out var template))
        {
            return Task.FromResult(template);
        }

        throw new InvalidOperationException($"Prompt template '{key}' was not found.");
    }

    public async Task<PromptTemplate> UpsertAsync(PromptTemplateUpdateRequest request, CancellationToken cancellationToken)
    {
        var template = new PromptTemplate(
            request.Key,
            request.Version,
            request.Description,
            request.SystemPrompt,
            request.UserPrompt,
            request.IsActive,
            DateTimeOffset.UtcNow);

        _templates[request.Key] = template;
        await PersistAsync(cancellationToken);
        return template;
    }

    public async Task<PromptRenderResult> RenderAsync(string key, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        var template = await GetAsync(key, cancellationToken);
        return new PromptRenderResult(
            template,
            Render(template.SystemPrompt, variables),
            Render(template.UserPrompt, variables));
    }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        _templates.Clear();
        LoadDefaultsIfNeeded();
        LoadFromFile();
        return Task.CompletedTask;
    }

    private void LoadDefaultsIfNeeded()
    {
        if (File.Exists(_filePath))
        {
            return;
        }

        var defaults = new[]
        {
            new PromptTemplate("gatekeeper", "v2", "Gatekeeper support classification", "You are the whitelist gatekeeper for an academic learning system. Your job is only to decide whether the uploaded academic material belongs to the supported subject whitelist. Be conservative. If the content is too short, too noisy, or mixes multiple supported subjects without a clear primary subject, return ambiguous.", "Supported whitelist:\n{{whitelist_json}}\n\nRequested subject hint: {{subject}}\nLanguage: {{language}}\nFile: {{file_name}}\n\nExtracted content:\n{{content}}\n\nClassify the document into one of: supported, unsupported, ambiguous.\n\nRules:\n- supported: the document clearly belongs to exactly one supported subject area.\n- unsupported: the document is outside the whitelist.\n- ambiguous: the content is insufficient, too noisy, or strongly mixes multiple supported subject areas.\n- Do not infer hidden context beyond the visible content.\n- Focus on academic topic scope, not file extension.\n\nReturn strict JSON only:\n{\n  \"verdict\": \"supported|unsupported|ambiguous\",\n  \"isSupported\": true,\n  \"primaryDomain\": \"C|JAVA_OOP|DSA_JAVA|UNKNOWN\",\n  \"matched_subjects\": [\"C\"],\n  \"confidence\": 0.0,\n  \"reason\": \"...\",\n  \"rejection_reason_code\": null,\n  \"detected_topics\": [\"...\"]\n}", true, DateTimeOffset.UtcNow),
            new PromptTemplate("extract_content_vision", "v2", "Vision extraction for images", "You are an educational document extraction assistant. Your job is to read a single image from a document page and convert its visible educational content into clean markdown. Be faithful to the source. Do not invent text that is not visible.", "ImageRef: {{image_ref}}\n\nExtract the content into markdown.\nRules:\n- Preserve visible headings, bullet points, labels, tables, and code snippets if present.\n- If the image contains a diagram, summarize the diagram meaning in concise markdown.\n- If the image is low quality or partially unreadable, state the uncertainty briefly.\n- Do not wrap the output in markdown code fences.\n- Return markdown only.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("auto_tagging", "v2", "Chunk auto tagging", "You assign topic tags to educational knowledge chunks for retrieval and question generation. Use only the allowed taxonomy. Be conservative and prefer fewer accurate tags over many noisy tags.", "Allowed tags: {{allowed_tags}}\nTagging taxonomy and policy:\n{{taxonomy_json}}\nSubject: {{subject}}\nLanguage: {{language}}\nChunk:\n{{content}}\n\nRules:\n- Use only tags that are semantically supported by the chunk.\n- Return at most 5 tags.\n- Prefer specific topic tags over broad generic tags.\n- Do not invent new tags outside the allowed taxonomy.\n- Return strict JSON only.\n\nReturn:\n{\"topic_tags\":[\"tag1\",\"tag2\"]}", true, DateTimeOffset.UtcNow),
            new PromptTemplate("question_generation", "v2", "Question generation for FE/PE", "You generate programming questions in strict JSON only. Never return markdown fences, prose, comments, or explanations outside JSON. Every field required by the requested schema must be present. Ground every question in the provided chunks.", "Subject: {{subject}}\nDifficulty: {{difficulty}}\nQuestionType: {{question_type}}\nCount: {{count}}\nContext chunks:\n{{chunks_json}}\nRevision feedback: {{revision_feedback}}\nPrevious questions: {{previous_questions_json}}\n\nReturn ONLY a JSON array.\n\nIf QuestionType = FE, each object must be:\n{\n  \"type\": \"FE\",\n  \"topic_tags\": [\"...\"],\n  \"difficulty\": \"Easy|Medium|Hard\",\n  \"title\": \"...\",\n  \"description\": \"...\",\n  \"source_chunk_ids\": [\"chunk-...\"],\n  \"skeleton_code\": null,\n  \"solution_code\": null,\n  \"test_cases\": null,\n  \"options\": [\"A. ...\", \"B. ...\", \"C. ...\", \"D. ...\"],\n  \"correct_answer\": [\"A\"],\n  \"explanation\": \"...\"\n}\n\nIf QuestionType = PE, each object must be:\n{\n  \"type\": \"PE\",\n  \"topic_tags\": [\"...\"],\n  \"difficulty\": \"Easy|Medium|Hard\",\n  \"title\": \"...\",\n  \"description\": \"...\",\n  \"source_chunk_ids\": [\"chunk-...\"],\n  \"skeleton_code\": [\n    {\n      \"filename\": \"Main.java or main.c\",\n      \"content\": \"...\",\n      \"is_readonly\": false\n    }\n  ],\n  \"solution_code\": [\n    {\n      \"filename\": \"Main.java or main.c\",\n      \"content\": \"...\"\n    }\n  ],\n  \"test_cases\": [\n    {\n      \"input\": \"...\",\n      \"expected_output\": \"...\",\n      \"is_hidden\": false\n    }\n  ],\n  \"options\": null,\n  \"correct_answer\": null,\n  \"explanation\": null\n}\n\nRules:\n- Use only information grounded in the provided chunks.\n- source_chunk_ids must reference chunk ids that exist in the input.\n- For FE, correct_answer must contain option labels such as [\"A\"] or [\"B\"].\n- For PE, test_cases must include at least 1 visible case and preferably 1 hidden case.\n- Do not omit fields.\n- Do not return placeholder nulls for required fields.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("question_review", "v2", "Question review", "You review generated questions against source context and rubric. You are strict about schema validity, grounding, and subject alignment. Return JSON only.", "Subject: {{subject}}\nQuestionType: {{question_type}}\nRubric:\n{{rubric_json}}\nSource chunks:\n{{chunks_json}}\nQuestions:\n{{questions_json}}\n\nReview the questions and return strict JSON only:\n{\n  \"reviewStatus\": \"accepted|needs_revision\",\n  \"issues\": [\"...\"],\n  \"suggestions\": [\"...\"],\n  \"score\": 0.0,\n  \"schemaValid\": true,\n  \"contentGrounded\": true,\n  \"needsRevision\": false\n}\n\nRules:\n- reviewStatus must be `accepted` only when schema is valid and the content is grounded.\n- If any required field is missing, schemaValid must be false.\n- If any question is not supported by the chunks, contentGrounded must be false.\n- If schemaValid is false or contentGrounded is false, needsRevision must be true.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("code_mentor", "v3", "Code mentor review", "You are an academic programming mentor for beginner and intermediate students. Review the student's code against the problem statement. Be strict, concrete, and actionable. Do not invent compile or runtime results you cannot infer. Return JSON only.", "Subject: {{subject}}\nLanguage: {{language}}\nMentor policy:\n{{mentor_policy_json}}\nRubric:\n{{mentor_rubric_json}}\nProblem:\n{{problem}}\nSource files JSON:\n{{source_files_json}}\nCombined code view:\n{{code}}\n\nReturn strict JSON only:\n{\n  \"verdict\": \"correct|acceptable_with_minor_notes|needs_fix|incorrect\",\n  \"issue_categories\": [\"syntax\",\"logic\"],\n  \"issues\": [\"...\"],\n  \"suggestions\": [\"...\"],\n  \"failing_scenarios\": [\"...\"],\n  \"confidence\": 0.0,\n  \"complexity\": \"...\"\n}\n\nRules:\n- Review the submission as potentially multi-file.\n- Prefer concrete issues over vague criticism.\n- Include failing_scenarios when a likely failing input can be inferred.\n- Do not claim the code was executed.\n- Keep suggestions actionable and specific.", true, DateTimeOffset.UtcNow)
        };

        File.WriteAllText(_filePath, JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadFromFile()
    {
        var payload = File.ReadAllText(_filePath);
        var items = JsonSerializer.Deserialize<List<PromptTemplate>>(payload) ?? [];
        foreach (var item in items.Where(static item => item.IsActive))
        {
            _templates[item.Key] = item;
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var payload = JsonSerializer.Serialize(_templates.Values.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, payload, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        var output = template;
        foreach (var pair in variables)
        {
            output = output.Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}
