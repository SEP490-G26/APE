import { useEffect, useState } from "react";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:3003";
const currency = (value) => `$${Number(value || 0).toFixed(6)}`;

const readJsonSafe = async (response) => {
  const raw = await response.text();
  try {
    return raw ? JSON.parse(raw) : {};
  } catch {
    throw new Error(`API non-JSON response (${response.status})`);
  }
};

const TabButton = ({ active, onClick, children }) => (
  <button
    onClick={onClick}
    className={`rounded-lg px-4 py-2 text-sm font-semibold transition ${
      active ? "bg-cyan-500 text-slate-950" : "bg-slate-800 text-slate-300 hover:bg-slate-700"
    }`}
  >
    {children}
  </button>
);

const SectionCard = ({ title, children }) => (
  <section className="mb-6 rounded-2xl border border-slate-700 bg-slate-900/60 p-5">
    <h2 className="mb-3 text-lg font-semibold text-slate-100">{title}</h2>
    {children}
  </section>
);

const MetricCard = ({ label, value }) => (
  <div className="rounded border border-slate-700 bg-slate-950 px-3 py-2">
    <p className="text-[11px] uppercase tracking-wide text-slate-400">{label}</p>
    <p className="text-sm font-semibold text-cyan-300">{value}</p>
  </div>
);

const ResultSummary = ({ result }) => {
  if (!result) return null;
  const inputTokens = result?.totals?.inputTokens ?? result?.ai?.inputTokens ?? result?.embedding?.inputTokens ?? result?.generation?.inputTokens ?? result?.inputTokens ?? 0;
  const outputTokens = result?.totals?.outputTokens ?? result?.ai?.outputTokens ?? result?.embedding?.outputTokens ?? result?.review?.outputTokens ?? result?.outputTokens ?? 0;
  const inputCost = result?.totals?.inputCostUsd ?? result?.ai?.inputCostUsd ?? result?.embedding?.inputCostUsd ?? result?.generation?.inputCostUsd ?? result?.inputCostUsd ?? 0;
  const outputCost = result?.totals?.outputCostUsd ?? result?.ai?.outputCostUsd ?? result?.embedding?.outputCostUsd ?? result?.review?.outputCostUsd ?? result?.outputCostUsd ?? 0;
  const totalCost = result?.totals?.totalCostUsd ?? result?.ai?.totalCostUsd ?? result?.embedding?.totalCostUsd ?? result?.totalCostUsd ?? 0;
  const latency = result?.totals?.latency_ms ?? result?.ai?.latency_ms ?? result?.embedding?.latency_ms ?? result?.latency_ms ?? result?.extracted?.latency_ms ?? 0;

  return (
    <div className="mb-3 grid grid-cols-2 gap-2 md:grid-cols-6">
      <MetricCard label="Input Tokens" value={Number(inputTokens || 0)} />
      <MetricCard label="Output Tokens" value={Number(outputTokens || 0)} />
      <MetricCard label="Input Cost" value={currency(inputCost)} />
      <MetricCard label="Output Cost" value={currency(outputCost)} />
      <MetricCard label="Total Cost" value={currency(totalCost)} />
      <MetricCard label="Latency (ms)" value={Number(latency || 0)} />
    </div>
  );
};

const ModelSelect = ({ provider, model, onProvider, onModel, providerModels }) => {
  const options = providerModels?.[provider] || [];
  return (
    <div className="grid grid-cols-2 gap-2">
      <select className="rounded border border-slate-700 bg-slate-900 px-2 py-2" value={provider} onChange={(e) => onProvider(e.target.value)}>
        <option value="openai">openai</option>
        <option value="gemini">gemini</option>
        <option value="cohere">cohere</option>
      </select>
      <select className="rounded border border-slate-700 bg-slate-900 px-2 py-2" value={model} onChange={(e) => onModel(e.target.value)}>
        {options.length ? options.map((m) => <option key={m} value={m}>{m}</option>) : <option value={model}>{model || "no models"}</option>}
      </select>
    </div>
  );
};

