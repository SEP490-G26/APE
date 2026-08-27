import xlsx from "xlsx";
import path from "node:path";

const INPUT_XLSX = "d:/__DLs/AI Reports (1).xlsx";
const OUTPUT_XLSX = "d:/__DLs/AI Reports (1)_with_summary.xlsx";
const SOURCE_SHEET = "3. Benchmark Logs";

const PREFIX_TO_FUNCTION = {
  GAT: "AI Gatekeeper",
  EXT: "AI Extracted Content",
  EMB: "AI Embedding + Auto Tagging",
  GEN: "AI Generation + Review",
  COD: "AI Code Mentor",
};

const n = (v) => {
  const x = Number(v);
  return Number.isFinite(x) ? x : 0;
};

const normalizeModel = (row) => {
  const a = row.model_a || row["model_a(Main Embedding Model)"] || "";
  const b = row["model_b(optional)"] || row["model_b(Autotagging)"] || "";
  const c = row["model_c(Vision)"] || "";
  return { model_a: a, model_b: b, model_c: c };
};

const getTotalCost = (row) => {
  // Some sections use total_cost_usd, embedding uses "Total Cost".
  return n(row.total_cost_usd) || n(row["Total Cost"]);
};

const normalizeRow = (row) => {
  const caseId = row.case_id;
  const prefix = String(caseId || "").split("-")[0];
  const function_name = PREFIX_TO_FUNCTION[prefix] || prefix || "Unknown";
  const { model_a, model_b, model_c } = normalizeModel(row);
  const input_tokens = n(row.input_tokens);
  const output_tokens = n(row.output_tokens);
  const input_cost_usd = n(row.input_cost_usd);
  const output_cost_usd = n(row.output_cost_usd);
  const total_cost_usd = getTotalCost(row);
  const latency_ms = n(row.latency_ms);
  const vision_cost_usd = n(row["Vision Cost"]);
  const vision_input_tok = n(row["Vision Input Tok"]);
  const vision_output_tok = n(row["Vision Output Tok"]);
  const tag_input_cost_usd = n(row["Tag Input Cost"]);
  const tag_output_cost_usd = n(row["Tag Output Cost"]);
  const tag_total_cost_usd = n(row["Tag Total Cost"]) || (tag_input_cost_usd + tag_output_cost_usd);
  return {
    case_id: caseId,
    function_name,
    priority: row.priority || "",
    scenario_group: row.scenario_group || "",
    input_type: row.input_type || "",
    subject: row.subject || "",
    language: row.language || "",
    model_a,
    model_b,
    model_c,
    input_tokens,
    output_tokens,
    input_cost_usd,
    output_cost_usd,
    total_cost_usd,
    latency_ms,
    embedded_images: n(row["Embedded Images"]),
    vision_input_tok,
    vision_output_tok,
    vision_cost_usd,
    vision_latency_ms: n(row["Vision Latency"]),
    tag_input_tok: n(row["Tag Input Token"]),
    tag_output_tok: n(row["Tag Output Token"]),
    tag_input_cost_usd,
    tag_output_cost_usd,
    tag_total_cost_usd,
    status: row.status || "",
    notes: row.notes || "",
  };
};

const isCompleted = (status) => {
  const s = String(status || "").toLowerCase();
  return s.includes("completed") || s === "ok" || s === "ok_estimated";
};

const sum = (arr, key) => arr.reduce((acc, r) => acc + n(r[key]), 0);

const wb = xlsx.readFile(INPUT_XLSX);
const ws = wb.Sheets[SOURCE_SHEET];
if (!ws) {
  throw new Error(`Sheet not found: ${SOURCE_SHEET}`);
}

const aoaRows = xlsx.utils.sheet_to_json(ws, { header: 1, defval: "" });
let currentHeader = null;
const rawRows = [];
for (const r of aoaRows) {
  const first = String(r?.[0] || "").trim();
  if (first === "case_id") {
    currentHeader = r.map((x) => String(x || "").trim());
    continue;
  }
  if (!currentHeader) continue;
  if (!/^[A-Z]{3,6}-\d+/.test(first)) continue;
  const obj = {};
  for (let i = 0; i < currentHeader.length; i += 1) {
    const key = currentHeader[i];
    if (!key) continue;
    obj[key] = r[i] ?? "";
  }
  rawRows.push(obj);
}

const normalized = rawRows.map(normalizeRow);

