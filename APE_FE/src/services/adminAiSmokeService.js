import { http } from "./http";

const STORAGE_KEY = "ape.admin.ai.smoke.flow";

function loadFlowState() {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function saveFlowState(nextState) {
  window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(nextState));
}

function splitCsv(value) {
  if (Array.isArray(value)) {
    return value.filter(Boolean);
  }

  return String(value || "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function toNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function toRuntimeOverride(value) {
  if (!value) {
    return null;
  }

  if (typeof value === "object") {
    return value;
  }

  const model = String(value).trim();
  return model ? { model } : null;
}

function unwrapApiData(payload) {
  return payload?.data ?? payload;
}

function inferCodeLanguage(question, fallback) {
  const files = [...(question?.solutionCode || []), ...(question?.skeletonCode || [])];
  const hasC = files.some((file) => file?.filename?.toLowerCase().endsWith(".c"));
  if (hasC) {
    return "c";
  }

  const hasJava = files.some((file) => file?.filename?.toLowerCase().endsWith(".java"));
  if (hasJava) {
    return "java";
  }

  return fallback || "java";
}

function buildMeta(parsed = {}) {
  return {
    provider: parsed.provider || parsed.embedding_usage?.provider || parsed.tagging_usage?.provider || "",
    configured_model: parsed.configured_model || parsed.embedding_usage?.configured_model || parsed.tagging_usage?.configured_model || "",
    effective_model: parsed.effective_model || parsed.embedding_usage?.effective_model || parsed.tagging_usage?.effective_model || "",
    total_tokens: parsed.total_tokens || parsed.embedding_usage?.tokens_used || parsed.metrics?.total_reported_cost_usd || parsed.metrics?.total_tokens || "",
    cost_usd: parsed.cost_usd || parsed.embedding_usage?.cost_usd || parsed.metrics?.total_reported_cost_usd || "",
    fallback_used: parsed.fallback_used || parsed.embedding_usage?.fallback_used || parsed.tagging_usage?.fallback_used || false,
    fallback_from_provider: parsed.fallback_from_provider || parsed.embedding_usage?.fallback_from_provider || parsed.tagging_usage?.fallback_from_provider || "",
    fallback_from_model: parsed.fallback_from_model || parsed.embedding_usage?.fallback_from_model || parsed.tagging_usage?.fallback_from_model || "",
    fallback_reason_code: parsed.fallback_reason_code || parsed.embedding_usage?.fallback_reason_code || parsed.tagging_usage?.fallback_reason_code || ""
  };
}

function normalizeChunkForRequest(chunk) {
  return {
    chunkId: chunk.id || chunk.chunkId || "",
    documentId: chunk.documentId || "",
    chunkIndex: chunk.chunkIndex || 0,
    sectionTitle: chunk.sectionTitle || "",
    chapterKey: chunk.chapterKey || "",
    sourcePageFrom: chunk.sourcePageFrom ?? null,
    sourcePageTo: chunk.sourcePageTo ?? null,
    topicTags: chunk.topicTags || [],
    selectionReasons: chunk.selectionReasons || [],
    tokenCount: chunk.tokenCount || 0,
    topicScore: chunk.topicScore || 0,
    lexicalScore: chunk.lexicalScore || 0,
    sectionScore: chunk.sectionScore || 0,
    finalScore: chunk.finalScore || 0,
    selectionRole: chunk.selectionRole || "primary",
    contentText: chunk.rawText || chunk.contentText || chunk.markdownText || chunk.normalizedText || ""
  };
}

function normalizeParsed(kind, data) {
  switch (kind) {
    case "gatekeeper":
      return {
        is_supported: data.isSupported ?? false,
        primary_domain: data.primaryDomain || "",
        reason: data.reason || "",
        verdict: data.verdict || "",
        confidence: data.confidence ?? 0,
        rejection_reason_code: data.rejectionReasonCode || "",
        matched_subjects: data.matchedSubjects || [],
        detected_topics: data.detectedTopics || [],
        provider: data.provider || "",
        model: data.model || "",
        configured_model: data.configuredModel || "",
        effective_model: data.effectiveModel || "",
        input_tokens: data.inputTokens ?? 0,
        output_tokens: data.outputTokens ?? 0,
        total_tokens: data.totalTokens ?? 0,
        cost_usd: data.costUsd ?? 0,
        usage_source: data.usageSource || "",
        cost_source: data.costSource || "",
        fallback_used: data.fallbackUsed || false,
        fallback_from_provider: data.fallbackFromProvider || "",
        fallback_from_model: data.fallbackFromModel || "",
        fallback_reason_code: data.fallbackReasonCode || ""
      };
    case "extracted":
      return {
        normalized_markdown: data.normalizedMarkdown || data.content || "",
        used_ai_normalization: data.usedAiNormalization || false,
        provider: data.provider || "",
        model: data.model || "",
        configured_model: data.configuredModel || "",
        effective_model: data.effectiveModel || "",
        usage_source: data.usageSource || "",
        cost_source: data.costSource || "",
        input_tokens: data.inputTokens ?? 0,
        output_tokens: data.outputTokens ?? 0,
        total_tokens: data.totalTokens ?? 0,
        cost_usd: data.costUsd ?? 0,
        fallback_used: data.fallbackUsed || false,
        fallback_from_provider: data.fallbackFromProvider || "",
        fallback_from_model: data.fallbackFromModel || "",
        fallback_reason_code: data.fallbackReasonCode || "",
        parser_name: data.parserName || "",
        vision_model: data.visionModel || "",
        extraction_mode: data.extractionMode || "",
        word_count: data.wordCount ?? 0,
        detected_image_references: data.detectedImageReferences || [],
        warnings: data.warnings || []
      };
    case "embedding":
      return {
        chunks: (data.chunks || []).map((chunk) => ({
          chunk_id: chunk.id || "",
          topic_tags: chunk.topicTags || [],
          token_count: chunk.tokenCount ?? 0,
          section_title: chunk.sectionTitle || "",
          chapter_key: chunk.chapterKey || ""
        })),
        embedding_usage: {
          provider: data.embeddingUsage?.provider || "",
          configured_model: data.embeddingUsage?.configuredModel || "",
          effective_model: data.embeddingUsage?.effectiveModel || "",
          normalized_model_key: data.embeddingUsage?.normalizedModelKey || "",
          tokens_used: data.embeddingUsage?.tokensUsed ?? 0,
          cost_usd: data.embeddingUsage?.costUsd ?? 0,
          usage_source: data.embeddingUsage?.usageSource || "",
          cost_source: data.embeddingUsage?.costSource || "",
          fallback_used: data.embeddingUsage?.fallbackUsed || false,
          fallback_from_provider: data.embeddingUsage?.fallbackFromProvider || "",
          fallback_from_model: data.embeddingUsage?.fallbackFromModel || "",
          fallback_reason_code: data.embeddingUsage?.fallbackReasonCode || ""
        },
        tagging_usage: {
          provider: data.taggingUsage?.provider || "",
          configured_model: data.taggingUsage?.configuredModel || "",
          effective_model: data.taggingUsage?.effectiveModel || "",
          normalized_model_key: data.taggingUsage?.normalizedModelKey || "",
          call_count: data.taggingUsage?.callCount ?? 0,
          successful_calls: data.taggingUsage?.successfulCalls ?? 0,
          fallback_calls: data.taggingUsage?.fallbackCalls ?? 0,
          input_tokens: data.taggingUsage?.inputTokens ?? 0,
          output_tokens: data.taggingUsage?.outputTokens ?? 0,
          total_tokens: data.taggingUsage?.totalTokens ?? 0,
          cost_usd: data.taggingUsage?.costUsd ?? 0,
          usage_source: data.taggingUsage?.usageSource || "",
          cost_source: data.taggingUsage?.costSource || "",
          fallback_used: data.taggingUsage?.fallbackUsed || false,
          fallback_from_provider: data.taggingUsage?.fallbackFromProvider || "",
          fallback_from_model: data.taggingUsage?.fallbackFromModel || "",
          fallback_reason_code: data.taggingUsage?.fallbackReasonCode || ""
        }
      };
    case "retrievalPlan":
      return {
        source_scope: data.contextPack?.sourceScope || "",
        course_id: data.contextPack?.courseId || "",
        document_id: data.contextPack?.documentId || "",
        chapter_key: data.contextPack?.dominantChapterKey || "",
        subject: data.subject || "",
        question_type: data.questionType || "",
        difficulty: data.difficulty || "",
        covered_topics: data.contextPack?.coveredTopics || [],
        uncovered_topics: data.contextPack?.uncoveredTopics || [],
        topic_coverage_ratio: data.contextPack?.topicCoverageRatio ?? 0,
        dominant_chapter_key: data.contextPack?.dominantChapterKey || "",
        retrieved_chunks: data.selectedChunks || [],
        candidate_stats: data.candidateStats || {},
        token_budget: data.tokenBudget || {},
        context_pack: data.contextPack || {}
      };
    case "questionGenerationReview":
      return {
        subject: data.subject || "",
        question_type: data.questionType || "",
        difficulty: data.difficulty || "",
        mode: data.mode || "",
        attempts_used: data.attemptsUsed ?? 0,
        generator_model: data.generatorModel || "",
        reviewer_model: data.reviewerModel || "",
        questions: (data.questions || []).map((question) => ({
          title: question.title || "",
          status: data.review?.reviewStatus || "generated",
          type: question.type || "",
          topic_tags: question.topicTags || []
        })),
        review: data.review || {},
        persistence: data.persistence || {},
        context_pack: data.contextPack || {},
        retrieval_plan: data.retrievalPlan || {},
        metrics: data.metrics || {}
      };
    case "codeMentor":
      return {
        submission_id: data.submissionId || "",
        feedback: data.feedback || {},
        provider: data.provider || "",
        model: data.model || "",
        configured_model: data.configuredModel || "",
        effective_model: data.effectiveModel || "",
        input_tokens: data.inputTokens ?? 0,
        output_tokens: data.outputTokens ?? 0,
        total_tokens: data.totalTokens ?? 0,
        cost_usd: data.costUsd ?? 0,
        reported_cost_usd: data.reportedCostUsd ?? 0,
        charged_vnd: data.chargedVnd ?? 0,
        actual_deducted_vnd: data.actualDeductedVnd ?? 0,
        remaining_balance_vnd: data.remainingBalanceVnd ?? 0,
        usage_source: data.UsageSource || data.usageSource || "",
        cost_source: data.CostSource || data.costSource || "",
        fallback_used: data.fallbackUsed || false,
        fallback_from_provider: data.fallbackFromProvider || "",
        fallback_from_model: data.fallbackFromModel || "",
        fallback_reason_code: data.fallbackReasonCode || "",
        created_at: data.createdAt || ""
      };
    default:
      return data || {};
  }
}

function getGeneratedPeQuestion(flowState) {
  const questions = flowState.questionGenerationReview?.raw?.questions || [];
  return questions.find((question) => String(question?.type || "").toUpperCase() === "PE") || null;
}

function buildRequest(kind, payload, flowState) {
  switch (kind) {
    case "gatekeeper":
      return {
        content: payload.content || "",
        fileName: payload.file_name || "",
        subjectHint: payload.subject_hint || "",
        language: payload.language || "",
        runtimeOverride: toRuntimeOverride(payload.runtime_override)
      };
    case "extracted":
      return {
        fileName: payload.file_name || "smoke.txt",
        rawText: payload.raw_text || "",
        sourceType: payload.source_type || "text",
        isVisionRecommended: Boolean(payload.is_vision_recommended),
        parserName: payload.parser_name || "",
        estimatedPageCount: toNumber(payload.estimated_page_count, 1),
        warnings: splitCsv(payload.warnings),
        runtimeOverride: toRuntimeOverride(payload.runtime_override)
      };
    case "embedding":
      return {
        documentId: payload.document_id || flowState.extracted?.request?.fileName || "",
        courseId: payload.course_id || flowState.gatekeeper?.response?.matchedSubjects?.[0] || "",
        content: payload.content || flowState.extracted?.raw?.normalizedMarkdown || "",
        sourceType: payload.source_type || "text",
        embeddingOverride: toRuntimeOverride(payload.embedding_override),
        taggingOverride: toRuntimeOverride(payload.tagging_override)
      };
    case "retrievalPlan":
      return {
        userId: payload.user_id || "admin-smoke",
        courseId: payload.course_id || flowState.embedding?.request?.courseId || "",
        documentId: payload.document_id || flowState.embedding?.request?.documentId || "",
        chapterKey: payload.chapter_key || "",
        subject: payload.subject || flowState.gatekeeper?.response?.primaryDomain || "",
        questionType: payload.question_type || "FE",
        difficulty: payload.difficulty || "Medium",
        targetTopics: splitCsv(payload.target_topics),
        retrievalQuery: payload.retrieval_query || "",
        maxCandidateCount: toNumber(payload.max_candidate_count, 24),
        maxPackedTokens: toNumber(payload.max_packed_tokens, 2200),
        includeChunkText: Boolean(payload.include_chunk_text)
      };
    case "questionGenerationReview": {
      const embeddingChunks = flowState.embedding?.raw?.chunks || [];
      const retrievalChunks = flowState.retrievalPlan?.raw?.selectedChunks || [];
      const chunks = (embeddingChunks.length > 0 ? embeddingChunks : retrievalChunks).map(normalizeChunkForRequest);

      if (!chunks.length) {
        throw {
          error: "Please run Embedding + AutoTagging first so Generation + Review has chunk data to use.",
          errorCode: "smoke.generation.missing_chunks"
        };
      }

      return {
        userId: "admin-smoke",
        courseId: payload.course_id || flowState.embedding?.request?.courseId || "",
        documentId: payload.document_id || flowState.embedding?.request?.documentId || "",
        subject: payload.subject || flowState.gatekeeper?.response?.primaryDomain || "",
        difficulty: payload.difficulty || "Medium",
        questionType: payload.question_type || "FE",
        count: toNumber(payload.count, 1),
        mode: payload.mode || "review",
        persistQuestions: false,
        suppressPersistenceSideEffects: true,
        chunks,
        generatorOverride: toRuntimeOverride(payload.generator_override),
        reviewerOverride: toRuntimeOverride(payload.reviewer_override)
      };
    }
    case "codeMentor": {
      const generatedPeQuestion = getGeneratedPeQuestion(flowState);
      if (!generatedPeQuestion) {
        throw {
          error: "Code Mentor smoke test can only run after Step 2 generates at least one PE question.",
          errorCode: "smoke.code_mentor.missing_pe_question"
        };
      }

      const submittedCode = generatedPeQuestion.solutionCode?.length
        ? generatedPeQuestion.solutionCode
        : generatedPeQuestion.skeletonCode || [];

      if (!submittedCode.length) {
        throw {
          error: "The generated PE question does not contain code files to evaluate.",
          errorCode: "smoke.code_mentor.missing_code"
        };
      }

      return {
        submissionId: payload.submission_id || `smoke-${Date.now()}`,
        subject: flowState.questionGenerationReview?.request?.subject || "",
        language: inferCodeLanguage(generatedPeQuestion, flowState.gatekeeper?.request?.language),
        questionTitle: generatedPeQuestion.title || "",
        questionDescription: generatedPeQuestion.description || "",
        topicTags: generatedPeQuestion.topicTags || [],
        submittedCode,
        runtimeOverride: toRuntimeOverride(payload.runtime_override)
      };
    }
    default:
      return payload;
  }
}

function mergeInitialState(kind, fields, flowState) {
  const initialState = Object.fromEntries(fields.map((field) => [field.key, field.initialValue]));

  if (kind === "embedding") {
    initialState.content = flowState.extracted?.raw?.normalizedMarkdown || initialState.content;
    initialState.document_id = flowState.extracted?.request?.fileName || initialState.document_id;
    initialState.course_id = flowState.gatekeeper?.response?.matchedSubjects?.[0] || initialState.course_id;
  }

  if (kind === "extracted") {
    initialState.file_name = flowState.gatekeeper?.request?.fileName || initialState.file_name;
    initialState.raw_text = flowState.gatekeeper?.request?.content || initialState.raw_text;
  }

  if (kind === "questionGenerationReview") {
    initialState.course_id = flowState.embedding?.request?.courseId || initialState.course_id;
    initialState.document_id = flowState.embedding?.request?.documentId || initialState.document_id;
    initialState.subject = flowState.gatekeeper?.response?.primaryDomain || initialState.subject;
  }

  if (kind === "codeMentor") {
    const generatedPeQuestion = getGeneratedPeQuestion(flowState);
    if (generatedPeQuestion) {
      initialState.submission_id = flowState.codeMentor?.request?.submissionId || `smoke-${Date.now()}`;
    }
  }

  return initialState;
}

export function getSmokeInitialState(kind, fields) {
  return mergeInitialState(kind, fields, loadFlowState());
}

export async function runSmokeTest(kind, payload) {
  const endpointMap = {
    gatekeeper: "/api/admin/ai/smoke/gatekeeper",
    extracted: "/api/admin/ai/smoke/extracted",
    embedding: "/api/admin/ai/smoke/embedding",
    questionGenerationReview: "/api/admin/ai/smoke/question-generation-review",
    retrievalPlan: "/api/admin/ai/smoke/retrieval-plan",
    codeMentor: "/api/admin/ai/smoke/code-mentor"
  };

  const flowState = loadFlowState();
  const requestPayload = buildRequest(kind, payload, flowState);

  try {
    const response = await http(endpointMap[kind], {
      method: "POST",
      body: JSON.stringify(requestPayload)
    });
    const raw = unwrapApiData(response);
    const parsed = normalizeParsed(kind, raw);
    const normalized = {
      parsed,
      raw,
      metadata: buildMeta(parsed)
    };

    saveFlowState({
      ...flowState,
      [kind]: {
        request: requestPayload,
        raw,
        parsed
      }
    });

    return normalized;
  } catch (error) {
    throw {
      error: error?.error || error?.message || "Smoke test failed.",
      errorCode: error?.errorCode || "smoke.request_failed"
    };
  }
}
