import fs from "node:fs/promises";
import path from "node:path";

const API_BASE = process.env.BENCH_API_BASE || "http://localhost:3003";
const DATASET_PATH =
  process.env.GENREVIEW_DATASET_PATH ||
  path.resolve(process.cwd(), "..", "Docs", "Benchmark_GroundTruth", "datasets", "master_genreview_from_embedding_case.json");
const OUT_DIR =
  process.env.GENREVIEW_OUT_DIR ||
  path.resolve(process.cwd(), "..", "Docs", "Benchmark_GroundTruth", "results");

const MODELS = [
  { label: "GPT - 4o", provider: "openai", model: "gpt-4o" },
  { label: "GPT - 5.4 Mini", provider: "openai", model: "gpt-5.4-mini" },
  { label: "GPT - 5.4", provider: "openai", model: "gpt-5.4" },
  { label: "GPT - 5.5", provider: "openai", model: "gpt-5.5" },
  { label: "Gemini - 2.5 Flash", provider: "gemini", model: "gemini-2.5-flash" },
  { label: "Gemini - 3.1 Flash", provider: "gemini", model: "gemini-3.1-flash" },
  { label: "Gemini - 3.5 Flash", provider: "gemini", model: "gemini-3.5-flash" },
];

const TEST_CASES = [
  {
    test_case_id: "FE_medium",
    inputPatch: {
      questionType: "multiple_choice",
      difficulty: "Medium",
      count: 3,
      groundTruth: "FE medium: đúng schema QuestionsBank FE, có options rõ ràng, không có code fields.",
    },
  },
  {
    test_case_id: "PE_medium",
    inputPatch: {
      questionType: "question",
      difficulty: "Medium",
      count: 1,
      groundTruth: "PE medium: đúng schema QuestionsBank PE, có skeleton_code, solution_code, test_cases.",
    },
  },
];

const nowStamp = () => new Date().toISOString().replace(/[:.]/g, "-");

const csvEscape = (value) => {
  const s = String(value ?? "");
  if (s.includes(",") || s.includes("\"") || s.includes("\n")) return `"${s.replace(/"/g, "\"\"")}"`;
  return s;
};

const postJson = async (url, body) => {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const raw = await res.text();
  let parsed = {};
  try {
    parsed = raw ? JSON.parse(raw) : {};
  } catch {
    throw new Error(`API non-JSON (${res.status}): ${raw.slice(0, 180)}`);
  }
  if (!res.ok) {
    throw new Error(parsed?.error || `HTTP ${res.status}`);
  }
  return parsed;
};

const buildBaseInput = (dataset) => {
  const questionTypeRaw = String(dataset?.questionType || "FE").toUpperCase();
  const questionType = questionTypeRaw === "PE" ? "question" : "multiple_choice";
  return {
    questionType,
    domain: dataset?.domain || "Java_OOP",
    difficulty: "Medium",
    count: Number(dataset?.count || 3),
    groundTruth: dataset?.groundTruth || "",
    contextChunks: Array.isArray(dataset?.contextChunks) ? dataset.contextChunks : [],
    contextChunksText: Array.isArray(dataset?.contextChunks) ? dataset.contextChunks.join("\n---\n") : "",
  };
};

