import { mkdir } from "node:fs/promises";
import path from "node:path";
import crypto from "node:crypto";
import Database from "better-sqlite3";

const DATA_DIR = path.resolve(process.cwd(), "backend", "data");
const DB_PATH = path.join(DATA_DIR, "benchmark_runs.sqlite");
const DEFAULT_PROMPTS = {
  gatekeeper:
    "Classify if this learning material is supported by system scope (C intro, Java OOP, DSA OOP). Return strict JSON: {is_supported:boolean,primary_domain:string,reason:string}. Material: {material}",
  extracted_content_vision:
    "Extract and structure all visible text from this document image as markdown. Keep headings, lists, and tables if present.",
  embedding_tagging:
    "Bạn là AI gắn nhãn topic cho chunk kiến thức lập trình. Dựa vào taxonomy: {allowedTags}. Môn học: {subject}. Ngôn ngữ: {language}. Trả về JSON duy nhất dạng {\"topic_tags\":[\"tag1\",\"tag2\"],\"confidence\":0.0-1.0}. Chunk: {chunk}",
  generation:
    "Bạn là AI tạo ngân hàng câu hỏi chuẩn cho bảng QuestionsBank. Tạo {count} câu dạng {questionType} cho môn {domain}. Trả về JSON array, mỗi phần tử PHẢI có đúng các field: type, topic_tags, difficulty, title, description, skeleton_code, solution_code, test_cases, options, explanation. Quy tắc: type chỉ nhận FE hoặc PE; FE => skeleton_code=null, solution_code=null, test_cases=null, options là mảng đáp án có đánh dấu đáp án đúng ngay trong string, explanation có thể có; PE => options=null, explanation có thể null, bắt buộc có skeleton_code, solution_code, test_cases (gồm input, expected_output, is_hidden).",
  review:
    "Review the generated exam content for correctness, difficulty alignment, and schema quality. Return JSON with {review_status,issues,suggestions}. Generated: {generatedOutput}",
  code_mentor:
    "You are AI Code Mentor. Analyze submission and provide verdict/issues/fix guidance and complexity. Problem: {problem} Code: {code}",
};

const ensureDir = async () => {
  await mkdir(DATA_DIR, { recursive: true });
};

let dbInstance = null;

const getDb = () => {
  if (!dbInstance) {
    dbInstance = new Database(DB_PATH);
    dbInstance.pragma("journal_mode = WAL");
    dbInstance.exec(`
      CREATE TABLE IF NOT EXISTS benchmark_runs (
        run_id TEXT PRIMARY KEY,
        mode TEXT NOT NULL,
        run_at TEXT NOT NULL,
        file_name TEXT,
        total_cost_usd REAL DEFAULT 0,
        latency_ms REAL,
        input_json TEXT NOT NULL,
        output_json TEXT NOT NULL,
        created_at TEXT NOT NULL DEFAULT (datetime('now'))
      );
      CREATE INDEX IF NOT EXISTS idx_benchmark_runs_mode ON benchmark_runs(mode);
      CREATE INDEX IF NOT EXISTS idx_benchmark_runs_run_at ON benchmark_runs(run_at);
      CREATE TABLE IF NOT EXISTS benchmark_prompts (
        function_key TEXT PRIMARY KEY,
        prompt_text TEXT NOT NULL,
        updated_at TEXT NOT NULL DEFAULT (datetime('now'))
      );
    `);

    const seedStmt = dbInstance.prepare(`
      INSERT INTO benchmark_prompts (function_key, prompt_text, updated_at)
      VALUES (?, ?, ?)
      ON CONFLICT(function_key) DO NOTHING
    `);
    const now = new Date().toISOString();
    for (const [functionKey, promptText] of Object.entries(DEFAULT_PROMPTS)) {
      seedStmt.run(functionKey, promptText, now);
    }
  }
  return dbInstance;
};

export const createRunId = () => {
  const stamp = new Date().toISOString().replace(/[.:]/g, "-");
  const suffix = crypto.randomBytes(4).toString("hex");
  return `run-${stamp}-${suffix}`;
};