const fnList = [...new Set(normalized.map((r) => r.function_name))];
const byFunction = fnList.map((fn) => {
  const items = normalized.filter((r) => r.function_name === fn);
  const completedCount = items.filter((r) => isCompleted(r.status)).length;
  const totalCases = items.length;
  const totalCost = sum(items, "total_cost_usd");
  return {
    function_name: fn,
    total_cases: totalCases,
    completed_cases: completedCount,
    completion_rate: totalCases ? Number((completedCount / totalCases).toFixed(4)) : 0,
    total_input_tokens: sum(items, "input_tokens"),
    total_output_tokens: sum(items, "output_tokens"),
    total_input_cost_usd: Number(sum(items, "input_cost_usd").toFixed(8)),
    total_output_cost_usd: Number(sum(items, "output_cost_usd").toFixed(8)),
    total_cost_usd: Number(totalCost.toFixed(8)),
    avg_cost_per_case_usd: totalCases ? Number((totalCost / totalCases).toFixed(8)) : 0,
    avg_latency_ms: totalCases ? Number((sum(items, "latency_ms") / totalCases).toFixed(2)) : 0,
  };
});

// 1 flow = 1 run for each core function.
const requiredFlowOrder = [
  "AI Gatekeeper",
  "AI Extracted Content",
  "AI Embedding + Auto Tagging",
  "AI Generation + Review",
  "AI Code Mentor",
];

const flowRows = requiredFlowOrder
  .map((fn) => byFunction.find((x) => x.function_name === fn))
  .filter(Boolean);

const flow_cost_usd = Number(sum(flowRows, "avg_cost_per_case_usd").toFixed(8));
const flow_input_tokens = sum(flowRows, "total_input_tokens");
const flow_output_tokens = sum(flowRows, "total_output_tokens");
const users_month = 500;
const monthly_cost_500_users_usd = Number((flow_cost_usd * users_month).toFixed(8));

const assumptions = [
  ["Assumption", "Value"],
  ["1 flow definition", "Gatekeeper + Extracted Content + Embedding/Tagging + Generation/Review + Code Mentor"],
  ["Flow cost (USD)", flow_cost_usd],
  ["Users / month", users_month],
  ["Estimated monthly cost for 500 users (USD)", monthly_cost_500_users_usd],
  ["Note", "Flow cost uses avg_cost_per_case of each function from current benchmark logs."],
];

const summaryHeader = [
  "function_name",
  "total_cases",
  "completed_cases",
  "completion_rate",
  "total_input_tokens",
  "total_output_tokens",
  "total_input_cost_usd",
  "total_output_cost_usd",
  "total_cost_usd",
  "avg_cost_per_case_usd",
  "avg_latency_ms",
];
const summaryRows = byFunction.map((r) => summaryHeader.map((h) => r[h]));

const unifiedHeader = [
  "case_id",
  "function_name",
  "priority",
  "scenario_group",
  "input_type",
  "subject",
  "language",
  "model_a",
  "model_b",
  "model_c",
  "input_tokens",
  "output_tokens",
  "input_cost_usd",
  "output_cost_usd",
  "total_cost_usd",
  "latency_ms",
  "embedded_images",
  "vision_input_tok",
  "vision_output_tok",
  "vision_cost_usd",
  "vision_latency_ms",
  "tag_input_tok",
  "tag_output_tok",
  "tag_input_cost_usd",
  "tag_output_cost_usd",
  "tag_total_cost_usd",
  "status",
  "notes",
];
const unifiedRows = normalized.map((r) => unifiedHeader.map((h) => r[h]));

const aoa = [];
aoa.push(["BENCHMARK SUMMARY"]);
aoa.push([]);
for (const row of assumptions) aoa.push(row);
aoa.push([]);
aoa.push(["TOTAL BY FUNCTION"]);
aoa.push(summaryHeader);
for (const row of summaryRows) aoa.push(row);
aoa.push([]);
aoa.push(["UNIFIED STANDARD TABLE"]);
aoa.push(unifiedHeader);
for (const row of unifiedRows) aoa.push(row);

const summaryWs = xlsx.utils.aoa_to_sheet(aoa);
wb.Sheets["5. Summary Benchmark"] = summaryWs;
if (!wb.SheetNames.includes("5. Summary Benchmark")) wb.SheetNames.push("5. Summary Benchmark");

xlsx.writeFile(wb, OUTPUT_XLSX);
console.log(`Written: ${OUTPUT_XLSX}`);