const main = async () => {
  await fs.mkdir(OUT_DIR, { recursive: true });
  const datasetRaw = await fs.readFile(DATASET_PATH, "utf8");
  const dataset = JSON.parse(datasetRaw);
  const baseInput = buildBaseInput(dataset);

  const pairs = [];
  for (const gen of MODELS) {
    for (const rev of MODELS) {
      pairs.push({ generation: gen, review: rev });
    }
  }

  const totalRuns = pairs.length * TEST_CASES.length;
  const results = [];
  let runIndex = 0;

  for (const tc of TEST_CASES) {
    for (const pair of pairs) {
      runIndex += 1;
      const payload = {
        input: { ...baseInput, ...tc.inputPatch },
        generationSelection: { provider: pair.generation.provider, model: pair.generation.model },
        reviewSelection: { provider: pair.review.provider, model: pair.review.model },
      };

      process.stdout.write(`[${runIndex}/${totalRuns}] ${tc.test_case_id} | GEN=${pair.generation.label} | REV=${pair.review.label}\n`);
      const started = Date.now();
      try {
        const response = await postJson(`${API_BASE}/api/benchmarks/gen-review/run`, payload);
        const row = {
          test_case_id: tc.test_case_id,
          generation_label: pair.generation.label,
          generation_provider: pair.generation.provider,
          generation_model: pair.generation.model,
          review_label: pair.review.label,
          review_provider: pair.review.provider,
          review_model: pair.review.model,
          run_id: response?.runId || "",
          status: "ok",
          input_tokens: Number(response?.totals?.inputTokens || 0),
          output_tokens: Number(response?.totals?.outputTokens || 0),
          input_cost_usd: Number(response?.totals?.inputCostUsd || 0),
          output_cost_usd: Number(response?.totals?.outputCostUsd || 0),
          total_cost_usd: Number(response?.totals?.totalCostUsd || 0),
          latency_ms: Number(response?.totals?.latency_ms || Date.now() - started),
          schema_valid: Boolean(response?.generationSchemaValidation?.valid),
          schema_error_count: Number(response?.generationSchemaValidation?.errors?.length || 0),
          error: "",
        };
        results.push(row);
      } catch (error) {
        results.push({
          test_case_id: tc.test_case_id,
          generation_label: pair.generation.label,
          generation_provider: pair.generation.provider,
          generation_model: pair.generation.model,
          review_label: pair.review.label,
          review_provider: pair.review.provider,
          review_model: pair.review.model,
          run_id: "",
          status: "error",
          input_tokens: 0,
          output_tokens: 0,
          input_cost_usd: 0,
          output_cost_usd: 0,
          total_cost_usd: 0,
          latency_ms: Date.now() - started,
          schema_valid: false,
          schema_error_count: 0,
          error: String(error?.message || error),
        });
      }
    }
  }

  const ts = nowStamp();
  const outJson = path.join(OUT_DIR, `genreview_98cases_${ts}.json`);
  const outCsv = path.join(OUT_DIR, `genreview_98cases_${ts}.csv`);

  const summary = {
    generated_at: new Date().toISOString(),
    total_runs: totalRuns,
    ok_runs: results.filter((r) => r.status === "ok").length,
    error_runs: results.filter((r) => r.status === "error").length,
    total_input_tokens: results.reduce((s, r) => s + r.input_tokens, 0),
    total_output_tokens: results.reduce((s, r) => s + r.output_tokens, 0),
    total_input_cost_usd: Number(results.reduce((s, r) => s + r.input_cost_usd, 0).toFixed(8)),
    total_output_cost_usd: Number(results.reduce((s, r) => s + r.output_cost_usd, 0).toFixed(8)),
    total_cost_usd: Number(results.reduce((s, r) => s + r.total_cost_usd, 0).toFixed(8)),
    avg_latency_ms: Number((results.reduce((s, r) => s + r.latency_ms, 0) / Math.max(results.length, 1)).toFixed(2)),
  };

  await fs.writeFile(outJson, JSON.stringify({ summary, results }, null, 2), "utf8");

  const headers = [
    "test_case_id",
    "generation_label",
    "generation_provider",
    "generation_model",
    "review_label",
    "review_provider",
    "review_model",
    "run_id",
    "status",
    "input_tokens",
    "output_tokens",
    "input_cost_usd",
    "output_cost_usd",
    "total_cost_usd",
    "latency_ms",
    "schema_valid",
    "schema_error_count",
    "error",
  ];
  const lines = [headers.join(",")];
  for (const row of results) {
    lines.push(headers.map((h) => csvEscape(row[h])).join(","));
  }
  await fs.writeFile(outCsv, `${lines.join("\n")}\n`, "utf8");

  process.stdout.write(`\nDone.\nJSON: ${outJson}\nCSV : ${outCsv}\n`);
  process.stdout.write(`Summary: ${JSON.stringify(summary, null, 2)}\n`);
};

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
