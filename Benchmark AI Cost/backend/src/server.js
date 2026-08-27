import express from "express";
import cors from "cors";
import multer from "multer";
import path from "node:path";
import { config } from "./config.js";
import { MONTHLY_USER_MULTIPLIER } from "./constants/pricing.js";
import { MAX_UPLOAD_SIZE_MB, SUPPORTED_UPLOAD_EXTENSIONS } from "./constants/upload.js";
import { runAllBenchmarks } from "./services/tasks.js";
import { runUploadBenchmark } from "./services/uploadBenchmark.js";
import { runFunctionBenchmarks } from "./services/functionBenchmarks.js";
import {
  benchmarkCodeMentor,
  benchmarkChunkingLogic,
  benchmarkFullFlow,
  benchmarkExtractedContent,
  benchmarkEmbedding,
  benchmarkGatekeeper,
  benchmarkGenReviewFlow,
  listProviderModels,
  modelOptions,
  simulateMonthlyCost,
  testConnections,
} from "./services/aiFunctionFlows.js";
import { openai } from "./services/clients.js";
import {
  createRunId,
  getPrompt,
  getRunReport,
  listRunReports,
  listPrompts,
  savePrompt,
  saveRunReport,
  toRunSummaryCsv,
} from "./services/reportStore.js";

const app = express();

app.use(cors());
app.use(express.json({ limit: "10mb" }));

const upload = multer({
  storage: multer.memoryStorage(),
  limits: {
    fileSize: MAX_UPLOAD_SIZE_MB * 1024 * 1024,
  },
});

const isAllowedUpload = (name = "") => {
  const ext = path.extname(name).toLowerCase();
  return SUPPORTED_UPLOAD_EXTENSIONS.includes(ext);
};

app.get("/api/health", (_req, res) => {
  res.json({ status: "ok" });
});

app.get("/api/benchmarks/upload/supported-types", (_req, res) => {
  res.json({
    maxUploadSizeMb: MAX_UPLOAD_SIZE_MB,
    supportedExtensions: SUPPORTED_UPLOAD_EXTENSIONS,
    notes: [
      "Legacy .doc may need conversion to .docx for best text extraction quality.",
      "Legacy .ppt may need conversion to .pptx for best extraction quality.",
      "Image files are parsed with Gemini Vision and include AI token/cost measurement.",
    ],
  });
});

app.get("/api/providers/openai/diagnostics", async (_req, res) => {
  let models = [];
  let listError = null;
  let embeddingProbe = null;

  try {
    const modelRes = await openai.models.list();
    models = (modelRes?.data || []).map((m) => m.id);
  } catch (error) {
    listError = error?.message || "Failed to list models";
  }

  try {
    const probe = await openai.embeddings.create({
      model: config.openaiEmbeddingModel,
      input: "embedding diagnostics probe",
    });

    embeddingProbe = {
      status: "ok",
      model: config.openaiEmbeddingModel,
      usage: probe.usage || {},
      dimensions: probe?.data?.[0]?.embedding?.length || 0,
    };
  } catch (error) {
    embeddingProbe = {
      status: "failed",
      model: config.openaiEmbeddingModel,
      error: error?.message || "Embedding probe failed",
    };
  }

  res.json({
    baseUrl: config.openaiBaseUrl || "https://api.openai.com/v1",
    configuredChatModel: config.openaiChatModel,
    configuredEmbeddingModel: config.openaiEmbeddingModel,
    modelList: {
      status: listError ? "failed" : "ok",
      error: listError,
      total: models.length,
      models,
    },
    embeddingProbe,
  });
});

app.get("/api/providers/cohere/diagnostics", async (_req, res) => {
  if (!config.cohereApiKey) {
    return res.status(400).json({ error: "COHERE_API_KEY is missing" });
  }

  let embedProbe = null;
  try {
    const response = await fetch(`${config.cohereBaseUrl}/v2/embed`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${config.cohereApiKey}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        model: config.cohereEmbeddingModel,
        input_type: "search_document",
        embedding_types: ["float"],
        texts: ["cohere diagnostics probe"],
      }),
    });

    const raw = await response.text();
    let json = {};
    try {
      json = raw ? JSON.parse(raw) : {};
    } catch {
      json = { raw };
    }

    if (!response.ok) {
      embedProbe = {
        status: "failed",
        model: config.cohereEmbeddingModel,
        error: json?.message || `HTTP ${response.status}`,
      };
    } else {
      embedProbe = {
        status: "ok",
        model: config.cohereEmbeddingModel,
        inputTokens: json?.meta?.billed_units?.input_tokens || 0,
        dimensions: json?.embeddings?.float?.[0]?.length || 0,
      };
    }
  } catch (error) {
    embedProbe = {
      status: "failed",
      model: config.cohereEmbeddingModel,
      error: error?.message || "Cohere diagnostics failed",
    };
  }

  return res.json({
    baseUrl: config.cohereBaseUrl,
    configuredEmbeddingModel: config.cohereEmbeddingModel,
    embedProbe,
  });
});