const RunHistoryModal = ({ mode, open, onClose }) => {
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(false);
  const [expandedRunId, setExpandedRunId] = useState("");
  const [detailMap, setDetailMap] = useState({});

  const load = async () => {
    setLoading(true);
    try {
      const resp = await fetch(`${API_BASE_URL}/api/reports/runs?mode=${mode}`);
      const json = await readJsonSafe(resp);
      if (resp.ok) setRuns(json.runs || []);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open) load();
  }, [mode, open]);

  const toggleDetail = async (runId) => {
    if (expandedRunId === runId) {
      setExpandedRunId("");
      return;
    }
    setExpandedRunId(runId);
    if (detailMap[runId]) return;
    try {
      const resp = await fetch(`${API_BASE_URL}/api/reports/runs/${runId}`);
      const json = await readJsonSafe(resp);
      setDetailMap((prev) => ({
        ...prev,
        [runId]: resp.ok ? json : { error: json?.error || "Run report not found" },
      }));
    } catch (e) {
      setDetailMap((prev) => ({ ...prev, [runId]: { error: e.message } }));
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4">
      <div className="w-full max-w-6xl rounded-2xl border border-slate-700 bg-slate-900 p-4">
        <div className="mb-3 flex items-center justify-between">
          <p className="text-sm font-semibold text-cyan-300">Run history ({mode})</p>
          <div className="flex gap-2">
            <button onClick={load} className="rounded bg-slate-800 px-2 py-1 text-xs">Refresh</button>
            <button onClick={onClose} className="rounded bg-slate-700 px-2 py-1 text-xs">Close</button>
          </div>
        </div>
        {loading && <p className="mb-2 text-xs text-slate-400">Loading...</p>}
        <div className="max-h-[72vh] overflow-auto">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {runs.map((r) => {
              const d = detailMap[r.runId] || {};
              const openDetail = expandedRunId === r.runId;
              return (
                <div key={r.runId} className="rounded-xl border border-slate-700 bg-slate-950/60 p-3">
                  <div className="mb-2 flex items-start justify-between">
                    <div>
                      <p className="text-[11px] text-slate-400">Run ID</p>
                      <p className="font-mono text-xs text-slate-200">{r.runId}</p>
                    </div>
                    <span className="rounded bg-cyan-900/40 px-2 py-1 text-[11px] text-cyan-200">{currency(r.totalCostUsd)}</span>
                  </div>
                  <p className="mb-2 text-[11px] text-slate-400">{r.runAt}</p>
                  <div className="mb-2 grid grid-cols-2 gap-2">
                    <MetricCard label="Cost" value={currency(r.totalCostUsd)} />
                    <MetricCard label="Latency" value={r.latencyMs ?? "-"} />
                  </div>
                  <div className="flex flex-wrap gap-2 text-[11px]">
                    <button className="rounded bg-slate-800 px-2 py-1" onClick={() => toggleDetail(r.runId)}>
                      {openDetail ? "Hide detail" : "Detail"}
                    </button>
                    <a className="rounded bg-slate-800 px-2 py-1" href={`${API_BASE_URL}/api/reports/runs/${r.runId}.json`} target="_blank" rel="noreferrer">JSON</a>
                    <a className="rounded bg-slate-800 px-2 py-1" href={`${API_BASE_URL}/api/reports/runs/${r.runId}.md`} target="_blank" rel="noreferrer">MD</a>
                    <a className="rounded bg-slate-800 px-2 py-1" href={`${API_BASE_URL}/api/reports/runs/${r.runId}.chunks.json`} target="_blank" rel="noreferrer">Chunks JSON</a>
                  </div>
                  {openDetail && (
                    <div className="mt-3 rounded border border-slate-700 bg-slate-950 p-2">
                      <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
                        <MetricCard label="Input Tokens" value={d?.totals?.inputTokens ?? d?.ai?.inputTokens ?? 0} />
                        <MetricCard label="Output Tokens" value={d?.totals?.outputTokens ?? d?.ai?.outputTokens ?? 0} />
                        <MetricCard label="Input Cost" value={currency(d?.totals?.inputCostUsd ?? d?.ai?.inputCostUsd ?? 0)} />
                        <MetricCard label="Output Cost" value={currency(d?.totals?.outputCostUsd ?? d?.ai?.outputCostUsd ?? 0)} />
                      </div>
                      {d?.error && <p className="mt-2 text-xs text-rose-400">{d.error}</p>}
                      {!d?.error && (
                        <details className="mt-2">
                          <summary className="cursor-pointer text-xs text-cyan-300">Show JSON</summary>
                          <pre className="mt-2 max-h-56 overflow-auto rounded border border-slate-700 bg-slate-950 p-2 text-[11px]">
                            {JSON.stringify(d || {}, null, 2)}
                          </pre>
                        </details>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
};

function App() {
  const [tab, setTab] = useState("connection");
  const [credentials, setCredentials] = useState({ openaiBaseUrl: "", openaiApiKey: "", geminiApiKey: "", cohereApiKey: "", cohereBaseUrl: "" });
  const [providerModels, setProviderModels] = useState({
    openai: ["gpt-4o-mini"],
    gemini: ["gemini-3.1-flash-lite"],
    cohere: ["embed-multilingual-v3.0", "embed-english-v3.0", "command-r7b-12-2024", "command-r-08-2024", "command-r-plus-08-2024"],
  });
  const [modelListResult, setModelListResult] = useState(null);

  const [connectionResult, setConnectionResult] = useState(null);
  const [connectionError, setConnectionError] = useState("");

  const [gatekeeperFile, setGatekeeperFile] = useState(null);
  const [gatekeeperSelection, setGatekeeperSelection] = useState({ provider: "gemini", model: "gemini-3.1-flash-lite" });
  const [gatekeeperSelectionB, setGatekeeperSelectionB] = useState({ provider: "openai", model: "gpt-4o-mini" });
  const [gatekeeperResult, setGatekeeperResult] = useState(null);
  const [gatekeeperResultB, setGatekeeperResultB] = useState(null);
  const [gatekeeperError, setGatekeeperError] = useState("");
  const [gatekeeperErrorB, setGatekeeperErrorB] = useState("");

  const [extractedFile, setExtractedFile] = useState(null);
  const [extractedSelection, setExtractedSelection] = useState({ provider: "gemini", model: "gemini-3.5-flash" });
  const [extractedResult, setExtractedResult] = useState(null);
  const [extractedError, setExtractedError] = useState("");

  const [embeddingFile, setEmbeddingFile] = useState(null);
  const [embeddingSelection, setEmbeddingSelection] = useState({ provider: "cohere", model: "embed-multilingual-v3.0" });
  const [embeddingSelectionB, setEmbeddingSelectionB] = useState({ provider: "openai", model: "text-embedding-3-small" });
  const [embeddingTaggingSelection, setEmbeddingTaggingSelection] = useState({ provider: "cohere", model: "command-r7b-12-2024" });
  const [embeddingTaggingContext, setEmbeddingTaggingContext] = useState({
    subject: "java_oop",
    language: "java",
    allowedTagsText: "array, pointer, oop, class, object, linked_list, stack, queue, sorting, searching",
  });
  const [embeddingResult, setEmbeddingResult] = useState(null);
  const [embeddingResultB, setEmbeddingResultB] = useState(null);
  const [embeddingError, setEmbeddingError] = useState("");
  const [embeddingErrorB, setEmbeddingErrorB] = useState("");
  const [chunkingFile, setChunkingFile] = useState(null);
  const [chunkingSelection, setChunkingSelection] = useState({ provider: "cohere", model: "embed-multilingual-v3.0" });
  const [chunkingVisionSelection, setChunkingVisionSelection] = useState({ provider: "gemini", model: "gemini-3.5-flash" });
  const [chunkingTaggingSelection, setChunkingTaggingSelection] = useState({ provider: "cohere", model: "command-r7b-12-2024" });
  const [chunkingStrategy, setChunkingStrategy] = useState("text_only");
  const [chunkingResult, setChunkingResult] = useState(null);
  const [chunkingError, setChunkingError] = useState("");
  const [chunkingRunLogs, setChunkingRunLogs] = useState([]);
  const [chunkingLoading, setChunkingLoading] = useState(false);

  const [genSelection, setGenSelection] = useState({ provider: "gemini", model: "gemini-3.5-flash" });
  const [reviewSelection, setReviewSelection] = useState({ provider: "gemini", model: "gemini-3.5-flash" });
  const [genSelectionB, setGenSelectionB] = useState({ provider: "openai", model: "gpt-4o-mini" });
  const [reviewSelectionB, setReviewSelectionB] = useState({ provider: "openai", model: "gpt-4o-mini" });
  const [genInput, setGenInput] = useState({ questionType: "multiple_choice", domain: "Java_OOP", difficulty: "Medium", count: 3 });
  const [genReviewResult, setGenReviewResult] = useState(null);
  const [genReviewResultB, setGenReviewResultB] = useState(null);
  const [genReviewError, setGenReviewError] = useState("");
  const [genReviewErrorB, setGenReviewErrorB] = useState("");

  const [mentorSelection, setMentorSelection] = useState({ provider: "openai", model: "gpt-4o-mini" });
  const [mentorSelectionB, setMentorSelectionB] = useState({ provider: "gemini", model: "gemini-3.5-flash" });
  const [mentorInput, setMentorInput] = useState({ language: "java", problem: "", code: "" });
  const [mentorResult, setMentorResult] = useState(null);
  const [mentorResultB, setMentorResultB] = useState(null);
  const [mentorError, setMentorError] = useState("");
  const [mentorErrorB, setMentorErrorB] = useState("");
  const [fullFlowFile, setFullFlowFile] = useState(null);
  const [fullFlowResult, setFullFlowResult] = useState(null);
  const [fullFlowError, setFullFlowError] = useState("");
  const [simulationInput, setSimulationInput] = useState({
    students: 500,
    feGenerationPerStudent: 30,
    peGenerationPerStudent: 12,
    byosPerStudent: 2,
    mentorPerStudent: 25,
  });
  const [simulationResult, setSimulationResult] = useState(null);
  const [simulationError, setSimulationError] = useState("");
  const [subjectPreset, setSubjectPreset] = useState("java_oop");
  const [contentLanguage, setContentLanguage] = useState("vi");
  const [historyMode, setHistoryMode] = useState("");

  const cohereEmbedModels = (providerModels?.cohere || []).filter((m) => /^embed/i.test(String(m || "")));
  const cohereCommandModels = (providerModels?.cohere || []).filter((m) => /^command/i.test(String(m || "")));
  const [prompts, setPrompts] = useState({
    gatekeeper: "",
    extracted_content_vision: "",
    embedding_tagging: "",
    generation: "",
    review: "",
    code_mentor: "",
  });
  const [promptStatus, setPromptStatus] = useState("");
  const [showJson, setShowJson] = useState({
    connection: true,
    gatekeeper: false,
    extracted: false,
    embedding: false,
    genreview: false,
    mentor: false,
    chunking: false,
  });
  const [autoLoadingModels, setAutoLoadingModels] = useState(false);

  const postJson = async (path, body, method = "POST") => {
    const resp = await fetch(`${API_BASE_URL}${path}`, { method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
    const json = await readJsonSafe(resp);
    if (!resp.ok) throw new Error(json?.error || "Request failed");
    return json;
  };

  const loadPrompts = async () => {
    try {
      const resp = await fetch(`${API_BASE_URL}/api/prompts`);
      const json = await readJsonSafe(resp);
      if (!resp.ok) throw new Error(json?.error || "Failed to load prompts");
      setPrompts({
        gatekeeper: json?.prompts?.gatekeeper?.prompt || "",
        extracted_content_vision: json?.prompts?.extracted_content_vision?.prompt || "",
        embedding_tagging: json?.prompts?.embedding_tagging?.prompt || "",
        generation: json?.prompts?.generation?.prompt || "",
        review: json?.prompts?.review?.prompt || "",
        code_mentor: json?.prompts?.code_mentor?.prompt || "",
      });
    } catch (e) {
      setPromptStatus(e.message);
    }
  };

  const savePromptToApi = async (functionKey) => {
    try {
      setPromptStatus("");
      const result = await postJson(`/api/prompts/${functionKey}`, { prompt: prompts[functionKey] }, "PUT");
      setPromptStatus(`Saved prompt: ${result.functionKey}`);
    } catch (e) {
      setPromptStatus(e.message);
    }
  };

  useEffect(() => {
    loadPrompts();
  }, []);

  useEffect(() => {
    const hasAnyCred =
      credentials.openaiApiKey ||
      credentials.geminiApiKey ||
      credentials.cohereApiKey ||
      credentials.openaiBaseUrl ||
      credentials.cohereBaseUrl;
    if (!hasAnyCred) return;
    const timer = setTimeout(async () => {
      try {
        setAutoLoadingModels(true);
        const result = await postJson("/api/providers/models/list", { credentials });
        setModelListResult(result);
        setProviderModels((prev) => ({
          openai: result?.providers?.openai?.models?.length ? result.providers.openai.models : prev.openai,
          gemini: result?.providers?.gemini?.models?.length ? result.providers.gemini.models : prev.gemini,
          cohere: result?.providers?.cohere?.models?.length ? result.providers.cohere.models : prev.cohere,
        }));
      } catch (e) {
        setModelListResult({ error: e.message });
      } finally {
        setAutoLoadingModels(false);
      }
    }, 700);
    return () => clearTimeout(timer);
  }, [credentials]);

  const loadModels = async () => {
    try {
      const result = await postJson("/api/providers/models/list", { credentials });
      setModelListResult(result);
      setProviderModels({
        openai: result?.providers?.openai?.models?.length ? result.providers.openai.models : providerModels.openai,
        gemini: result?.providers?.gemini?.models?.length ? result.providers.gemini.models : providerModels.gemini,
        cohere: result?.providers?.cohere?.models?.length ? result.providers.cohere.models : providerModels.cohere,
      });
    } catch (e) {
      setModelListResult({ error: e.message });
    }
  };

  const testConnection = async () => {
    setConnectionError("");
    try { setConnectionResult(await postJson("/api/connection/test", { credentials })); } catch (e) { setConnectionError(e.message); }
  };

  const runGatekeeper = async () => {
    if (!gatekeeperFile) return setGatekeeperError("Please upload a file.");
    setGatekeeperError("");
    try {
      const fd = new FormData(); fd.append("file", gatekeeperFile); fd.append("selection", JSON.stringify(gatekeeperSelection)); fd.append("credentials", JSON.stringify(credentials));
      fd.append("prompt", prompts.gatekeeper || "");
      const resp = await fetch(`${API_BASE_URL}/api/benchmarks/gatekeeper/upload`, { method: "POST", body: fd });
      const json = await readJsonSafe(resp); if (!resp.ok) throw new Error(json?.error || "Gatekeeper benchmark failed"); setGatekeeperResult(json);
    } catch (e) { setGatekeeperError(e.message); }
  };
  const runGatekeeperCompare = async () => {
    if (!gatekeeperFile) return setGatekeeperError("Please upload a file.");
    setGatekeeperError("");
    setGatekeeperErrorB("");
    try {
      const makeReq = async (selection) => {
        const fd = new FormData();
        fd.append("file", gatekeeperFile);
        fd.append("selection", JSON.stringify(selection));
        fd.append("credentials", JSON.stringify(credentials));
        fd.append("prompt", prompts.gatekeeper || "");
        const resp = await fetch(`${API_BASE_URL}/api/benchmarks/gatekeeper/upload`, { method: "POST", body: fd });
        const json = await readJsonSafe(resp);
        if (!resp.ok) throw new Error(json?.error || "Gatekeeper benchmark failed");
        return json;
      };
      const [a, b] = await Promise.all([makeReq(gatekeeperSelection), makeReq(gatekeeperSelectionB)]);
      setGatekeeperResult(a);
      setGatekeeperResultB(b);
    } catch (e) {
      setGatekeeperError(e.message);
      setGatekeeperErrorB(e.message);
    }
  };

  const runEmbedding = async () => {
    if (!embeddingFile) return setEmbeddingError("Please upload a file.");
    setEmbeddingError("");
    try {
      const fd = new FormData();
      fd.append("file", embeddingFile);
      fd.append("selection", JSON.stringify(embeddingSelection));
      fd.append("taggingSelection", JSON.stringify({ provider: "cohere", model: embeddingTaggingSelection.model }));
      fd.append("taggingPrompt", prompts.embedding_tagging || "");
      fd.append(
        "taggingContext",
        JSON.stringify({
          subject: embeddingTaggingContext.subject || "general_programming",
          language: embeddingTaggingContext.language || "general",
          allowedTags: (embeddingTaggingContext.allowedTagsText || "")
            .split(",")
            .map((t) => t.trim())
            .filter(Boolean),
        })
      );
      fd.append("credentials", JSON.stringify(credentials));
      const resp = await fetch(`${API_BASE_URL}/api/benchmarks/embedding/upload`, { method: "POST", body: fd });
      const json = await readJsonSafe(resp); if (!resp.ok) throw new Error(json?.error || "Embedding benchmark failed"); setEmbeddingResult(json);
    } catch (e) { setEmbeddingError(e.message); }
  };
  const runEmbeddingCompare = async () => {
    if (!embeddingFile) return setEmbeddingError("Please upload a file.");
    setEmbeddingError("");
    setEmbeddingErrorB("");
    try {
      const makeReq = async (selection) => {
        const fd = new FormData();
        fd.append("file", embeddingFile);
        fd.append("selection", JSON.stringify(selection));
        fd.append("taggingSelection", JSON.stringify({ provider: "cohere", model: embeddingTaggingSelection.model }));
        fd.append("taggingPrompt", prompts.embedding_tagging || "");
        fd.append(
          "taggingContext",
          JSON.stringify({
            subject: embeddingTaggingContext.subject || "general_programming",
            language: embeddingTaggingContext.language || "general",
            allowedTags: (embeddingTaggingContext.allowedTagsText || "").split(",").map((t) => t.trim()).filter(Boolean),
          })
        );
        fd.append("credentials", JSON.stringify(credentials));
        const resp = await fetch(`${API_BASE_URL}/api/benchmarks/embedding/upload`, { method: "POST", body: fd });
        const json = await readJsonSafe(resp);
        if (!resp.ok) throw new Error(json?.error || "Embedding benchmark failed");
        return json;
      };
      const [a, b] = await Promise.all([makeReq(embeddingSelection), makeReq(embeddingSelectionB)]);
      setEmbeddingResult(a);
      setEmbeddingResultB(b);
    } catch (e) {
      setEmbeddingError(e.message);
      setEmbeddingErrorB(e.message);
    }
  };
  const runChunkingLogic = async () => {
    if (!chunkingFile) return setChunkingError("Please upload a file.");
    setChunkingError("");
    setChunkingRunLogs([]);
    setChunkingLoading(true);
    try {
      setChunkingRunLogs((p) => [...p, "Preparing request payload..."]);
      const fd = new FormData();
      fd.append("file", chunkingFile);
      fd.append("selection", JSON.stringify(chunkingSelection));
      fd.append("visionSelection", JSON.stringify(chunkingVisionSelection));
      fd.append("visionPrompt", prompts.extracted_content_vision || "");
      fd.append("taggingSelection", JSON.stringify(chunkingTaggingSelection));
      fd.append("taggingPrompt", prompts.embedding_tagging || "");
      fd.append("processingStrategy", chunkingStrategy);
      fd.append(
        "taggingContext",
        JSON.stringify({
          subject: embeddingTaggingContext.subject || "general_programming",
          language: embeddingTaggingContext.language || "general",
          allowedTags: (embeddingTaggingContext.allowedTagsText || "").split(",").map((t) => t.trim()).filter(Boolean),
        })
      );
      fd.append("credentials", JSON.stringify(credentials));
      setChunkingRunLogs((p) => [...p, "Sending request to /api/benchmarks/chunking-logic/upload ..."]);
      const resp = await fetch(`${API_BASE_URL}/api/benchmarks/chunking-logic/upload`, { method: "POST", body: fd });
      setChunkingRunLogs((p) => [...p, `Response status: ${resp.status}`]);
      const json = await readJsonSafe(resp);
      if (!resp.ok) throw new Error(json?.error || "Chunking logic benchmark failed");
      setChunkingResult(json);
      setChunkingRunLogs((p) => [...p, "Completed successfully."]);
    } catch (e) {
      setChunkingError(e.message);
      setChunkingRunLogs((p) => [...p, `Error: ${e.message}`]);
    } finally {
      setChunkingLoading(false);
    }
  };

  const importChunkJsonToGeneration = async (file) => {
    if (!file) return;
    try {
      const text = await file.text();
      const json = JSON.parse(text);
      const chunks = Array.isArray(json?.chunks) ? json.chunks : [];
      const lines = chunks
        .map((c) => {
          const body = c?.content_text || c?.text || "";
          const tags = Array.isArray(c?.topic_tags) ? c.topic_tags.join(", ") : "";
          if (!body) return "";
          return tags ? `[topic_tags: ${tags}]\n${body}` : body;
        })
        .filter(Boolean);
      setGenInput((prev) => ({
        ...prev,
        contextChunksText: lines.join("\n---\n"),
      }));
    } catch (e) {
      setGenReviewError(`Import chunk JSON failed: ${e.message}`);
    }
  };

  const runExtractedContent = async () => {
    if (!extractedFile) return setExtractedError("Please upload a file.");
    setExtractedError("");
    try {
      const fd = new FormData(); fd.append("file", extractedFile); fd.append("selection", JSON.stringify(extractedSelection)); fd.append("credentials", JSON.stringify(credentials));
      fd.append("prompt", prompts.extracted_content_vision || "");
      const resp = await fetch(`${API_BASE_URL}/api/benchmarks/extracted-content/upload`, { method: "POST", body: fd });
      const json = await readJsonSafe(resp); if (!resp.ok) throw new Error(json?.error || "Extracted content benchmark failed"); setExtractedResult(json);
    } catch (e) { setExtractedError(e.message); }
  };

  const runGenReview = async () => {
    setGenReviewError("");
    try {
      setGenReviewResult(await postJson("/api/benchmarks/gen-review/run", {
        input: genInput,
        generationSelection: genSelection,
        reviewSelection,
        generationPrompt: prompts.generation || "",
        reviewPrompt: prompts.review || "",
        credentials,
      }));
    } catch (e) { setGenReviewError(e.message); }
  };
  const runGenReviewCompare = async () => {
    setGenReviewError("");
    setGenReviewErrorB("");
    try {
      const makeReq = async (gSel, rSel) =>
        postJson("/api/benchmarks/gen-review/run", {
          input: genInput,
          generationSelection: gSel,
          reviewSelection: rSel,
          generationPrompt: prompts.generation || "",
          reviewPrompt: prompts.review || "",
          credentials,
        });
      const [a, b] = await Promise.all([
        makeReq(genSelection, reviewSelection),
        makeReq(genSelectionB, reviewSelectionB),
      ]);
      setGenReviewResult(a);
      setGenReviewResultB(b);
    } catch (e) {
      setGenReviewError(e.message);
      setGenReviewErrorB(e.message);
    }
  };
  const runMentor = async () => {
    setMentorError("");
    try {
      setMentorResult(await postJson("/api/benchmarks/mentor/run", {
        input: mentorInput,
        mentorPrompt: prompts.code_mentor || "",
        selection: mentorSelection,
        credentials,
      }));
    } catch (e) { setMentorError(e.message); }
  };
  const runMentorCompare = async () => {
    setMentorError("");
    setMentorErrorB("");
    try {
      const makeReq = async (selection) =>
        postJson("/api/benchmarks/mentor/run", {
          input: mentorInput,
          mentorPrompt: prompts.code_mentor || "",
          selection,
          credentials,
        });
      const [a, b] = await Promise.all([makeReq(mentorSelection), makeReq(mentorSelectionB)]);
      setMentorResult(a);
      setMentorResultB(b);
    } catch (e) {
      setMentorError(e.message);
      setMentorErrorB(e.message);
    }
  };

  const runFullFlow = async () => {
    if (!fullFlowFile) return setFullFlowError("Please upload a file for full flow.");
    setFullFlowError("");
    try {
      const fd = new FormData();
      fd.append("file", fullFlowFile);
      fd.append("credentials", JSON.stringify(credentials));
      fd.append("gatekeeperSelection", JSON.stringify(gatekeeperSelection));
      fd.append("embeddingSelection", JSON.stringify(embeddingSelection));
      fd.append("taggingSelection", JSON.stringify(embeddingTaggingSelection));
      fd.append("generationSelection", JSON.stringify(genSelection));
      fd.append("reviewSelection", JSON.stringify(reviewSelection));
      fd.append("mentorSelection", JSON.stringify(mentorSelection));
      fd.append(
        "prompts",
        JSON.stringify({
          gatekeeper: prompts.gatekeeper,
          embedding_tagging: prompts.embedding_tagging,
          generation: prompts.generation,
          review: prompts.review,
          code_mentor: prompts.code_mentor,
        })
      );
      fd.append(
        "generationInput",
        JSON.stringify({
          questionType: genInput.questionType || "FE",
          domain: genInput.domain || "Java_OOP",
          difficulty: genInput.difficulty || "Medium",
          count: Number(genInput.count || 3),
          taggingContext: {
            subject: embeddingTaggingContext.subject || "java_oop",
            language: embeddingTaggingContext.language || "java",
            allowedTags: (embeddingTaggingContext.allowedTagsText || "").split(",").map((t) => t.trim()).filter(Boolean),
          },
        })
      );
      fd.append("mentorInput", JSON.stringify(mentorInput));
      const resp = await fetch(`${API_BASE_URL}/api/benchmarks/full-flow/upload`, { method: "POST", body: fd });
      const json = await readJsonSafe(resp);
      if (!resp.ok) throw new Error(json?.error || "Full flow benchmark failed");
      setFullFlowResult(json);
    } catch (e) {
      setFullFlowError(e.message);
    }
  };

  const runMonthlySimulation = async () => {
    setSimulationError("");
    try {
      const result = await postJson("/api/benchmarks/full-flow/simulate", {
        students: Number(simulationInput.students || 500),
        assumptions: {
          feGenerationPerStudent: Number(simulationInput.feGenerationPerStudent || 30),
          peGenerationPerStudent: Number(simulationInput.peGenerationPerStudent || 12),
          byosPerStudent: Number(simulationInput.byosPerStudent || 2),
          mentorPerStudent: Number(simulationInput.mentorPerStudent || 25),
        },
        modelPlan: {
          gatekeeper: gatekeeperSelection.model,
          embedding: embeddingSelection.model,
          tagging: embeddingTaggingSelection.model,
          generation: genSelection.model,
          review: reviewSelection.model,
          mentor: mentorSelection.model,
        },
      });
      setSimulationResult(result);
    } catch (e) {
      setSimulationError(e.message);
    }
  };

  const applySubjectPreset = () => {
    const presets = {
      c_intro: {
        tags: "array, pointer, loop, function, struct, string, memory, recursion, file_io",
        domain: "C_Intro",
      },
      java_oop: {
        tags: "oop, class, object, encapsulation, inheritance, polymorphism, interface, arraylist, exception",
        domain: "Java_OOP",
      },
      dsa_oop: {
        tags: "array, linked_list, stack, queue, tree, graph, sorting, searching, complexity",
        domain: "DSA_OOP",
      },
    };
    const p = presets[subjectPreset] || presets.java_oop;
    setEmbeddingTaggingContext((prev) => ({
      ...prev,
      subject: subjectPreset,
      language: contentLanguage,
      allowedTagsText: p.tags,
    }));
    setGenInput((prev) => ({ ...prev, domain: p.domain }));
    setPrompts((prev) => ({
      ...prev,
      generation:
        "Tạo {count} câu hỏi cho môn {domain}, dạng {questionType}. Trả về JSON array theo schema QuestionsBank: type, topic_tags, difficulty, title, description, skeleton_code, solution_code, test_cases, options, explanation. Quy tắc FE/PE phải đúng nullability. Ngôn ngữ tài liệu: " +
        contentLanguage +
        ". Bám sát context chunk đã cung cấp.",
      review:
        "Bạn là AI reviewer học thuật. Kiểm tra đáp án/cấu trúc/độ đúng của bộ câu hỏi đã tạo cho môn " +
        p.domain +
        ". Trả JSON {review_status,issues,suggestions,needs_regeneration}.",
      code_mentor:
        "Bạn là AI mentor code cho môn " +
        p.domain +
        ". Phân tích lỗi, đưa hướng sửa cụ thể, độ phức tạp, và nhận xét theo mức sinh viên.",
      embedding_tagging:
        "Bạn là AI gắn nhãn topic cho chunk kiến thức " +
        p.domain +
        ". Dựa vào taxonomy: {allowedTags}. Ngôn ngữ: {language}. Trả JSON {\"topic_tags\":[],\"confidence\":0-1}. Chunk: {chunk}",
    }));
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 md:px-8 text-slate-100">
      <header className="mb-8 rounded-2xl border border-slate-700 bg-slate-900/60 p-6">
        <h1 className="text-2xl font-bold">AI Benchmark Console</h1>
        <div className="mt-3 grid grid-cols-1 gap-2 md:grid-cols-4">
          <select className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" value={subjectPreset} onChange={(e) => setSubjectPreset(e.target.value)}>
            <option value="c_intro">C nhập môn</option>
            <option value="java_oop">Java OOP</option>
            <option value="dsa_oop">Cấu trúc DL & GT</option>
          </select>
          <select className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" value={contentLanguage} onChange={(e) => setContentLanguage(e.target.value)}>
            <option value="vi">Tiếng Việt</option>
            <option value="en">English</option>
          </select>
          <button onClick={applySubjectPreset} className="rounded bg-emerald-600 px-3 py-2 text-sm font-semibold text-white">Apply Prompt Preset</button>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          <TabButton active={tab === "connection"} onClick={() => setTab("connection")}>Connection / Credentials</TabButton>
          <TabButton active={tab === "gatekeeper"} onClick={() => setTab("gatekeeper")}>AI Gatekeeper</TabButton>
          <TabButton active={tab === "extracted"} onClick={() => setTab("extracted")}>AI Extracted Content</TabButton>
          <TabButton active={tab === "embedding"} onClick={() => setTab("embedding")}>AI Embedding</TabButton>
          <TabButton active={tab === "genreview"} onClick={() => setTab("genreview")}>Generation + Review Flow</TabButton>
          <TabButton active={tab === "mentor"} onClick={() => setTab("mentor")}>AI Mentor Code</TabButton>
          <TabButton active={tab === "chunking"} onClick={() => setTab("chunking")}>Chunking Logic</TabButton>
          <TabButton active={tab === "fullflow"} onClick={() => setTab("fullflow")}>Full Flow + Simulation</TabButton>
        </div>
      </header>

      {tab === "connection" && (
        <SectionCard title="Connection / Credentials">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="OpenAI Base URL" value={credentials.openaiBaseUrl} onChange={(e) => setCredentials((p) => ({ ...p, openaiBaseUrl: e.target.value }))} />
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="OpenAI API Key" value={credentials.openaiApiKey} onChange={(e) => setCredentials((p) => ({ ...p, openaiApiKey: e.target.value }))} />
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Gemini API Key" value={credentials.geminiApiKey} onChange={(e) => setCredentials((p) => ({ ...p, geminiApiKey: e.target.value }))} />
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Cohere API Key" value={credentials.cohereApiKey} onChange={(e) => setCredentials((p) => ({ ...p, cohereApiKey: e.target.value }))} />
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Cohere Base URL" value={credentials.cohereBaseUrl} onChange={(e) => setCredentials((p) => ({ ...p, cohereBaseUrl: e.target.value }))} />
          </div>
          <div className="mt-4 flex gap-2">
            <button onClick={testConnection} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Test Connection</button>
            <button onClick={loadModels} className="rounded bg-slate-700 px-4 py-2 font-semibold">Load Models From Providers</button>
            {autoLoadingModels && <span className="rounded bg-slate-800 px-3 py-2 text-xs text-slate-300">Auto loading models...</span>}
          </div>
          {connectionError && <p className="mt-2 text-rose-400">{connectionError}</p>}
          <button onClick={() => setShowJson((p) => ({ ...p, connection: !p.connection }))} className="mt-3 rounded bg-slate-800 px-3 py-1 text-xs">
            {showJson.connection ? "Hide JSON" : "Show JSON"}
          </button>
          {showJson.connection && <pre className="mt-3 max-h-64 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(connectionResult || {}, null, 2)}</pre>}
          {showJson.connection && <pre className="mt-3 max-h-64 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(modelListResult || {}, null, 2)}</pre>}
        </SectionCard>
      )}

      {tab === "gatekeeper" && (
        <SectionCard title="AI Gatekeeper Benchmark">
          <p className="mb-2 text-xs text-slate-400">Prompt for AI Gatekeeper</p>
          <textarea
            className="mb-3 h-28 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.gatekeeper}
            onChange={(e) => setPrompts((p) => ({ ...p, gatekeeper: e.target.value }))}
          />
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-3">
            <ModelSelect provider={gatekeeperSelection.provider} model={gatekeeperSelection.model} providerModels={providerModels} onProvider={(v) => setGatekeeperSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setGatekeeperSelection((p) => ({ ...p, model: v }))} />
            <ModelSelect provider={gatekeeperSelectionB.provider} model={gatekeeperSelectionB.model} providerModels={providerModels} onProvider={(v) => setGatekeeperSelectionB((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setGatekeeperSelectionB((p) => ({ ...p, model: v }))} />
            <input type="file" className="rounded border border-slate-700 bg-slate-900 px-3 py-2" onChange={(e) => setGatekeeperFile(e.target.files?.[0] || null)} />
          </div>
          <button onClick={() => savePromptToApi("gatekeeper")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Prompt</button>
          <button onClick={runGatekeeper} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Gatekeeper</button>
          <button onClick={runGatekeeperCompare} className="ml-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white">Run A + B</button>
          <button onClick={() => setHistoryMode("gatekeeper")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {gatekeeperError && <p className="mt-2 text-rose-400">{gatekeeperError}</p>}
          {gatekeeperErrorB && <p className="mt-2 text-rose-400">{gatekeeperErrorB}</p>}
          <div className="mt-2 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3">
          <ResultSummary result={gatekeeperResult} />
          {gatekeeperResult?.verdict && (
            <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-sm">
              <p>
                <span className="text-slate-400">Supported:</span>{" "}
                <span className={gatekeeperResult.verdict.is_supported ? "text-emerald-300" : "text-rose-300"}>
                  {gatekeeperResult.verdict.is_supported === null ? "Unknown" : gatekeeperResult.verdict.is_supported ? "Yes" : "No"}
                </span>
              </p>
              <p><span className="text-slate-400">Primary domain:</span> {gatekeeperResult.verdict.primary_domain || "N/A"}</p>
              <p><span className="text-slate-400">Reason:</span> {gatekeeperResult.verdict.reason || "N/A"}</p>
            </div>
          )}
          <button onClick={() => setShowJson((p) => ({ ...p, gatekeeper: !p.gatekeeper }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.gatekeeper ? "Hide JSON" : "Show JSON"}</button>
          {showJson.gatekeeper && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(gatekeeperResult || {}, null, 2)}</pre>}
            </div>
            <div className="rounded border border-slate-700 p-3">
              <ResultSummary result={gatekeeperResultB} />
              {gatekeeperResultB?.verdict && (
                <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-sm">
                  <p><span className="text-slate-400">Supported:</span> <span className={gatekeeperResultB.verdict.is_supported ? "text-emerald-300" : "text-rose-300"}>{gatekeeperResultB.verdict.is_supported === null ? "Unknown" : gatekeeperResultB.verdict.is_supported ? "Yes" : "No"}</span></p>
                  <p><span className="text-slate-400">Primary domain:</span> {gatekeeperResultB.verdict.primary_domain || "N/A"}</p>
                  <p><span className="text-slate-400">Reason:</span> {gatekeeperResultB.verdict.reason || "N/A"}</p>
                </div>
              )}
              <details>
                <summary className="cursor-pointer text-xs text-cyan-300">Show JSON (Case B)</summary>
                <pre className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-2 text-xs">{JSON.stringify(gatekeeperResultB || {}, null, 2)}</pre>
              </details>
            </div>
          </div>
        </SectionCard>
      )}

      {tab === "extracted" && (
        <SectionCard title="AI Extracted Content (Vision + Parser)">
          <p className="mb-2 text-xs text-slate-400">Prompt for AI Extracted Content (Vision)</p>
          <textarea
            className="mb-3 h-28 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.extracted_content_vision}
            onChange={(e) => setPrompts((p) => ({ ...p, extracted_content_vision: e.target.value }))}
          />
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-2">
            <ModelSelect provider={extractedSelection.provider} model={extractedSelection.model} providerModels={providerModels} onProvider={(v) => setExtractedSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setExtractedSelection((p) => ({ ...p, model: v }))} />
            <input type="file" className="rounded border border-slate-700 bg-slate-900 px-3 py-2" onChange={(e) => setExtractedFile(e.target.files?.[0] || null)} />
          </div>
          <button onClick={() => savePromptToApi("extracted_content_vision")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Prompt</button>
          <button onClick={runExtractedContent} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Extracted Content</button>
          <button onClick={() => setHistoryMode("extracted_content")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {extractedError && <p className="mt-2 text-rose-400">{extractedError}</p>}
          <ResultSummary result={extractedResult} />
          <div className="mb-2 grid grid-cols-1 gap-2 md:grid-cols-2">
            <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-1 text-slate-400">Extracted Markdown Preview</p>
              <div className="max-h-48 overflow-auto whitespace-pre-wrap">{extractedResult?.parsedMarkdown || ""}</div>
            </div>
            <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-1 text-slate-400">Chunk Count</p>
              <p>{extractedResult?.chunkedMarkdown?.length || 0}</p>
            </div>
          </div>
          <button onClick={() => setShowJson((p) => ({ ...p, extracted: !p.extracted }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.extracted ? "Hide JSON" : "Show JSON"}</button>
          {showJson.extracted && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(extractedResult || {}, null, 2)}</pre>}
        </SectionCard>
      )}

      {tab === "embedding" && (
        <SectionCard title="AI Embedding Function Benchmark">
          <p className="mb-2 text-xs text-slate-400">Prompt for AI Tagging (gắn topic tag cho chunk)</p>
          <textarea
            className="mb-3 h-24 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.embedding_tagging}
            onChange={(e) => setPrompts((p) => ({ ...p, embedding_tagging: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("embedding_tagging")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Tagging Prompt</button>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-3">
            <div className="rounded border border-slate-700 bg-slate-950/30 p-3 text-xs">
              <p className="mb-1 text-slate-400">Embedding model (Case A - Cohere embed)</p>
              <select className="w-full rounded border border-slate-700 bg-slate-900 px-2 py-2" value={embeddingSelection.model} onChange={(e) => setEmbeddingSelection({ provider: "cohere", model: e.target.value })}>
                {(cohereEmbedModels.length ? cohereEmbedModels : [embeddingSelection.model]).map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
            <ModelSelect provider={embeddingSelectionB.provider} model={embeddingSelectionB.model} providerModels={providerModels} onProvider={(v) => setEmbeddingSelectionB((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setEmbeddingSelectionB((p) => ({ ...p, model: v }))} />
            <input type="file" className="rounded border border-slate-700 bg-slate-900 px-3 py-2" onChange={(e) => setEmbeddingFile(e.target.files?.[0] || null)} />
          </div>
          <div className="mb-3 rounded border border-slate-700 p-3">
            <p className="mb-2 text-sm font-semibold text-cyan-300">Auto Tagging model (Cohere Command)</p>
            <select className="w-full rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" value={embeddingTaggingSelection.model} onChange={(e) => setEmbeddingTaggingSelection({ provider: "cohere", model: e.target.value })}>
              {(cohereCommandModels.length ? cohereCommandModels : [embeddingTaggingSelection.model]).map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
            <div className="mt-3 grid grid-cols-1 gap-2 md:grid-cols-2">
              <input
                className="rounded border border-slate-700 bg-slate-900 px-3 py-2 text-sm"
                placeholder="Subject (vd: java_oop)"
                value={embeddingTaggingContext.subject}
                onChange={(e) => setEmbeddingTaggingContext((p) => ({ ...p, subject: e.target.value }))}
              />
              <input
                className="rounded border border-slate-700 bg-slate-900 px-3 py-2 text-sm"
                placeholder="Language (vd: java)"
                value={embeddingTaggingContext.language}
                onChange={(e) => setEmbeddingTaggingContext((p) => ({ ...p, language: e.target.value }))}
              />
            </div>
            <textarea
              className="mt-2 h-20 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 text-xs"
              placeholder="Allowed tags, cách nhau bởi dấu phẩy"
              value={embeddingTaggingContext.allowedTagsText}
              onChange={(e) => setEmbeddingTaggingContext((p) => ({ ...p, allowedTagsText: e.target.value }))}
            />
          </div>
          <button onClick={runEmbedding} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Embedding</button>
          <button onClick={runEmbeddingCompare} className="ml-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white">Run A + B</button>
          <button onClick={() => setHistoryMode("embedding")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {embeddingResult?.runId && (
            <a
              className="ml-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white"
              href={`${API_BASE_URL}/api/reports/runs/${embeddingResult.runId}.chunks.json`}
              target="_blank"
              rel="noreferrer"
            >
              Export Chunks JSON
            </a>
          )}
          {embeddingError && <p className="mt-2 text-rose-400">{embeddingError}</p>}
          {embeddingErrorB && <p className="mt-2 text-rose-400">{embeddingErrorB}</p>}
          <div className="mt-2 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3">
          <ResultSummary result={embeddingResult} />
          {embeddingResult?.noiseFilter && (
            <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-1 text-slate-300">Noise filter</p>
              <p><span className="text-slate-400">Strategy:</span> {embeddingResult.noiseFilter.strategy}</p>
              <p><span className="text-slate-400">Removed words (estimate):</span> {embeddingResult.noiseFilter.removedWordsEstimate}</p>
              <p><span className="text-slate-400">Dropped short chunks:</span> {embeddingResult.noiseFilter.droppedShortChunks}</p>
            </div>
          )}
          <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="mb-1 text-slate-400">Chunking</p>
            <p>Total chunks: {embeddingResult?.chunkedMarkdown?.length || 0}</p>
          </div>
          <div className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="mb-2 text-slate-300">KnowledgeChunks preview (schema-aligned)</p>
            {(embeddingResult?.knowledgeChunks || []).slice(0, 20).map((c, idx) => (
              <div key={`${c.chunk_id}-${idx}`} className="mb-3 rounded border border-slate-800 p-2">
                <p><span className="text-slate-400">chunk_id:</span> {c.chunk_id}</p>
                <p><span className="text-slate-400">topic_tags:</span> {(c.topic_tags || []).join(", ") || "(none)"}</p>
                <p><span className="text-slate-400">vector_dim:</span> {(c.vector_embedding || []).length}</p>
                <p className="mt-1 whitespace-pre-wrap"><span className="text-slate-400">content_text:</span> {c.content_text}</p>
              </div>
            ))}
          </div>
          <button onClick={() => setShowJson((p) => ({ ...p, embedding: !p.embedding }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.embedding ? "Hide JSON" : "Show JSON"}</button>
          {showJson.embedding && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(embeddingResult || {}, null, 2)}</pre>}
            </div>
            <div className="rounded border border-slate-700 p-3">
              <ResultSummary result={embeddingResultB} />
              <div className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">
                <p className="mb-2 text-slate-300">KnowledgeChunks preview (Case B)</p>
                {(embeddingResultB?.knowledgeChunks || []).slice(0, 10).map((c, idx) => (
                  <div key={`${c.chunk_id}-${idx}`} className="mb-2 rounded border border-slate-800 p-2">
                    <p><span className="text-slate-400">topic_tags:</span> {(c.topic_tags || []).join(", ") || "(none)"}</p>
                    <p className="whitespace-pre-wrap"><span className="text-slate-400">content_text:</span> {c.content_text}</p>
                  </div>
                ))}
              </div>
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-cyan-300">Show JSON (Case B)</summary>
                <pre className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-2 text-xs">{JSON.stringify(embeddingResultB || {}, null, 2)}</pre>
              </details>
            </div>
          </div>
        </SectionCard>
      )}

      {tab === "genreview" && (
        <SectionCard title="Generation + Review Flow">
          <p className="mb-2 text-xs text-slate-400">Prompt for Question Generation</p>
          <textarea
            className="mb-3 h-24 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.generation}
            onChange={(e) => setPrompts((p) => ({ ...p, generation: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("generation")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Generation Prompt</button>
          <p className="mb-2 text-xs text-slate-400">Prompt for Question Review</p>
          <textarea
            className="mb-3 h-24 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.review}
            onChange={(e) => setPrompts((p) => ({ ...p, review: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("review")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Review Prompt</button>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-4">
            <select className="rounded border border-slate-700 bg-slate-900 px-3 py-2" value={genInput.questionType} onChange={(e) => setGenInput((p) => ({ ...p, questionType: e.target.value }))}>
              <option value="multiple_choice">multiple_choice</option>
              <option value="question">question</option>
            </select>
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Domain" value={genInput.domain} onChange={(e) => setGenInput((p) => ({ ...p, domain: e.target.value }))} />
            <select className="rounded border border-slate-700 bg-slate-900 px-3 py-2" value={genInput.difficulty || "Medium"} onChange={(e) => setGenInput((p) => ({ ...p, difficulty: e.target.value }))}>
              <option value="Easy">Easy</option>
              <option value="Medium">Medium</option>
              <option value="Hard">Hard</option>
            </select>
            <input type="number" className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Count" value={genInput.count} onChange={(e) => setGenInput((p) => ({ ...p, count: Number(e.target.value) }))} />
          </div>
          <p className="mb-2 text-xs text-slate-400">Input context chunks (paste retrieved/vector chunks, separate by line `---`)</p>
          <textarea
            className="mb-3 h-28 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            placeholder="Chunk 1 ...&#10;---&#10;Chunk 2 ..."
            value={genInput.contextChunksText || ""}
            onChange={(e) => setGenInput((p) => ({ ...p, contextChunksText: e.target.value }))}
          />
          <p className="mb-2 text-xs text-slate-400">Ground truth / expected quality notes</p>
          <textarea
            className="mb-3 h-24 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 text-xs"
            placeholder="Expected answer quality, constraints, rubric references..."
            value={genInput.groundTruth || ""}
            onChange={(e) => setGenInput((p) => ({ ...p, groundTruth: e.target.value }))}
          />
          <div className="mb-3 rounded border border-slate-700 p-3">
            <p className="mb-2 text-xs text-slate-300">Import chunk JSON (từ Embedding export)</p>
            <input
              type="file"
              accept=".json,application/json"
              className="rounded border border-slate-700 bg-slate-900 px-3 py-2 text-xs"
              onChange={(e) => importChunkJsonToGeneration(e.target.files?.[0] || null)}
            />
          </div>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3"><p className="mb-2 text-sm font-semibold text-cyan-300">Generator model</p><ModelSelect provider={genSelection.provider} model={genSelection.model} providerModels={providerModels} onProvider={(v) => setGenSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setGenSelection((p) => ({ ...p, model: v }))} /></div>
            <div className="rounded border border-slate-700 p-3"><p className="mb-2 text-sm font-semibold text-cyan-300">Reviewer model</p><ModelSelect provider={reviewSelection.provider} model={reviewSelection.model} providerModels={providerModels} onProvider={(v) => setReviewSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setReviewSelection((p) => ({ ...p, model: v }))} /></div>
          </div>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3"><p className="mb-2 text-sm font-semibold text-emerald-300">Generator model (Case B)</p><ModelSelect provider={genSelectionB.provider} model={genSelectionB.model} providerModels={providerModels} onProvider={(v) => setGenSelectionB((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setGenSelectionB((p) => ({ ...p, model: v }))} /></div>
            <div className="rounded border border-slate-700 p-3"><p className="mb-2 text-sm font-semibold text-emerald-300">Reviewer model (Case B)</p><ModelSelect provider={reviewSelectionB.provider} model={reviewSelectionB.model} providerModels={providerModels} onProvider={(v) => setReviewSelectionB((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setReviewSelectionB((p) => ({ ...p, model: v }))} /></div>
          </div>
          <button onClick={runGenReview} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Generation + Review</button>
          <button onClick={runGenReviewCompare} className="ml-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white">Run A + B</button>
          <button onClick={() => setHistoryMode("gen_review")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {genReviewError && <p className="mt-2 text-rose-400">{genReviewError}</p>}
          {genReviewErrorB && <p className="mt-2 text-rose-400">{genReviewErrorB}</p>}
          <div className="mt-2 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3">
          <ResultSummary result={genReviewResult} />
          <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="mb-1 text-slate-400">Generation Schema Validation</p>
            <p className={genReviewResult?.generationSchemaValidation?.valid ? "text-emerald-300" : "text-rose-300"}>
              {genReviewResult?.generationSchemaValidation?.valid ? "PASS" : "FAIL"}
            </p>
            {!genReviewResult?.generationSchemaValidation?.valid && (
              <div className="mt-2 max-h-32 overflow-auto whitespace-pre-wrap text-rose-200">
                {(genReviewResult?.generationSchemaValidation?.errors || []).join("\n")}
              </div>
            )}
          </div>
          <div className="mb-2 grid grid-cols-1 gap-2 md:grid-cols-2">
            <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-1 text-slate-400">Generation Output</p>
              <div className="max-h-48 overflow-auto whitespace-pre-wrap">{genReviewResult?.rawLog?.generatedOutput || ""}</div>
            </div>
            <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-1 text-slate-400">Review Output</p>
              <div className="max-h-48 overflow-auto whitespace-pre-wrap">{genReviewResult?.rawLog?.reviewOutput || ""}</div>
            </div>
          </div>
          <button onClick={() => setShowJson((p) => ({ ...p, genreview: !p.genreview }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.genreview ? "Hide JSON" : "Show JSON"}</button>
          {showJson.genreview && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(genReviewResult || {}, null, 2)}</pre>}
            </div>
            <div className="rounded border border-slate-700 p-3">
              <ResultSummary result={genReviewResultB} />
              <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
                <p className="mb-1 text-slate-400">Generation Schema Validation (Case B)</p>
                <p className={genReviewResultB?.generationSchemaValidation?.valid ? "text-emerald-300" : "text-rose-300"}>
                  {genReviewResultB?.generationSchemaValidation?.valid ? "PASS" : "FAIL"}
                </p>
                {!genReviewResultB?.generationSchemaValidation?.valid && (
                  <div className="mt-2 max-h-32 overflow-auto whitespace-pre-wrap text-rose-200">
                    {(genReviewResultB?.generationSchemaValidation?.errors || []).join("\n")}
                  </div>
                )}
              </div>
              <div className="mb-2 grid grid-cols-1 gap-2">
                <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
                  <p className="mb-1 text-slate-400">Generation Output (Case B)</p>
                  <div className="max-h-48 overflow-auto whitespace-pre-wrap">{genReviewResultB?.rawLog?.generatedOutput || ""}</div>
                </div>
                <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
                  <p className="mb-1 text-slate-400">Review Output (Case B)</p>
                  <div className="max-h-48 overflow-auto whitespace-pre-wrap">{genReviewResultB?.rawLog?.reviewOutput || ""}</div>
                </div>
              </div>
              <details>
                <summary className="cursor-pointer text-xs text-cyan-300">Show JSON (Case B)</summary>
                <pre className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-2 text-xs">{JSON.stringify(genReviewResultB || {}, null, 2)}</pre>
              </details>
            </div>
          </div>
        </SectionCard>
      )}

      {tab === "mentor" && (
        <SectionCard title="AI Mentor Code Benchmark">
          <p className="mb-2 text-xs text-slate-400">Prompt for AI Code Mentor</p>
          <textarea
            className="mb-3 h-28 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.code_mentor}
            onChange={(e) => setPrompts((p) => ({ ...p, code_mentor: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("code_mentor")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Prompt</button>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-3">
            <input className="rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Language" value={mentorInput.language} onChange={(e) => setMentorInput((p) => ({ ...p, language: e.target.value }))} />
            <ModelSelect provider={mentorSelection.provider} model={mentorSelection.model} providerModels={providerModels} onProvider={(v) => setMentorSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setMentorSelection((p) => ({ ...p, model: v }))} />
            <ModelSelect provider={mentorSelectionB.provider} model={mentorSelectionB.model} providerModels={providerModels} onProvider={(v) => setMentorSelectionB((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))} onModel={(v) => setMentorSelectionB((p) => ({ ...p, model: v }))} />
          </div>
          <textarea className="mb-3 h-24 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2" placeholder="Problem statement" value={mentorInput.problem} onChange={(e) => setMentorInput((p) => ({ ...p, problem: e.target.value }))} />
          <textarea className="mb-3 h-40 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono" placeholder="Student code" value={mentorInput.code} onChange={(e) => setMentorInput((p) => ({ ...p, code: e.target.value }))} />
          <button onClick={runMentor} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Code Mentor</button>
          <button onClick={runMentorCompare} className="ml-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white">Run A + B</button>
          <button onClick={() => setHistoryMode("code_mentor")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {mentorError && <p className="mt-2 text-rose-400">{mentorError}</p>}
          {mentorErrorB && <p className="mt-2 text-rose-400">{mentorErrorB}</p>}
          <div className="mt-2 grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded border border-slate-700 p-3">
          <ResultSummary result={mentorResult} />
          <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="mb-1 text-slate-400">Mentor Output</p>
            <div className="max-h-48 overflow-auto whitespace-pre-wrap">{mentorResult?.rawLog?.mentorOutput || ""}</div>
          </div>
          <button onClick={() => setShowJson((p) => ({ ...p, mentor: !p.mentor }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.mentor ? "Hide JSON" : "Show JSON"}</button>
          {showJson.mentor && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(mentorResult || {}, null, 2)}</pre>}
            </div>
            <div className="rounded border border-slate-700 p-3">
              <ResultSummary result={mentorResultB} />
              <div className="rounded border border-slate-700 bg-slate-950 p-3 text-xs">
                <p className="mb-1 text-slate-400">Mentor Output (Case B)</p>
                <div className="max-h-48 overflow-auto whitespace-pre-wrap">{mentorResultB?.rawLog?.mentorOutput || ""}</div>
              </div>
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-cyan-300">Show JSON (Case B)</summary>
                <pre className="mt-2 max-h-72 overflow-auto rounded border border-slate-700 bg-slate-950 p-2 text-xs">{JSON.stringify(mentorResultB || {}, null, 2)}</pre>
              </details>
            </div>
          </div>
        </SectionCard>
      )}

      {tab === "fullflow" && (
        <SectionCard title="Full Flow Benchmark + Monthly Cost Simulation">
          <div className="mb-3 rounded border border-slate-700 p-3">
            <p className="mb-2 text-sm font-semibold text-cyan-300">Full flow from document to question and mentor</p>
            <input type="file" className="mb-2 rounded border border-slate-700 bg-slate-900 px-3 py-2" onChange={(e) => setFullFlowFile(e.target.files?.[0] || null)} />
            <button onClick={runFullFlow} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950">Run Full Flow</button>
            <button onClick={() => setHistoryMode("full_flow")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
            {fullFlowError && <p className="mt-2 text-rose-400">{fullFlowError}</p>}
            <ResultSummary result={fullFlowResult} />
            <details>
              <summary className="cursor-pointer text-xs text-cyan-300">Show JSON</summary>
              <pre className="mt-2 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(fullFlowResult || {}, null, 2)}</pre>
            </details>
          </div>

          <div className="rounded border border-slate-700 p-3">
            <p className="mb-2 text-sm font-semibold text-emerald-300">Monthly cost simulation</p>
            <div className="grid grid-cols-1 gap-2 md:grid-cols-5">
              <input className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" type="number" placeholder="Students" value={simulationInput.students} onChange={(e) => setSimulationInput((p) => ({ ...p, students: Number(e.target.value) }))} />
              <input className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" type="number" placeholder="FE/student" value={simulationInput.feGenerationPerStudent} onChange={(e) => setSimulationInput((p) => ({ ...p, feGenerationPerStudent: Number(e.target.value) }))} />
              <input className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" type="number" placeholder="PE/student" value={simulationInput.peGenerationPerStudent} onChange={(e) => setSimulationInput((p) => ({ ...p, peGenerationPerStudent: Number(e.target.value) }))} />
              <input className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" type="number" placeholder="BYOS/student" value={simulationInput.byosPerStudent} onChange={(e) => setSimulationInput((p) => ({ ...p, byosPerStudent: Number(e.target.value) }))} />
              <input className="rounded border border-slate-700 bg-slate-900 px-2 py-2 text-sm" type="number" placeholder="Mentor/student" value={simulationInput.mentorPerStudent} onChange={(e) => setSimulationInput((p) => ({ ...p, mentorPerStudent: Number(e.target.value) }))} />
            </div>
            <button onClick={runMonthlySimulation} className="mt-2 rounded bg-emerald-600 px-4 py-2 font-semibold text-white">Run Simulation</button>
            <button onClick={() => setHistoryMode("full_flow_simulation")} className="ml-2 mt-2 rounded bg-slate-700 px-4 py-2 font-semibold">View Simulation History</button>
            {simulationError && <p className="mt-2 text-rose-400">{simulationError}</p>}
            <ResultSummary result={simulationResult} />
            <details>
              <summary className="cursor-pointer text-xs text-cyan-300">Show JSON</summary>
              <pre className="mt-2 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(simulationResult || {}, null, 2)}</pre>
            </details>
          </div>
        </SectionCard>
      )}
      {tab === "chunking" && (
        <SectionCard title="Chunking Logic + Embedding Benchmark">
          <p className="mb-2 text-xs text-slate-400">Prompt for AI Vision (embedded images)</p>
          <textarea
            className="mb-2 h-20 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.extracted_content_vision}
            onChange={(e) => setPrompts((p) => ({ ...p, extracted_content_vision: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("extracted_content_vision")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Vision Prompt</button>

          <p className="mb-2 text-xs text-slate-400">Prompt for AI Auto Tagging</p>
          <textarea
            className="mb-2 h-20 w-full rounded border border-slate-700 bg-slate-900 px-3 py-2 font-mono text-xs"
            value={prompts.embedding_tagging}
            onChange={(e) => setPrompts((p) => ({ ...p, embedding_tagging: e.target.value }))}
          />
          <button onClick={() => savePromptToApi("embedding_tagging")} className="mb-3 rounded bg-amber-500 px-4 py-2 font-semibold text-slate-950">Save Tagging Prompt</button>

          <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="text-slate-300">
              Mode 1 (Text only): Input file -&gt; Lib extract text -&gt; Logical block chunking -&gt; Cohere Embedding -&gt; Cohere Command Auto Tagging.
            </p>
            <p className="mt-1 text-slate-300">
              Mode 2 (Full multimodal page): Input file (text + image) -&gt; tách page/image -&gt; Vision đọc từng page/image -&gt; text có marker [PAGE n] -&gt; Cohere Embedding -&gt; Cohere Command Auto Tagging.
            </p>
          </div>
          <div className="mb-3">
            <label className="mb-1 block text-xs text-slate-400">Processing Strategy</label>
            <select className="rounded border border-slate-700 bg-slate-900 px-3 py-2 text-sm" value={chunkingStrategy} onChange={(e) => setChunkingStrategy(e.target.value)}>
              <option value="text_only">Text-only (logical chunking, no vision)</option>
              <option value="full_multimodal_page">Full multimodal by page/image (vision)</option>
            </select>
          </div>
          <div className="mb-3 grid grid-cols-1 gap-3 md:grid-cols-4">
            <div className="rounded border border-slate-700 bg-slate-950/30 p-3 text-xs">
              <p className="mb-1 text-slate-400">Embedding model (Cohere embed)</p>
              <select className="w-full rounded border border-slate-700 bg-slate-900 px-2 py-2" value={chunkingSelection.model} onChange={(e) => setChunkingSelection({ provider: "cohere", model: e.target.value })}>
                {(cohereEmbedModels.length ? cohereEmbedModels : [chunkingSelection.model]).map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
            <ModelSelect
              provider={chunkingVisionSelection.provider}
              model={chunkingVisionSelection.model}
              providerModels={providerModels}
              onProvider={(v) => setChunkingVisionSelection((p) => ({ ...p, provider: v, model: (providerModels[v] || [p.model])[0] || p.model }))}
              onModel={(v) => setChunkingVisionSelection((p) => ({ ...p, model: v }))}
            />
            <div className="rounded border border-slate-700 bg-slate-950/30 p-3 text-xs">
              <p className="mb-1 text-slate-400">Tagging model (Cohere command)</p>
              <select className="w-full rounded border border-slate-700 bg-slate-900 px-2 py-2" value={chunkingTaggingSelection.model} onChange={(e) => setChunkingTaggingSelection({ provider: "cohere", model: e.target.value })}>
                {(cohereCommandModels.length ? cohereCommandModels : [chunkingTaggingSelection.model]).map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
            <input type="file" className="rounded border border-slate-700 bg-slate-900 px-3 py-2" onChange={(e) => setChunkingFile(e.target.files?.[0] || null)} />
          </div>
          {chunkingVisionSelection.provider === "openai" && !String(chunkingVisionSelection.model || "").toLowerCase().includes("4o") && (
            <p className="mb-2 text-amber-300 text-xs">
              Warning: Selected OpenAI vision model may not support image input on your endpoint. Prefer `gpt-4o`/`gpt-4o-mini` or use Gemini vision.
            </p>
          )}
          <button disabled={chunkingLoading} onClick={runChunkingLogic} className="rounded bg-cyan-500 px-4 py-2 font-semibold text-slate-950 disabled:opacity-60">
            {chunkingLoading ? "Running..." : "Run Chunking Logic"}
          </button>
          <button onClick={() => setHistoryMode("chunking_logic")} className="ml-2 rounded bg-slate-700 px-4 py-2 font-semibold">View History</button>
          {chunkingError && <p className="mt-2 text-rose-400">{chunkingError}</p>}
          {!!chunkingRunLogs.length && (
            <div className="mt-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p className="mb-2 text-slate-300">Run logs</p>
              {chunkingRunLogs.map((l, i) => (
                <p key={i} className="text-slate-400">- {l}</p>
              ))}
            </div>
          )}
          <ResultSummary result={chunkingResult} />
          {chunkingResult?.processingStrategy && (
            <div className="mb-2 rounded border border-slate-700 bg-slate-950 p-3 text-xs">
              <p><span className="text-slate-400">Strategy requested:</span> {chunkingResult.processingStrategy.requested}</p>
              <p><span className="text-slate-400">Strategy applied:</span> {chunkingResult.processingStrategy.applied}</p>
              {(chunkingResult.processingStrategy.notes || []).map((n, i) => (
                <p key={i} className="text-amber-300">{n}</p>
              ))}
            </div>
          )}
          {chunkingResult?.visionExtraction && (
            <div className="mb-2 grid grid-cols-2 gap-2 md:grid-cols-5">
              <MetricCard label="Embedded Images" value={chunkingResult.visionExtraction.embeddedImagesDetected || 0} />
              <MetricCard label="Vision Input Tok" value={chunkingResult.visionExtraction.inputTokens || 0} />
              <MetricCard label="Vision Output Tok" value={chunkingResult.visionExtraction.outputTokens || 0} />
              <MetricCard label="Vision Cost" value={currency(chunkingResult.visionExtraction.totalCostUsd || 0)} />
              <MetricCard label="Vision Latency" value={chunkingResult.visionExtraction.latency_ms || 0} />
            </div>
          )}
          {chunkingResult?.tagging && (
            <div className="mb-2 grid grid-cols-2 gap-2 md:grid-cols-5">
              <MetricCard label="Tag Input Tok" value={chunkingResult.tagging.inputTokens || 0} />
              <MetricCard label="Tag Output Tok" value={chunkingResult.tagging.outputTokens || 0} />
              <MetricCard label="Tag Input Cost" value={currency(chunkingResult.tagging.inputCostUsd || 0)} />
              <MetricCard label="Tag Output Cost" value={currency(chunkingResult.tagging.outputCostUsd || 0)} />
              <MetricCard label="Tag Total Cost" value={currency(chunkingResult.tagging.totalCostUsd || 0)} />
            </div>
          )}
          <div className="mt-2 max-h-80 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">
            <p className="mb-2 text-slate-300">Logic blocks preview</p>
            {Number(chunkingResult?.logicChunking?.droppedBlockCount || 0) > 0 && (
              <p className="mb-2 text-amber-300">
                Noise filter dropped {chunkingResult.logicChunking.droppedBlockCount} block(s).
              </p>
            )}
            {(chunkingResult?.logicChunking?.blocks || []).slice(0, 40).map((b) => (
              <div key={b.blockIndex} className="mb-2 rounded border border-slate-800 p-2">
                <p><span className="text-slate-400">block:</span> #{b.blockIndex} | {b.blockType} | {b.words} words</p>
                <p><span className="text-slate-400">embed cost:</span> {currency(b?.embedding?.totalCostUsd || 0)} | tokens: {b?.embedding?.inputTokens || 0}</p>
                <p><span className="text-slate-400">topic_tags:</span> {(b?.topic_tags || []).join(", ") || "(none)"}</p>
                <p className="whitespace-pre-wrap"><span className="text-slate-400">text:</span> {b.text}</p>
              </div>
            ))}
          </div>
          <button onClick={() => setShowJson((p) => ({ ...p, chunking: !p.chunking }))} className="mt-2 rounded bg-slate-800 px-3 py-1 text-xs">{showJson.chunking ? "Hide JSON" : "Show JSON"}</button>
          {showJson.chunking && <pre className="mt-3 max-h-96 overflow-auto rounded border border-slate-700 bg-slate-950 p-3 text-xs">{JSON.stringify(chunkingResult || {}, null, 2)}</pre>}
        </SectionCard>
      )}
      {promptStatus && <p className="mb-4 text-sm text-amber-300">{promptStatus}</p>}
      <RunHistoryModal mode={historyMode} open={Boolean(historyMode)} onClose={() => setHistoryMode("")} />
    </div>
  );
}

export default App;
