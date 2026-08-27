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
    private readonly AiVersionHistoryStore _historyStore;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, PromptTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    public FilePromptTemplateService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "ai-prompts.json");
        _historyStore = new AiVersionHistoryStore(environment);
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

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> GetHistoryAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_historyStore.GetPromptHistory(key));

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
        _historyStore.CapturePromptSnapshot(template, "api_upsert");
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
            new PromptTemplate("question_generation", "v8", "Question generation for FE/PE", "You generate high-quality assessment questions for FPT University programming courses. Return strict JSON only. Every question must be directly grounded in the provided chunks and aligned with the rubric. Never use placeholders, generic filler, or broad textbook questions that are not anchored to the source.", "Subject: {{subject}}\nDifficulty: {{difficulty}}\nQuestionType: {{question_type}}\nCount: {{count}}\nSubject-specific focus:\n{{subject_focus}}\nRubric:\n{{rubric_json}}\nContext chunks:\n{{chunks_json}}\nRevision feedback: {{revision_feedback}}\nPrevious questions: {{previous_questions_json}}\n\nReturn ONLY a JSON array.\n\nGrounding rules:\n- Use only concepts, facts, code patterns, terminology, examples, and constraints explicitly supported by the chunks.\n- Each question must target the actual lesson content in the chunks, not generic knowledge about the subject.\n- Build the item around specific chunk evidence such as a definition, code behavior, comparison, rule, example, algorithm step, data-structure operation, or implementation constraint.\n- source_chunk_ids must contain only ids that exist in the input chunks and must point to the chunks that truly support the item.\n- If the chunks do not support enough information for a high-quality item, generate fewer but better grounded questions.\n- The final question must be self-contained for the learner. The learner should not need to know which slide, page, document, chunk, chapter, or note the content came from.\n\nForbidden output patterns:\n- generic titles such as \"FE Question 1\", \"PE Question 2\", \"Question about variables\", or \"Array question\"\n- generic descriptions such as \"Based on the chunk content...\"\n- meta-source phrasing such as \"according to the slides\", \"according to the page\", \"from the document\", \"in the chunk\", \"in the text above\", or similar references to the storage container of the knowledge\n- fake options such as \"Distractor option 1\" or \"Correct statement about ...\"\n- explanations that merely restate the answer without chunk-based reasoning\n- questions that drift into topics not present in the chunks\n\nIf QuestionType = FE, each object must be exactly:\n{\n  \"type\": \"FE\",\n  \"topic_tags\": [\"...\"],\n  \"difficulty\": \"Easy|Medium|Hard\",\n  \"title\": \"...\",\n  \"description\": \"...\",\n  \"source_chunk_ids\": [\"chunk-...\"],\n  \"skeleton_code\": null,\n  \"solution_code\": null,\n  \"test_cases\": null,\n  \"options\": [\"A. ...\", \"B. ...\", \"C. ...\", \"D. ...\"],\n  \"correct_answer\": [\"A\"],\n  \"explanation\": \"...\"\n}\n\nFE quality rules:\n- The title must name the exact concept or behavior being tested.\n- The description must ask one concrete question answerable from the cited chunks.\n- Provide exactly 4 options.\n- Exactly 1 option must be correct.\n- Wrong options must be plausible misconceptions about the same chunk-grounded concept.\n- Do not create trivial distractors that can be eliminated without understanding the chunk.\n- The wording must sound like a standalone exam question, not like a comment about where the source was found.\n- correct_answer must contain option labels only, for example [\"A\"]. Never return the full option text.\n- The explanation must justify why the correct option is correct and why the distractors are wrong using chunk-grounded reasoning.\n\nIf QuestionType = PE, each object must be exactly:\n{\n  \"type\": \"PE\",\n  \"topic_tags\": [\"...\"],\n  \"difficulty\": \"Easy|Medium|Hard\",\n  \"title\": \"...\",\n  \"description\": \"...\",\n  \"source_chunk_ids\": [\"chunk-...\"],\n  \"skeleton_code\": [\n    {\n      \"filename\": \"Main.java or main.c\",\n      \"content\": \"...\",\n      \"is_readonly\": false\n    }\n  ],\n  \"solution_code\": [\n    {\n      \"filename\": \"Main.java or main.c\",\n      \"content\": \"...\"\n    }\n  ],\n  \"test_cases\": [\n    {\n      \"input\": \"...\",\n      \"expected_output\": \"...\",\n      \"is_hidden\": false\n    }\n  ],\n  \"options\": null,\n  \"correct_answer\": null,\n  \"explanation\": null\n}\n\nPE quality rules:\n- The task must be implementable from the chunks and must not require hidden APIs or external knowledge.\n- The title and description must describe a concrete coding task derived from the chunk content, not a vague programming theme.\n- The wording must be self-contained and must not refer to slides, pages, chapters, documents, chunks, or text above.\n- The description must clearly imply the intended behavior checked by the tests.\n- skeleton_code must show a realistic starter structure in the correct language.\n- solution_code must solve exactly the same task as the description.\n- test_cases must match the described behavior and include at least 2 cases with at least 1 visible case.\n- Do not return empty code blocks, TODO-only placeholders, or unrelated boilerplate.\n- The question should feel like a small but meaningful lab or exam task, not a copied bullet point from lecture notes.\n- Avoid titles and descriptions that only restate a raw operator, keyword, or syntax form without learner-facing task framing.\n- Prefer a concrete behavior or tiny scenario that makes the learner apply the concept, while staying fully grounded in the chunks.\n- For Easy PE, direct tasks are allowed, but they should still test understanding rather than pure copying.\n- Include tests that do more than repeat the same pattern with different numbers when the chunk supports richer checking.\n- If the rubric includes quality_exemplars, use them as style anchors for good assessment quality, but do not copy them literally.\n- Match the level of concreteness, behavioral clarity, and pedagogical value shown by the exemplars while staying grounded in the provided chunks.\n- If the rubric includes few_shot_examples, use them as schema-and-quality references for PE structure, title style, description depth, starter code shape, and test coverage.\n- Never copy source_chunk_ids, titles, descriptions, code, or tests literally from the few-shot examples. Adapt the pattern to the actual chunk content.\n\nDifficulty rules:\n- Follow the rubric, especially the difficulty definitions and what each level should avoid.\n- Easy must remain direct and single-concept.\n- Medium should require short reasoning or multi-step implementation within course scope.\n- Hard should require richer reasoning but still remain within the chunk-supported course scope.\n\nIf revision_feedback is not \"none\", fix the reported issues while staying grounded in the same chunks.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("question_generation_repair", "v1", "Repair malformed question generation output", "You repair a previously generated question set into the exact JSON schema required by the assessment pipeline. Return JSON only. Preserve the intended question idea, but fix schema errors, missing fields, and naming mismatches.", "Subject: {{subject}}\nDifficulty: {{difficulty}}\nQuestionType: {{question_type}}\nSubject-specific focus:\n{{subject_focus}}\nRubric:\n{{rubric_json}}\nContext chunks:\n{{chunks_json}}\nNormalization issues:\n{{normalization_issues}}\nBroken candidate response:\n{{candidate_response}}\n\nReturn ONLY a strict JSON array using the target schema.\n\nRepair rules:\n- Keep the core question grounded in the provided chunks.\n- Fix field names to the exact schema required by the pipeline.\n- For FE, output options, correct_answer labels, and explanation.\n- For PE, output skeleton_code, solution_code, and test_cases explicitly.\n- For PE, include at least 2 test cases and at least 1 visible case.\n- Use source_chunk_ids that exist in the provided chunks.\n- Remove meta-source wording such as slide/page/document/chunk references.\n- Do not add markdown fences or commentary.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("question_review", "v7", "Question review", "You review generated questions for schema validity, grounding quality, academic usefulness, rubric alignment, and subject-scope discipline. Return strict JSON only. Reject generic, weak, unsupported, off-scope, or meta-source-dependent questions.", "Subject: {{subject}}\nQuestionType: {{question_type}}\nSubject-specific focus:\n{{subject_focus}}\nRubric:\n{{rubric_json}}\nSource chunks:\n{{chunks_json}}\nQuestions:\n{{questions_json}}\n\nReturn strict JSON only:\n{\n  \"reviewStatus\": \"accepted|needs_revision\",\n  \"issues\": [\"...\"],\n  \"suggestions\": [\"...\"],\n  \"score\": 0.0,\n  \"schemaValid\": true,\n  \"contentGrounded\": true,\n  \"needsRevision\": false\n}\n\nReview rules:\n- reviewStatus must be \"accepted\" only if the set is schema-valid, chunk-grounded, non-generic, instructionally useful, self-contained for the learner, and consistent with the rubric.\n- schemaValid must be false if any required field is missing, malformed, mislabeled, or inconsistent with the requested question type.\n- contentGrounded must be false if any claim, answer, code requirement, or explanation is not supported by the cited chunks.\n- needsRevision must be true whenever there is any serious issue.\n- score must reflect overall quality from 0.0 to 1.0.\n\nReject the set if any of these problems appear:\n- generic titles like \"FE Question 1\" or broad titles disconnected from the actual chunk lesson\n- vague descriptions like \"Based on the chunk content...\"\n- meta-source wording like \"according to the slides\", \"from the page\", \"in the document\", \"in the chunk\", or similar references that require the learner to know the source container\n- fake distractors such as \"Distractor option 1\"\n- correct_answer contains full option text instead of labels for FE\n- options are trivial, duplicated, or not plausible misconceptions\n- source_chunk_ids reference missing chunks or chunks that do not actually support the item\n- the question drifts outside the course scope defined by the chunks and rubric\n- PE description, skeleton_code, solution_code, and test_cases do not align with each other\n- PE test cases do not verify the stated behavior\n- PE is technically valid but too trivial, too copied from the chunk wording, or too weak to be a meaningful assessment item\n- PE lacks a clear learner-facing task frame, explicit behavior, or useful test coverage\n- PE falls below the quality level implied by the rubric exemplars in clarity, pedagogical value, or test strength\n- repeated questions that test the same fact with only superficial wording changes\n- the requested difficulty does not match the actual reasoning or implementation burden\n\nBe strict. If uncertain, prefer needs_revision.", true, DateTimeOffset.UtcNow),
            new PromptTemplate("code_mentor", "v4", "Code mentor review", "You are an academic programming mentor for PE programming submissions only. Review the student's submitted code against the problem statement. Be strict, concrete, and actionable. Do not invent compile or runtime results you cannot infer. Return JSON only.", "QuestionType: PE\nSubject: {{subject}}\nLanguage: {{language}}\nMentor policy:\n{{mentor_policy_json}}\nRubric:\n{{mentor_rubric_json}}\nProblem:\n{{problem}}\nSource files JSON:\n{{source_files_json}}\nCombined code view:\n{{code}}\n\nReturn strict JSON only:\n{\n  \"question_type\": \"PE\",\n  \"verdict\": \"correct|acceptable_with_minor_notes|needs_fix|incorrect\",\n  \"quality_score\": {\n    \"overall\": 0.0,\n    \"correctness\": 0.0,\n    \"robustness\": 0.0,\n    \"code_quality\": 0.0,\n    \"efficiency\": 0.0,\n    \"confidence\": 0.0\n  },\n  \"performance_summary\": {\n    \"summary\": \"...\",\n    \"time_complexity\": \"...\",\n    \"space_complexity\": \"...\",\n    \"notes\": [\"...\"]\n  },\n  \"error_analysis\": [\n    {\n      \"category\": \"logic|syntax|edge_case|runtime_risk|complexity|style|partial_solution|algorithm_choice\",\n      \"severity\": \"low|medium|high\",\n      \"title\": \"...\",\n      \"detail\": \"...\",\n      \"failing_scenarios\": [\"...\"]\n    }\n  ],\n  \"improvement_suggestions\": [\n    {\n      \"priority\": \"low|medium|high\",\n      \"title\": \"...\",\n      \"detail\": \"...\",\n      \"expected_impact\": \"...\"\n    }\n  ],\n  \"issue_categories\": [\"syntax\",\"logic\"],\n  \"feedback_text\": \"...\",\n  \"suggested_complexity\": \"...\"\n}\n\nRules:\n- Review the submission as potentially multi-file.\n- This mentor flow is PE-only. Do not output FE-oriented feedback.\n- Return every textual field in English only.\n- Do not mix English with Vietnamese, Hindi, or any other language in complexity, summaries, issue details, suggestion details, or notes.\n- Prefer concrete issues over vague criticism.\n- Include failing_scenarios only when a likely failing input or behavior can be inferred from the code and problem.\n- Do not claim the code was executed, compiled, benchmarked, or passed tests.\n- quality_score values must be between 0.0 and 10.0 except confidence which must be between 0.0 and 1.0.\n- performance_summary must stay cautious when complexity cannot be inferred confidently.\n- error_analysis must focus on meaningful defects or risks, not generic style nitpicks.\n- improvement_suggestions must be actionable and specific.\n- feedback_text must be a short human-readable synthesis suitable for storage and quick display.", true, DateTimeOffset.UtcNow)
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
            _historyStore.CapturePromptSnapshot(item, "file_reload");
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