app.post("/api/benchmarks/run-all", async (_req, res) => {
  const benchmark = await runAllBenchmarks();
  const estimatedCostFor1000UsersMonth = Number(
    (benchmark.totalCostUsd * MONTHLY_USER_MULTIPLIER).toFixed(6)
  );
  const runId = createRunId();
  await saveRunReport({
    ...benchmark,
    runId,
    mode: "estimated_cost",
    estimatedCostFor1000UsersMonth,
  }, { endpoint: "/api/benchmarks/run-all", body: _req.body || {} });

  res.json({
    runId,
    mode: "estimated_cost",
    ...benchmark,
    estimatedCostFor1000UsersMonth,
  });
});

app.post("/api/benchmarks/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) {
      return res.status(400).json({ error: "Please upload a file via form-data key: file" });
    }

    if (!isAllowedUpload(req.file.originalname)) {
      return res.status(400).json({
        error: "Unsupported file type",
        supportedExtensions: SUPPORTED_UPLOAD_EXTENSIONS,
      });
    }

    const result = await runUploadBenchmark(req.file);
    const runId = createRunId();
    await saveRunReport({
      ...result,
      runId,
      mode: "benchmark_upload",
    }, {
      endpoint: "/api/benchmarks/upload",
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
    });
    return res.json({
      runId,
      mode: "benchmark_upload",
      ...result,
    });
  } catch (error) {
    return res.status(500).json({
      error: error?.message || "Failed to benchmark uploaded file",
    });
  }
});

app.get("/api/benchmarks/functions/model-options", (_req, res) => {
  res.json({ ...modelOptions, functions: ["gatekeeper", "extracted_content", "embedding", "generation", "review", "mentor"] });
});

app.post("/api/benchmarks/functions/run", async (req, res) => {
  try {
    const result = await runFunctionBenchmarks({
      selections: req.body?.selections || {},
      credentials: req.body?.credentials || {},
    });
    const runId = createRunId();
    await saveRunReport({ ...result, runId }, {
      endpoint: "/api/benchmarks/functions/run",
      body: req.body || {},
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Failed to run function benchmark" });
  }
});

app.post("/api/connection/test", async (req, res) => {
  try {
    const result = await testConnections({ credentials: req.body?.credentials || {} });
    res.json(result);
  } catch (error) {
    res.status(500).json({ error: error?.message || "Connection test failed" });
  }
});

app.post("/api/providers/models/list", async (req, res) => {
  try {
    const result = await listProviderModels({ credentials: req.body?.credentials || {} });
    res.json(result);
  } catch (error) {
    res.status(500).json({ error: error?.message || "Model listing failed" });
  }
});

app.get("/api/prompts", async (_req, res) => {
  try {
    const prompts = await listPrompts();
    res.json(prompts);
  } catch (error) {
    res.status(500).json({ error: error?.message || "Failed to list prompts" });
  }
});

app.put("/api/prompts/:functionKey", async (req, res) => {
  try {
    const functionKey = String(req.params.functionKey || "").trim();
    const prompt = String(req.body?.prompt || "").trim();
    if (!functionKey) return res.status(400).json({ error: "Missing function key" });
    if (!prompt) return res.status(400).json({ error: "Prompt cannot be empty" });
    const saved = await savePrompt(functionKey, prompt);
    res.json(saved);
  } catch (error) {
    res.status(500).json({ error: error?.message || "Failed to save prompt" });
  }
});

app.post("/api/benchmarks/gatekeeper/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ error: "Missing file" });
    const selection = req.body?.selection ? JSON.parse(req.body.selection) : { provider: "gemini", model: "gemini-3.1-flash-lite" };
    const credentials = req.body?.credentials ? JSON.parse(req.body.credentials) : {};
    const promptTemplate = req.body?.prompt || (await getPrompt("gatekeeper")).prompt;
    const result = await benchmarkGatekeeper({ file: req.file, selection, credentials, promptTemplate });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "gatekeeper", ...result }, {
      endpoint: "/api/benchmarks/gatekeeper/upload",
      selection,
      promptTemplate,
      credentials: {
        openaiBaseUrl: credentials?.openaiBaseUrl || "",
        hasOpenaiApiKey: Boolean(credentials?.openaiApiKey),
        hasGeminiApiKey: Boolean(credentials?.geminiApiKey),
        hasCohereApiKey: Boolean(credentials?.cohereApiKey),
        cohereBaseUrl: credentials?.cohereBaseUrl || "",
      },
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Gatekeeper benchmark failed" });
  }
});