export const saveRunReport = async (report, inputPayload = {}) => {
  await ensureDir();
  const db = getDb();
  const stmt = db.prepare(`
    INSERT INTO benchmark_runs (
      run_id, mode, run_at, file_name, total_cost_usd, latency_ms, input_json, output_json
    ) VALUES (
      @run_id, @mode, @run_at, @file_name, @total_cost_usd, @latency_ms, @input_json, @output_json
    )
  `);

  const totalCostUsd = report?.totals?.totalCostUsd ?? report?.totalCostUsd ?? report?.embedding?.totalCostUsd ?? 0;
  const latencyMs = report?.totals?.latency_ms ?? report?.latency_ms ?? report?.embedding?.latency_ms ?? null;
  stmt.run({
    run_id: report.runId,
    mode: report.mode || "unknown",
    run_at: report.runAt || new Date().toISOString(),
    file_name: report?.file?.name || null,
    total_cost_usd: totalCostUsd,
    latency_ms: latencyMs,
    input_json: JSON.stringify(inputPayload || {}),
    output_json: JSON.stringify(report || {}),
  });
  return report.runId;
};

export const getRunReport = async (runId) => {
  await ensureDir();
  const db = getDb();
  const row = db
    .prepare("SELECT input_json, output_json FROM benchmark_runs WHERE run_id = ?")
    .get(runId);

  if (!row) {
    throw new Error("Run not found");
  }

  const output = JSON.parse(row.output_json || "{}");
  const input = JSON.parse(row.input_json || "{}");
  return {
    ...output,
    benchmarkIO: {
      input,
      output,
    },
  };
};

export const listRunReports = async () => {
  await ensureDir();
  const db = getDb();
  const rows = db
    .prepare(
      `SELECT run_id, mode, run_at, file_name, total_cost_usd, latency_ms
       FROM benchmark_runs
       ORDER BY run_at DESC`
    )
    .all();

  return rows.map((row) => ({
    runId: row.run_id,
    mode: row.mode,
    runAt: row.run_at,
    fileName: row.file_name,
    totalCostUsd: row.total_cost_usd ?? 0,
    latencyMs: row.latency_ms ?? null,
  }));
};

export const toRunSummaryCsv = (report) => {
  const rows = [
    ["runId", report.runId],
    ["mode", report.mode],
    ["runAt", report.runAt],
    ["fileName", report.file?.name || ""],
    ["fileSizeMb", report.file?.sizeMb ?? ""],
    ["words", report.documentStats?.words ?? ""],
    ["chunks", report.documentStats?.chunks ?? ""],
    ["totalInputTokens", report.totals?.inputTokens ?? ""],
    ["totalOutputTokens", report.totals?.outputTokens ?? ""],
    ["totalCostUsd", report.totals?.totalCostUsd ?? ""],
    ["totalLatencyMs", report.totals?.latency_ms ?? ""],
    [],
    ["step", "model_or_parser", "latency_ms", "input_tokens", "output_tokens", "cost_usd", "status", "note"],
    ...(report.steps || []).map((s) => [
      s.step,
      s.parser || s.model || "",
      s.latency_ms ?? "",
      s.inputTokens ?? "",
      s.outputTokens ?? "",
      s.totalCostUsd ?? "",
      s.status ?? "",
      (s.note || "").replace(/[\r\n]+/g, " "),
    ]),
  ];

  return rows
    .map((r) => r.map((v) => `"${String(v ?? "").replace(/"/g, '""')}"`).join(","))
    .join("\n");
};

export const listPrompts = async () => {
  await ensureDir();
  const db = getDb();
  const rows = db.prepare("SELECT function_key, prompt_text, updated_at FROM benchmark_prompts ORDER BY function_key ASC").all();
  const prompts = {};
  for (const row of rows) {
    prompts[row.function_key] = {
      prompt: row.prompt_text,
      updatedAt: row.updated_at,
    };
  }
  return { prompts };
};

export const savePrompt = async (functionKey, promptText) => {
  await ensureDir();
  const db = getDb();
  const now = new Date().toISOString();
  db.prepare(
    `INSERT INTO benchmark_prompts (function_key, prompt_text, updated_at)
     VALUES (?, ?, ?)
     ON CONFLICT(function_key) DO UPDATE SET
       prompt_text = excluded.prompt_text,
       updated_at = excluded.updated_at`
  ).run(functionKey, promptText, now);
  return { functionKey, prompt: promptText, updatedAt: now };
};

export const getPrompt = async (functionKey) => {
  await ensureDir();
  const db = getDb();
  const row = db.prepare("SELECT function_key, prompt_text, updated_at FROM benchmark_prompts WHERE function_key = ?").get(functionKey);
  if (!row) {
    return {
      functionKey,
      prompt: DEFAULT_PROMPTS[functionKey] || "",
      updatedAt: null,
    };
  }
  return {
    functionKey: row.function_key,
    prompt: row.prompt_text,
    updatedAt: row.updated_at,
  };
};