app.post("/api/benchmarks/embedding/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ error: "Missing file" });
    const selection = req.body?.selection ? JSON.parse(req.body.selection) : { provider: "cohere", model: "embed-multilingual-v3.0" };
    const credentials = req.body?.credentials ? JSON.parse(req.body.credentials) : {};
    const taggingSelection = req.body?.taggingSelection
      ? JSON.parse(req.body.taggingSelection)
      : { provider: "cohere", model: "command-r7b-12-2024" };
    const taggingPromptTemplate = req.body?.taggingPrompt || (await getPrompt("embedding_tagging")).prompt;
    const taggingContext = req.body?.taggingContext
      ? JSON.parse(req.body.taggingContext)
      : { subject: "general_programming", language: "general", allowedTags: [] };
    const result = await benchmarkEmbedding({
      file: req.file,
      selection,
      credentials,
      taggingSelection,
      taggingPromptTemplate,
      taggingContext,
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "embedding", ...result }, {
      endpoint: "/api/benchmarks/embedding/upload",
      selection,
      taggingSelection,
      taggingPromptTemplate,
      taggingContext,
      credentials: {
        openaiBaseUrl: credentials?.openaiBaseUrl || "",
        hasOpenaiApiKey: Boolean(credentials?.openaiApiKey),
        hasGeminiApiKey: Boolean(credentials?.geminiApiKey),
        hasCohereApiKey: Boolean(credentials?.cohereApiKey),
        cohereBaseUrl: credentials?.cohereBaseUrl || "",
      },
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Embedding benchmark failed" });
  }
});

app.post("/api/benchmarks/chunking-logic/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ error: "Missing file" });
    const selection = req.body?.selection ? JSON.parse(req.body.selection) : { provider: "cohere", model: "embed-multilingual-v3.0" };
    const visionSelection = req.body?.visionSelection ? JSON.parse(req.body.visionSelection) : { provider: "gemini", model: "gemini-3.5-flash" };
    const visionPromptTemplate = req.body?.visionPrompt || (await getPrompt("extracted_content_vision")).prompt;
    const taggingSelection = req.body?.taggingSelection ? JSON.parse(req.body.taggingSelection) : { provider: "cohere", model: "command-r7b-12-2024" };
    const taggingPromptTemplate = req.body?.taggingPrompt || (await getPrompt("embedding_tagging")).prompt;
    const taggingContext = req.body?.taggingContext
      ? JSON.parse(req.body.taggingContext)
      : { subject: "general_programming", language: "general", allowedTags: [] };
    const processingStrategy = String(req.body?.processingStrategy || "hybrid");
    const credentials = req.body?.credentials ? JSON.parse(req.body.credentials) : {};
    const result = await benchmarkChunkingLogic({
      file: req.file,
      selection,
      visionSelection,
      visionPromptTemplate,
      taggingSelection,
      taggingPromptTemplate,
      taggingContext,
      processingStrategy,
      credentials,
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "chunking_logic", ...result }, {
      endpoint: "/api/benchmarks/chunking-logic/upload",
      selection,
      visionSelection,
      visionPromptTemplate,
      taggingSelection,
      taggingPromptTemplate,
      taggingContext,
      processingStrategy,
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Chunking logic benchmark failed" });
  }
});

app.post("/api/benchmarks/extracted-content/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ error: "Missing file" });
    const selection = req.body?.selection ? JSON.parse(req.body.selection) : { provider: "gemini", model: "gemini-3.5-flash" };
    const credentials = req.body?.credentials ? JSON.parse(req.body.credentials) : {};
    const promptTemplate = req.body?.prompt || (await getPrompt("extracted_content_vision")).prompt;
    const result = await benchmarkExtractedContent({ file: req.file, selection, credentials, promptTemplate });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "extracted_content", ...result }, {
      endpoint: "/api/benchmarks/extracted-content/upload",
      selection,
      promptTemplate,
      credentials: {
        openaiBaseUrl: credentials?.openaiBaseUrl || "",
        hasOpenaiApiKey: Boolean(credentials?.openaiApiKey),
        hasGeminiApiKey: Boolean(credentials?.geminiApiKey),
        hasCohereApiKey: Boolean(credentials?.cohereApiKey),
        cohereBaseUrl: credentials?.cohereBaseUrl || "",
      },
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Extracted content benchmark failed" });
  }
});

app.post("/api/benchmarks/gen-review/run", async (req, res) => {
  try {
    const generationSelection = req.body?.generationSelection || { provider: "gemini", model: "gemini-3.5-flash" };
    const reviewSelection = req.body?.reviewSelection || { provider: "gemini", model: "gemini-3.5-flash" };
    const credentials = req.body?.credentials || {};
    const generationPromptTemplate = req.body?.generationPrompt || (await getPrompt("generation")).prompt;
    const reviewPromptTemplate = req.body?.reviewPrompt || (await getPrompt("review")).prompt;
    const result = await benchmarkGenReviewFlow({
      input: {
        ...(req.body?.input || {}),
        generationPromptTemplate,
        reviewPromptTemplate,
      },
      generationSelection,
      reviewSelection,
      credentials,
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "gen_review", ...result }, {
      endpoint: "/api/benchmarks/gen-review/run",
      body: {
        input: req.body?.input || {},
        generationSelection,
        reviewSelection,
        generationPromptTemplate,
        reviewPromptTemplate,
        credentials: {
          openaiBaseUrl: credentials?.openaiBaseUrl || "",
          hasOpenaiApiKey: Boolean(credentials?.openaiApiKey),
          hasGeminiApiKey: Boolean(credentials?.geminiApiKey),
          hasCohereApiKey: Boolean(credentials?.cohereApiKey),
          cohereBaseUrl: credentials?.cohereBaseUrl || "",
        },
      },
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Gen/Review benchmark failed" });
  }
});

app.post("/api/benchmarks/mentor/run", async (req, res) => {
  try {
    const selection = req.body?.selection || { provider: "openai", model: "gpt-4o-mini" };
    const credentials = req.body?.credentials || {};
    const mentorPromptTemplate = req.body?.mentorPrompt || (await getPrompt("code_mentor")).prompt;
    const result = await benchmarkCodeMentor({
      input: { ...(req.body?.input || {}), mentorPromptTemplate },
      selection,
      credentials,
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "code_mentor", ...result }, {
      endpoint: "/api/benchmarks/mentor/run",
      body: {
        input: req.body?.input || {},
        selection,
        mentorPromptTemplate,
        credentials: {
          openaiBaseUrl: credentials?.openaiBaseUrl || "",
          hasOpenaiApiKey: Boolean(credentials?.openaiApiKey),
          hasGeminiApiKey: Boolean(credentials?.geminiApiKey),
          hasCohereApiKey: Boolean(credentials?.cohereApiKey),
          cohereBaseUrl: credentials?.cohereBaseUrl || "",
        },
      },
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Code mentor benchmark failed" });
  }
});

app.post("/api/benchmarks/full-flow/upload", upload.single("file"), async (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ error: "Missing file" });
    const credentials = req.body?.credentials ? JSON.parse(req.body.credentials) : {};
    const gatekeeperSelection = req.body?.gatekeeperSelection
      ? JSON.parse(req.body.gatekeeperSelection)
      : { provider: "gemini", model: "gemini-3.1-flash-lite" };
    const embeddingSelection = req.body?.embeddingSelection
      ? JSON.parse(req.body.embeddingSelection)
      : { provider: "cohere", model: "embed-multilingual-v3.0" };
    const taggingSelection = req.body?.taggingSelection
      ? JSON.parse(req.body.taggingSelection)
      : { provider: "cohere", model: "command-r7b-12-2024" };
    const generationSelection = req.body?.generationSelection
      ? JSON.parse(req.body.generationSelection)
      : { provider: "gemini", model: "gemini-3.5-flash" };
    const reviewSelection = req.body?.reviewSelection
      ? JSON.parse(req.body.reviewSelection)
      : { provider: "gemini", model: "gemini-3.5-flash" };
    const mentorSelection = req.body?.mentorSelection
      ? JSON.parse(req.body.mentorSelection)
      : { provider: "openai", model: "gpt-4o-mini" };
    const generationInput = req.body?.generationInput ? JSON.parse(req.body.generationInput) : {};
    const mentorInput = req.body?.mentorInput ? JSON.parse(req.body.mentorInput) : {};
    const prompts = req.body?.prompts
      ? JSON.parse(req.body.prompts)
      : {
          gatekeeper: (await getPrompt("gatekeeper")).prompt,
          embedding_tagging: (await getPrompt("embedding_tagging")).prompt,
          generation: (await getPrompt("generation")).prompt,
          review: (await getPrompt("review")).prompt,
          code_mentor: (await getPrompt("code_mentor")).prompt,
        };

    const result = await benchmarkFullFlow({
      file: req.file,
      credentials,
      gatekeeperSelection,
      embeddingSelection,
      taggingSelection,
      generationSelection,
      reviewSelection,
      mentorSelection,
      prompts,
      generationInput,
      mentorInput,
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "full_flow", ...result }, {
      endpoint: "/api/benchmarks/full-flow/upload",
      file: req.file ? { name: req.file.originalname, type: req.file.mimetype, sizeBytes: req.file.size } : null,
      prompts,
      selections: { gatekeeperSelection, embeddingSelection, taggingSelection, generationSelection, reviewSelection, mentorSelection },
      generationInput,
      mentorInput,
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Full flow benchmark failed" });
  }
});

app.post("/api/benchmarks/full-flow/simulate", async (req, res) => {
  try {
    const result = simulateMonthlyCost({
      students: Number(req.body?.students || 500),
      assumptions: req.body?.assumptions || {},
      modelPlan: req.body?.modelPlan || {},
    });
    const runId = createRunId();
    await saveRunReport({ runId, mode: "full_flow_simulation", ...result }, {
      endpoint: "/api/benchmarks/full-flow/simulate",
      body: req.body || {},
    });
    res.json({ runId, ...result });
  } catch (error) {
    res.status(500).json({ error: error?.message || "Simulation failed" });
  }
});

app.get("/api/reports/runs", async (_req, res) => {
  const runs = await listRunReports();
  const mode = _req.query?.mode;
  const filtered = mode ? runs.filter((r) => r.mode === mode) : runs;
  res.json({ total: filtered.length, runs: filtered });
});

app.get("/api/reports/runs/:runId.json", async (req, res) => {
  try {
    const report = await getRunReport(req.params.runId);
    res.setHeader("Content-Type", "application/json");
    res.setHeader("Content-Disposition", `attachment; filename=\"${req.params.runId}.json\"`);
    res.send(JSON.stringify(report, null, 2));
  } catch {
    res.status(404).json({ error: "Run report not found" });
  }
});

app.get("/api/reports/runs/:runId.csv", async (req, res) => {
  try {
    const report = await getRunReport(req.params.runId);
    const csv = toRunSummaryCsv(report);
    res.setHeader("Content-Type", "text/csv; charset=utf-8");
    res.setHeader("Content-Disposition", `attachment; filename=\"${req.params.runId}.csv\"`);
    res.send(csv);
  } catch {
    res.status(404).json({ error: "Run report not found" });
  }
});

app.get("/api/reports/runs/:runId.md", async (req, res) => {
  try {
    const report = await getRunReport(req.params.runId);
    const markdown = report?.parsedMarkdown || "";
    res.setHeader("Content-Type", "text/markdown; charset=utf-8");
    res.setHeader("Content-Disposition", `attachment; filename=\"${req.params.runId}.md\"`);
    res.send(markdown);
  } catch {
    res.status(404).json({ error: "Run report not found" });
  }
});

app.get("/api/reports/runs/:runId.chunks.json", async (req, res) => {
  try {
    const report = await getRunReport(req.params.runId);
    const payload = {
      runId: report.runId,
      mode: report.mode,
      file: report.file || null,
      chunks: report.knowledgeChunks || report.chunkedMarkdown || [],
    };
    res.setHeader("Content-Type", "application/json");
    res.setHeader("Content-Disposition", `attachment; filename=\"${req.params.runId}.chunks.json\"`);
    res.send(JSON.stringify(payload, null, 2));
  } catch {
    res.status(404).json({ error: "Run report not found" });
  }
});

app.get("/api/reports/runs/:runId", async (req, res) => {
  try {
    const report = await getRunReport(req.params.runId);
    res.json(report);
  } catch {
    res.status(404).json({ error: "Run report not found" });
  }
});

app.use((req, res) => {
  res.status(404).json({ error: "Not found" });
});

app.use((error, _req, res, _next) => {
  if (error?.name === "MulterError") {
    return res.status(400).json({ error: error.message });
  }

  const message = error?.message || "Internal server error";
  return res.status(500).json({ error: message });
});

app.listen(config.port, () => {
  console.log(`Benchmark API listening on http://localhost:${config.port}`);
});
