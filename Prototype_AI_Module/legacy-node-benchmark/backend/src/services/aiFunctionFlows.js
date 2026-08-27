import OpenAI from "openai";
import { GoogleGenerativeAI } from "@google/generative-ai";
import { performance } from "node:perf_hooks";
import { config } from "../config.js";
import { extractEmbeddedImagesFromOfficeBuffer, parseFileToMarkdown } from "./fileParser.js";

const PRICING = {
  "gpt-4o": { input: 2.5, output: 10 },
  "gpt-4o-mini": { input: 0.15, output: 0.6 },
  "gpt-5.1": { input: 0, output: 0 },
  "gemini-3.1-flash": { input: 0.075, output: 0.3 },
  "gemini-3.1-flash-lite": { input: 0.075, output: 0.3 },
  "gemini-3.5-flash": { input: 0.075, output: 0.3 },
  "text-embedding-3-small": { input: 0.02, output: 0 },
  "embed-multilingual-v3.0": { input: Number(config.cohereInputCostPer1M || 0), output: 0 },
  "embed-english-v3.0": { input: Number(config.cohereInputCostPer1M || 0), output: 0 },
  "command-r7b-12-2024": { input: 0.0375, output: 0.15 },
};

const calcCost = (model, inTok, outTok) => {
  const p = PRICING[model];
  if (!p) return 0;
  return Number((((inTok / 1_000_000) * p.input) + ((outTok / 1_000_000) * p.output)).toFixed(8));
};
const calcCostBreakdown = (model, inTok = 0, outTok = 0) => {
  const p = PRICING[model];
  if (!p) return { inputCostUsd: 0, outputCostUsd: 0, totalCostUsd: 0 };
  const inputCostUsd = Number((((inTok || 0) / 1_000_000) * p.input).toFixed(8));
  const outputCostUsd = Number((((outTok || 0) / 1_000_000) * p.output).toFixed(8));
  return {
    inputCostUsd,
    outputCostUsd,
    totalCostUsd: Number((inputCostUsd + outputCostUsd).toFixed(8)),
  };
};

const countWords = (text = "") => (text.trim() ? text.trim().split(/\s+/).length : 0);
const chunkByWords = (text = "", chunkWords = 500) => {
  const words = text.trim().split(/\s+/).filter(Boolean);
  const chunks = [];
  for (let i = 0; i < words.length; i += chunkWords) chunks.push(words.slice(i, i + chunkWords).join(" "));
  return chunks;
};
const splitLogicalBlocks = (markdown = "") => {
  const lines = String(markdown || "").split(/\r?\n/);
  const blocks = [];
  let current = [];
  let blockType = "paragraph";

  const flush = () => {
    const text = current.join("\n").trim();
    if (!text) return;
    blocks.push({ blockType, text });
    current = [];
    blockType = "paragraph";
  };

  for (const raw of lines) {
    const line = raw.trimEnd();
    if (!line.trim()) {
      flush();
      continue;
    }
    if (/^#{1,6}\s/.test(line)) {
      flush();
      blockType = "heading";
      current.push(line);
      continue;
    }
    if (/^[-*]\s+/.test(line) || /^\d+\.\s+/.test(line)) {
      if (!current.length) blockType = "list";
      if (blockType !== "list") flush(), (blockType = "list");
      current.push(line);
      continue;
    }
    if (/^\|.+\|$/.test(line)) {
      if (!current.length) blockType = "table";
      if (blockType !== "table") flush(), (blockType = "table");
      current.push(line);
      continue;
    }
    if (!current.length) blockType = "paragraph";
    if (blockType !== "paragraph") flush(), (blockType = "paragraph");
    current.push(line);
  }
  flush();
  return blocks.map((b, i) => ({
    blockIndex: i,
    blockType: b.blockType,
    text: b.text,
    words: countWords(b.text),
  }));
};
const sanitizeRichText = (text = "") =>
  String(text || "")
    // Drop markdown images to avoid noisy non-text chunks.
    .replace(/!\[[^\]]*]\([^)]*\)/g, " ")
    // Remove HTML tags but keep inner text.
    .replace(/<br\s*\/?>/gi, "\n")
    .replace(/<\/?div[^>]*>/gi, " ")
    .replace(/<\/?span[^>]*>/gi, " ")
    .replace(/<\/?u>/gi, "")
    .replace(/<\/?em>/gi, "")
    .replace(/<\/?strong>/gi, "")
    .replace(/<\/?p[^>]*>/gi, "\n")
    .replace(/<[^>]+>/g, " ")
    // Remove anchor/id markers.
    .replace(/\{#.+?\}/g, " ")
    // Normalize unusual arrows/symbols from PPT/PDF conversions.
    .replace(/[‐‑‒–—]/g, "-")
    .replace(/[•·]/g, "-")
    .replace(/[→➜⇒]/g, " -> ")
    .replace(/[“”]/g, "\"")
    .replace(/[‘’]/g, "'")
    .replace(/\u00A0/g, " ");

const normalizeInlineText = (text = "") =>
  sanitizeRichText(text)
    .replace(/[ \t]+/g, " ")
    .replace(/\n{3,}/g, "\n\n")
    .trim();

const NOISE_PATTERNS = [
  /^-{2,}$/,
  /^\d{1,4}$/,
  /^\d{1,2}[/-]\d{1,2}[/-]\d{2,4}$/,
  /^\d{4}-\d{2}-\d{2}$/,
  /^>?\s*\*{0,2}note\*{0,2}\s*:?\s*\d*$/i,
  /^\{#.+\}$/,
  /^page\s+\d+$/i,
];

const isNoiseLine = (line = "") => {
  const t = normalizeInlineText(line);
  if (!t) return true;
  return NOISE_PATTERNS.some((r) => r.test(t));
};

const cleanMarkdownForEmbedding = (markdown = "") => {
  let text = String(markdown || "");
  // Drop YAML frontmatter if present.
  text = text.replace(/^---\s*\n[\s\S]*?\n---\s*\n?/m, "");
  text = sanitizeRichText(text);
  const lines = text
    .split(/\r?\n/)
    .map((l) => l.trimEnd())
    .map((l) => l.replace(/^>\s*\*{0,2}note\*{0,2}\s*:?\s*/i, ""))
    .filter((l) => !isNoiseLine(l));
  return normalizeInlineText(lines.join("\n"));
};

const filterLogicalBlocks = (blocks = []) => {
  const dropped = [];
  const kept = [];
  for (const b of blocks) {
    const cleaned = normalizeInlineText(String(b?.text || "").replace(/^>\s*\*{0,2}note\*{0,2}\s*:?\s*/i, "").trim());
    const words = countWords(cleaned);
    const nonAlnum = (cleaned.match(/[^a-zA-Z0-9\u00C0-\u024F]/g) || []).length;
    const ratio = cleaned.length ? nonAlnum / cleaned.length : 1;
    const tooShort = words < 3 && cleaned.length < 24;
    const mostlySymbols = ratio > 0.6 && words < 8;
    const singleNoiseLine = cleaned.split(/\r?\n/).every((ln) => isNoiseLine(ln));
    if (!cleaned || tooShort || mostlySymbols || singleNoiseLine) {
      dropped.push({
        blockIndex: b?.blockIndex ?? -1,
        blockType: b?.blockType || "unknown",
        reason: !cleaned ? "empty" : tooShort ? "too_short" : mostlySymbols ? "mostly_symbols" : "noise_pattern",
        preview: cleaned.slice(0, 120),
      });
      continue;
    }
    kept.push({
      ...b,
      text: cleaned,
      words,
    });
  }
  return { kept, dropped };
};
const fillTemplate = (template = "", vars = {}) =>
  template.replace(/\{(\w+)\}/g, (_m, key) => `${vars?.[key] ?? ""}`);
const safeJsonParse = (text = "") => {
  try {
    return JSON.parse(text);
  } catch {
    const m = text.match(/\{[\s\S]*\}|\[[\s\S]*\]/);
    if (!m) return null;
    try {
      return JSON.parse(m[0]);
    } catch {
      return null;
    }
  }
};
const normalizeTags = (candidate, allowedTags = []) => {
  const allowed = new Set((allowedTags || []).map((t) => String(t).trim().toLowerCase()).filter(Boolean));
  const list = Array.isArray(candidate)
    ? candidate
    : Array.isArray(candidate?.topic_tags)
      ? candidate.topic_tags
      : [];
  const cleaned = list
    .map((t) => String(t || "").trim())
    .filter(Boolean)
    .filter((t) => !allowed.size || allowed.has(t.toLowerCase()));
  return [...new Set(cleaned)].slice(0, 8);
};
const parseSupportedVerdict = (text = "") => {
  const parsed = safeJsonParse(text);
  if (parsed && typeof parsed === "object") {
    const isSupported = typeof parsed.is_supported === "boolean" ? parsed.is_supported : null;
    return {
      is_supported: isSupported,
      primary_domain: parsed.primary_domain || null,
      reason: parsed.reason || null,
      raw: parsed,
    };
  }
  const low = String(text || "").toLowerCase();
  if (low.includes("is_supported\":true") || low.includes("supported")) {
    return { is_supported: true, primary_domain: null, reason: null, raw: null };
  }
  if (low.includes("is_supported\":false") || low.includes("not supported") || low.includes("unsupported")) {
    return { is_supported: false, primary_domain: null, reason: null, raw: null };
  }
  return { is_supported: null, primary_domain: null, reason: null, raw: null };
};
const validateQuestionBankItem = (item, expectedType) => {
  const errors = [];
  const required = ["type", "topic_tags", "difficulty", "title", "description", "skeleton_code", "solution_code", "test_cases", "options", "explanation"];
  for (const f of required) {
    if (!(f in item)) errors.push(`missing_field:${f}`);
  }
  if (!["FE", "PE"].includes(item?.type)) errors.push("invalid_type_value");
  if (expectedType && item?.type !== expectedType) errors.push(`unexpected_type:${item?.type}`);
  if (!Array.isArray(item?.topic_tags)) errors.push("topic_tags_not_array");
  if (!["Easy", "Medium", "Hard"].includes(item?.difficulty)) errors.push("invalid_difficulty");
  if (typeof item?.title !== "string" || !item.title.trim()) errors.push("invalid_title");
  if (typeof item?.description !== "string" || !item.description.trim()) errors.push("invalid_description");

  if (item?.type === "FE") {
    if (item?.skeleton_code !== null) errors.push("fe_skeleton_code_must_be_null");
    if (item?.solution_code !== null) errors.push("fe_solution_code_must_be_null");
    if (item?.test_cases !== null) errors.push("fe_test_cases_must_be_null");
    if (!Array.isArray(item?.options) || item.options.length < 2) errors.push("fe_options_invalid");
  }
  if (item?.type === "PE") {
    if (!Array.isArray(item?.skeleton_code) || !item.skeleton_code.length) errors.push("pe_skeleton_code_required");
    if (!Array.isArray(item?.solution_code) || !item.solution_code.length) errors.push("pe_solution_code_required");
    if (!Array.isArray(item?.test_cases) || !item.test_cases.length) errors.push("pe_test_cases_required");
    if (item?.options !== null) errors.push("pe_options_must_be_null");
  }
  return { valid: errors.length === 0, errors };
};
const validateQuestionBankOutput = (text, expectedType) => {
  const parsed = safeJsonParse(text);
  if (!Array.isArray(parsed)) {
    return { valid: false, errors: ["output_not_json_array"], items: [] };
  }
  const itemResults = parsed.map((item, idx) => ({ index: idx, ...validateQuestionBankItem(item || {}, expectedType) }));
  const errors = itemResults.flatMap((r) => r.errors.map((e) => `item_${r.index}:${e}`));
  return {
    valid: errors.length === 0,
    errors,
    items: parsed,
    itemResults,
  };
};

const buildOpenAI = (credentials = {}) => {
  const apiKey = credentials.openaiApiKey || config.openaiApiKey;
  const baseURL = credentials.openaiBaseUrl || config.openaiBaseUrl;
  return new OpenAI({ apiKey, ...(baseURL ? { baseURL } : {}) });
};

const buildGeminiModel = (credentials = {}, model) => {
  const apiKey = credentials.geminiApiKey || config.geminiApiKey;
  const genAI = new GoogleGenerativeAI(apiKey || "");
  return genAI.getGenerativeModel({ model });
};

const runChat = async ({ provider, model, prompt, credentials, systemPrompt = "You are a helpful assistant." }) => {
  const t0 = performance.now();
  if (provider === "cohere") {
    const apiKey = credentials.cohereApiKey || config.cohereApiKey;
    const base = credentials.cohereBaseUrl || config.cohereBaseUrl;
    const resp = await fetch(`${base}/v2/chat`, {
      method: "POST",
      headers: { Authorization: `Bearer ${apiKey}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        model,
        messages: [{ role: "system", content: systemPrompt }, { role: "user", content: prompt }],
        temperature: 0.2,
      }),
    });
    const raw = await resp.text();
    const json = raw ? JSON.parse(raw) : {};
    if (!resp.ok) throw new Error(json?.message || `Cohere chat failed ${resp.status}`);
    const output =
      json?.message?.content
        ?.map((c) => (typeof c?.text === "string" ? c.text : ""))
        .join("\n")
        .trim() ||
      json?.text ||
      "";
    const inputTokens = Number(
      json?.usage?.tokens?.input_tokens ??
        json?.meta?.billed_units?.input_tokens ??
        json?.meta?.tokens?.input_tokens ??
        0
    );
    const outputTokens = Number(
      json?.usage?.tokens?.output_tokens ??
        json?.meta?.billed_units?.output_tokens ??
        json?.meta?.tokens?.output_tokens ??
        0
    );
    const cost = calcCostBreakdown(model, inputTokens, outputTokens);
    return {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens,
      outputTokens,
      ...cost,
      output,
    };
  }

  if (provider === "gemini") {
    const gem = buildGeminiModel(credentials, model);
    const result = await gem.generateContent(prompt);
    const r = result.response;
    const inputTokens = Number(r?.usageMetadata?.promptTokenCount || 0);
    const outputTokens = Number(r?.usageMetadata?.candidatesTokenCount || 0);
    const cost = calcCostBreakdown(model, inputTokens, outputTokens);
    return {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens,
      outputTokens,
      ...cost,
      output: r?.text?.() || "",
    };
  }

  const openai = buildOpenAI(credentials);
  const response = await openai.chat.completions.create({
    model,
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: prompt },
    ],
    temperature: 0.2,
  });
  const inputTokens = Number(response.usage?.prompt_tokens || 0);
  const outputTokens = Number(response.usage?.completion_tokens || 0);
  const cost = calcCostBreakdown(model, inputTokens, outputTokens);
  return {
    latency_ms: Number((performance.now() - t0).toFixed(2)),
    inputTokens,
    outputTokens,
    ...cost,
    output: response.choices?.[0]?.message?.content || "",
  };
};

const runEmbedding = async ({ provider, model, text, credentials }) => {
  const t0 = performance.now();
  if (provider === "cohere") {
    if (!/^embed/i.test(String(model || ""))) {
      throw new Error(`Invalid Cohere embedding model: ${model}. Please select an embed-* model.`);
    }
    const apiKey = credentials.cohereApiKey || config.cohereApiKey;
    const base = credentials.cohereBaseUrl || config.cohereBaseUrl;
    const resp = await fetch(`${base}/v2/embed`, {
      method: "POST",
      headers: { Authorization: `Bearer ${apiKey}`, "Content-Type": "application/json" },
      body: JSON.stringify({ model, input_type: "search_document", embedding_types: ["float"], texts: [text] }),
    });
    const raw = await resp.text();
    const json = raw ? JSON.parse(raw) : {};
    if (!resp.ok) throw new Error(json?.message || `Cohere embedding failed ${resp.status}`);
    const inputTokens = Number(json?.meta?.billed_units?.input_tokens || 0);
    const cost = calcCostBreakdown(model, inputTokens, 0);
    return {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens,
      outputTokens: 0,
      ...cost,
      embeddingDimensions: json?.embeddings?.float?.[0]?.length || 0,
      vectorEmbedding: json?.embeddings?.float?.[0] || [],
    };
  }

  const openai = buildOpenAI(credentials);
  const response = await openai.embeddings.create({ model, input: text });
  const inputTokens = Number(response.usage?.prompt_tokens ?? response.usage?.input_tokens ?? 0);
  const cost = calcCostBreakdown(model, inputTokens, 0);
  return {
    latency_ms: Number((performance.now() - t0).toFixed(2)),
    inputTokens,
    outputTokens: 0,
    ...cost,
    embeddingDimensions: response.data?.[0]?.embedding?.length || 0,
    vectorEmbedding: response.data?.[0]?.embedding || [],
  };
};

const runVisionForImage = async ({ provider, imageBuffer, mimeType, model, credentials, prompt }) => {
  const t0 = performance.now();
  const finalPrompt =
    prompt ||
    "Extract all readable text and summarize diagram semantics in markdown. Keep relation arrows and hierarchy if present.";

  if ((provider || "gemini") === "gemini") {
    const gem = buildGeminiModel(credentials, model);
    const result = await gem.generateContent([
      { text: finalPrompt },
      {
        inlineData: {
          mimeType: mimeType || "image/png",
          data: imageBuffer.toString("base64"),
        },
      },
    ]);
    const r = result.response;
    const inputTokens = Number(r?.usageMetadata?.promptTokenCount || 0);
    const outputTokens = Number(r?.usageMetadata?.candidatesTokenCount || 0);
    const cost = calcCostBreakdown(model, inputTokens, outputTokens);
    return {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens,
      outputTokens,
      ...cost,
      output: r?.text?.() || "",
    };
  }

  if (provider === "openai") {
    const openai = buildOpenAI(credentials);
    const dataUrl = `data:${mimeType || "image/png"};base64,${imageBuffer.toString("base64")}`;
    const response = await openai.chat.completions.create({
      model,
      messages: [
        {
          role: "user",
          content: [
            { type: "text", text: finalPrompt },
            { type: "image_url", image_url: { url: dataUrl } },
          ],
        },
      ],
      temperature: 0.2,
    });
    const inputTokens = Number(response.usage?.prompt_tokens || 0);
    const outputTokens = Number(response.usage?.completion_tokens || 0);
    const cost = calcCostBreakdown(model, inputTokens, outputTokens);
    return {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens,
      outputTokens,
      ...cost,
      output: response.choices?.[0]?.message?.content || "",
    };
  }

  throw new Error(`Vision is not supported for provider: ${provider}`);
};

export const testConnections = async ({ credentials = {} }) => {
  const out = { openai: { status: "skipped" }, gemini: { status: "skipped" }, cohere: { status: "skipped" } };

  try {
    if (credentials.openaiApiKey || config.openaiApiKey) {
      const openai = buildOpenAI(credentials);
      await openai.chat.completions.create({ model: "gpt-4o-mini", messages: [{ role: "user", content: "ping" }], max_tokens: 5 });
      out.openai = { status: "ok", baseUrl: credentials.openaiBaseUrl || config.openaiBaseUrl || "https://api.openai.com/v1" };
    }
  } catch (e) { out.openai = { status: "failed", error: e.message }; }

  try {
    if (credentials.geminiApiKey || config.geminiApiKey) {
      const g = buildGeminiModel(credentials, "gemini-3.1-flash");
      await g.generateContent("ping");
      out.gemini = { status: "ok" };
    }
  } catch (e) { out.gemini = { status: "failed", error: e.message }; }

  try {
    if (credentials.cohereApiKey || config.cohereApiKey) {
      await runEmbedding({ provider: "cohere", model: "embed-multilingual-v3.0", text: "ping", credentials });
      out.cohere = { status: "ok", baseUrl: credentials.cohereBaseUrl || config.cohereBaseUrl };
    }
  } catch (e) { out.cohere = { status: "failed", error: e.message }; }

  return { runAt: new Date().toISOString(), results: out };
};

export const benchmarkGatekeeper = async ({ file, selection, credentials, promptTemplate }) => {
  const parsed = await parseFileToMarkdown(file, {
    credentials,
    visionProvider: selection?.provider,
    visionModel: selection?.model,
    visionPrompt: promptTemplate,
  });
  const prompt = fillTemplate(promptTemplate || "", {
    material: (parsed.markdown || "").slice(0, 12000),
  }) || `Classify if this learning material is supported by system scope (C intro, Java OOP, DSA OOP). Return strict JSON: {is_supported:boolean,primary_domain:string,reason:string}.\\n\\nMaterial:\\n${(parsed.markdown || "").slice(0, 12000)}`;
  const ai = await runChat({ provider: selection.provider, model: selection.model, prompt, credentials, systemPrompt: "You are an academic gatekeeper classifier." });
  const verdict = parseSupportedVerdict(ai.output);
  return {
    functionName: "AI_Gatekeeper",
    file: { name: file.originalname, type: file.mimetype, sizeBytes: file.size },
    parse: { parser: parsed.parserType, textChars: (parsed.markdown || "").length },
    ai: { provider: selection.provider, model: selection.model, ...ai },
    verdict,
    rawLog: { parsedUsage: parsed.usage || {}, aiOutput: ai.output },
  };
};

export const benchmarkEmbedding = async ({ file, selection, credentials, taggingSelection, taggingPromptTemplate, taggingContext = {} }) => {
  const t0 = performance.now();
  const parsed = await parseFileToMarkdown(file, {
    credentials,
    visionModel: "gemini-3.5-flash",
  });
  const markdown = parsed.markdown || "";
  const cleanedMarkdown = cleanMarkdownForEmbedding(markdown);
  const chunks = chunkByWords(cleanedMarkdown, 500).filter((c) => countWords(c) >= 12);
  const rows = [];
  let totalInput = 0;
  let totalOutput = 0;
  let totalInputCost = 0;
  let totalOutputCost = 0;
  let totalCost = 0;
  let tagInput = 0;
  let tagOutput = 0;
  let tagInputCost = 0;
  let tagOutputCost = 0;
  let tagTotalCost = 0;
  let embedLatency = 0;
  let tagLatency = 0;
  const knowledgeChunks = [];
  for (let i = 0; i < chunks.length; i += 1) {
    const chunkText = chunks[i];
    const r = await runEmbedding({ provider: selection.provider, model: selection.model, text: chunkText, credentials });
    embedLatency += r.latency_ms;
    totalInput += r.inputTokens;
    totalOutput += r.outputTokens || 0;
    totalInputCost += r.inputCostUsd || 0;
    totalOutputCost += r.outputCostUsd || 0;
    totalCost += r.totalCostUsd;
    let tagging = null;
    let topicTags = [];
    if (taggingSelection?.model) {
      const taggingProvider = taggingSelection?.provider || "cohere";
      const tagPrompt = fillTemplate(
        taggingPromptTemplate ||
          "Bạn là AI gắn nhãn topic cho chunk kiến thức lập trình. Dựa vào taxonomy: {allowedTags}. Môn học: {subject}. Ngôn ngữ: {language}. Trả về JSON duy nhất dạng {\"topic_tags\":[\"tag1\",\"tag2\"],\"confidence\":0.0-1.0}. Chunk: {chunk}",
        {
          allowedTags: (taggingContext?.allowedTags || []).join(", "),
          subject: taggingContext?.subject || "general_programming",
          language: taggingContext?.language || "general",
          chunk: chunkText.slice(0, 5000),
        }
      );
      const tr = await runChat({
        provider: taggingProvider,
        model: taggingSelection.model,
        prompt: tagPrompt,
        credentials,
        systemPrompt: "You are a strict classifier. Return JSON only.",
      });
      tagLatency += tr.latency_ms;
      tagInput += tr.inputTokens || 0;
      tagOutput += tr.outputTokens || 0;
      tagInputCost += tr.inputCostUsd || 0;
      tagOutputCost += tr.outputCostUsd || 0;
      tagTotalCost += tr.totalCostUsd || 0;
      const parsedTag = safeJsonParse(tr.output);
      topicTags = normalizeTags(parsedTag, taggingContext?.allowedTags || []);
      tagging = {
        provider: taggingProvider,
        model: taggingSelection.model,
        prompt: tagPrompt,
        ...tr,
        parsed: parsedTag,
        topic_tags: topicTags,
      };
    }

    const knowledgeChunk = {
      chunk_id: `chunk_${i + 1}`,
      topic_tags: topicTags,
      content_text: chunkText,
      vector_embedding: r.vectorEmbedding || [],
      metadata: {
        words: countWords(chunkText),
        embedding_model: selection.model,
        embedding_provider: selection.provider,
      },
    };
    knowledgeChunks.push(knowledgeChunk);

    rows.push({ chunkIndex: i, words: countWords(chunkText), embedding: r, tagging });
  }
  return {
    functionName: "AI_Embedding",
    file: { name: file.originalname, type: file.mimetype, sizeBytes: file.size },
    parse: {
      parser: parsed.parserType,
      words: countWords(markdown),
      cleanedWords: countWords(cleanedMarkdown),
      chunks: chunks.length,
    },
    noiseFilter: {
      enabled: true,
      strategy: "line_cleaning_before_500w_chunking",
      removedWordsEstimate: Math.max(0, countWords(markdown) - countWords(cleanedMarkdown)),
      droppedShortChunks: chunkByWords(cleanedMarkdown, 500).length - chunks.length,
    },
    embedding: {
      provider: selection.provider,
      model: selection.model,
      latency_ms: Number(embedLatency.toFixed(2)),
      inputTokens: totalInput,
      outputTokens: totalOutput,
      inputCostUsd: Number(totalInputCost.toFixed(8)),
      outputCostUsd: Number(totalOutputCost.toFixed(8)),
      totalCostUsd: Number(totalCost.toFixed(8)),
    },
    tagging: {
      provider: taggingSelection?.provider || "cohere",
      model: taggingSelection?.model || null,
      latency_ms: Number(tagLatency.toFixed(2)),
      inputTokens: tagInput,
      outputTokens: tagOutput,
      inputCostUsd: Number(tagInputCost.toFixed(8)),
      outputCostUsd: Number(tagOutputCost.toFixed(8)),
      totalCostUsd: Number(tagTotalCost.toFixed(8)),
      taggingPromptTemplate: taggingPromptTemplate || "",
      taggingContext,
    },
    chunkedMarkdown: chunks.map((text, idx) => ({ chunkIndex: idx, words: countWords(text), text })),
    knowledgeChunks,
    chunkLogs: rows,
    totals: {
      latency_ms: Number((performance.now() - t0).toFixed(2)),
      inputTokens: totalInput + tagInput,
      outputTokens: totalOutput + tagOutput,
      inputCostUsd: Number((totalInputCost + tagInputCost).toFixed(8)),
      outputCostUsd: Number((totalOutputCost + tagOutputCost).toFixed(8)),
      totalCostUsd: Number((totalCost + tagTotalCost).toFixed(8)),
    },
  };
};

export const benchmarkExtractedContent = async ({ file, selection, credentials, promptTemplate }) => {
  const t0 = performance.now();
  const parsed = await parseFileToMarkdown(file, {
    credentials,
    visionProvider: selection?.provider,
    visionModel: selection?.model,
    visionPrompt: promptTemplate,
  });
  const markdown = parsed.markdown || "";
  const words = countWords(markdown);
  const inputTokens = Number(parsed?.usage?.promptTokenCount || parsed?.usage?.input_tokens || 0);
  const outputTokens = Number(parsed?.usage?.candidatesTokenCount || parsed?.usage?.output_tokens || 0);
  const cost = calcCostBreakdown(selection?.model, inputTokens, outputTokens);
  return {
    functionName: "AI_Extracted_Content",
    file: { name: file.originalname, type: file.mimetype, sizeBytes: file.size },
    extracted: {
      parser: parsed.parserType,
      provider: selection?.provider || "local_or_gemini_vision",
      model: parsed?.visionModel || selection?.model || "n/a",
      words,
      characters: markdown.length,
      inputTokens,
      outputTokens,
      inputCostUsd: cost.inputCostUsd,
      outputCostUsd: cost.outputCostUsd,
      totalCostUsd: cost.totalCostUsd,
      usage: parsed.usage || {},
      latency_ms: Number((performance.now() - t0).toFixed(2)),
    },
    totals: {
      inputTokens,
      outputTokens,
      inputCostUsd: cost.inputCostUsd,
      outputCostUsd: cost.outputCostUsd,
      totalCostUsd: cost.totalCostUsd,
      latency_ms: Number((performance.now() - t0).toFixed(2)),
    },
    parsedMarkdown: markdown,
    chunkedMarkdown: chunkByWords(markdown, 500).map((text, idx) => ({ chunkIndex: idx, words: countWords(text), text })),
  };
};

export const benchmarkGenReviewFlow = async ({ input, generationSelection, reviewSelection, credentials }) => {
  const contextChunks = Array.isArray(input?.contextChunks)
    ? input.contextChunks
    : String(input?.contextChunksText || "")
        .split(/\r?\n-{3,}\r?\n/g)
        .map((s) => s.trim())
        .filter(Boolean);
  const contextBlock = contextChunks.length
    ? `\n\nRetrieved context chunks:\n${contextChunks.map((c, i) => `[Chunk ${i + 1}] ${c}`).join("\n\n")}`
    : "";

  const difficulty = input?.difficulty || "Medium";
  const expectedQuestionType = String(input?.questionType || "multiple_choice").toLowerCase() === "question" ? "PE" : "FE";
  const genPrompt = fillTemplate(
    input?.generationPromptTemplate || "Generate {questionType} questions for {domain} with difficulty {difficulty}. Count: {count}. Output JSON array.",
    {
      questionType: input?.questionType || "multiple_choice",
      domain: input?.domain || "Java_OOP",
      difficulty,
      count: input?.count || 3,
      contextChunks: contextBlock,
      groundTruth: input?.groundTruth || "",
    }
  ) + contextBlock;
  const generated = await runChat({ provider: generationSelection.provider, model: generationSelection.model, prompt: genPrompt, credentials, systemPrompt: "You are an exam generator." });
  const generationSchemaValidation = validateQuestionBankOutput(generated.output, expectedQuestionType);

  const reviewPrompt = fillTemplate(
    input?.reviewPromptTemplate ||
      "Review the generated exam content for correctness, difficulty alignment, and schema quality. Return JSON with {review_status,issues,suggestions}. Generated: {generatedOutput}",
    { generatedOutput: generated.output }
  );
  const reviewed = await runChat({ provider: reviewSelection.provider, model: reviewSelection.model, prompt: reviewPrompt, credentials, systemPrompt: "You are a strict academic reviewer." });

  return {
    functionName: "AI_Generation_Review_Flow",
    input: input || {},
    context: {
      chunkCount: contextChunks.length,
      chunks: contextChunks,
      groundTruth: input?.groundTruth || "",
    },
    generation: { provider: generationSelection.provider, model: generationSelection.model, ...generated },
    generationSchemaValidation,
    review: { provider: reviewSelection.provider, model: reviewSelection.model, ...reviewed },
    totals: {
      inputTokens: generated.inputTokens + reviewed.inputTokens,
      outputTokens: generated.outputTokens + reviewed.outputTokens,
      inputCostUsd: Number(((generated.inputCostUsd || 0) + (reviewed.inputCostUsd || 0)).toFixed(8)),
      outputCostUsd: Number(((generated.outputCostUsd || 0) + (reviewed.outputCostUsd || 0)).toFixed(8)),
      totalCostUsd: Number((generated.totalCostUsd + reviewed.totalCostUsd).toFixed(8)),
      latency_ms: Number((generated.latency_ms + reviewed.latency_ms).toFixed(2)),
    },
    rawLog: {
      generatedOutput: generated.output,
      reviewOutput: reviewed.output,
    },
  };
};

export const benchmarkCodeMentor = async ({ input, selection, credentials }) => {
  const prompt = fillTemplate(
    input?.mentorPromptTemplate ||
      "You are AI Code Mentor. Analyze submission and provide verdict/issues/fix guidance and complexity. Problem: {problem} Code: {code}",
    {
      problem: input?.problem || "N/A",
      code: input?.code || "",
      language: input?.language || "java",
    }
  );
  const result = await runChat({ provider: selection.provider, model: selection.model, prompt, credentials, systemPrompt: "You are a careful code mentor for students." });
  return {
    functionName: "AI_Code_Mentor",
    input: { language: input?.language || "java", problem: input?.problem || "", codeLength: (input?.code || "").length },
    model: { provider: selection.provider, model: selection.model },
    ...result,
    rawLog: { mentorOutput: result.output },
  };
};

export const benchmarkChunkingLogic = async ({
  file,
  selection,
  visionSelection,
  visionPromptTemplate,
  taggingSelection,
  taggingPromptTemplate,
  taggingContext = {},
  processingStrategy = "text_only",
  credentials,
}) => {
  const requestedStrategy = String(processingStrategy || "text_only");
  const parsed = await parseFileToMarkdown(file, { credentials });
  const markdown = parsed.markdown || "";
  const rawTextBlocks = splitLogicalBlocks(cleanMarkdownForEmbedding(markdown));
  const { kept: textBlocks, dropped: droppedTextBlocks } = filterLogicalBlocks(rawTextBlocks);
  const images = await extractEmbeddedImagesFromOfficeBuffer(file);
  const supportedImages = images.filter((img) => Boolean(img.mimeType));
  const skippedUnsupportedImages = images
    .filter((img) => !img.mimeType)
    .map((img) => ({ imageIndex: img.imageIndex, name: img.name, path: img.path, reason: "unsupported_image_format" }));
  const imageBlocks = [];
  let visionInput = 0;
  let visionOutput = 0;
  let visionInputCost = 0;
  let visionOutputCost = 0;
  let visionTotalCost = 0;
  let visionLatency = 0;
  const isRawImage = (file?.mimetype || "").startsWith("image/");
  let strategyApplied = requestedStrategy;
  const strategyNotes = [];
  if (requestedStrategy === "full_multimodal_page") {
    if (isRawImage) {
      const vision = await runVisionForImage({
        provider: visionSelection?.provider || "gemini",
        imageBuffer: file.buffer,
        mimeType: file.mimetype || "image/png",
        model: visionSelection?.model || "gemini-3.5-flash",
        credentials,
        prompt:
          visionPromptTemplate ||
          "Bạn là OCR + vision parser. Hãy đọc toàn bộ text và mô tả hình/biểu đồ trong ảnh. Trả markdown có prefix [PAGE 1].",
      });
      visionInput += vision.inputTokens || 0;
      visionOutput += vision.outputTokens || 0;
      visionInputCost += vision.inputCostUsd || 0;
      visionOutputCost += vision.outputCostUsd || 0;
      visionTotalCost += vision.totalCostUsd || 0;
      visionLatency += vision.latency_ms || 0;
      imageBlocks.push({
        blockIndex: 0,
        blockType: "page_vision",
        text: `[PAGE 1]\n${vision.output}`,
        words: countWords(vision.output),
        imageMeta: { imageIndex: 0, name: file.originalname, path: file.originalname },
        vision,
      });
    } else {
      for (const img of supportedImages) {
        const pageNum = Number(img.imageIndex || 0) + 1;
        const vision = await runVisionForImage({
          provider: visionSelection?.provider || "gemini",
          imageBuffer: img.buffer,
          mimeType: img.mimeType,
          model: visionSelection?.model || "gemini-3.5-flash",
          credentials,
          prompt:
            visionPromptTemplate ||
            "Bạn là OCR + vision parser. Hãy đọc toàn bộ text và mô tả hình/biểu đồ trong ảnh. Trả markdown có prefix [PAGE {page}].",
        });
        visionInput += vision.inputTokens || 0;
        visionOutput += vision.outputTokens || 0;
        visionInputCost += vision.inputCostUsd || 0;
        visionOutputCost += vision.outputCostUsd || 0;
        visionTotalCost += vision.totalCostUsd || 0;
        visionLatency += vision.latency_ms || 0;
        imageBlocks.push({
          blockIndex: imageBlocks.length,
          blockType: "page_vision",
          text: `[PAGE ${pageNum}]\n${vision.output}`,
          words: countWords(vision.output),
          imageMeta: {
            imageIndex: img.imageIndex,
            name: img.name,
            path: img.path,
          },
          vision,
        });
      }
      if (!supportedImages.length) {
        strategyNotes.push("No extractable page images detected; fallback to text-extraction blocks.");
        strategyApplied = "text_fallback";
      }
    }
  }

  if (!["text_only", "full_multimodal_page", "text_fallback"].includes(strategyApplied)) {
    strategyApplied = "text_only";
  }
  const rawBlocks = strategyApplied === "text_only" || strategyApplied === "text_fallback" ? [...textBlocks] : [...imageBlocks];
  const { kept: blocks, dropped: droppedBlocks } = filterLogicalBlocks(rawBlocks);
  const rows = [];
  let totalInput = 0;
  let totalOutput = 0;
  let totalInCost = 0;
  let totalOutCost = 0;
  let totalCost = 0;
  let totalLatency = 0;
  let tagInput = 0;
  let tagOutput = 0;
  let tagInputCost = 0;
  let tagOutputCost = 0;
  let tagTotalCost = 0;
  let tagLatency = 0;
  for (const b of blocks) {
    const emb = await runEmbedding({
      provider: selection.provider,
      model: selection.model,
      text: b.text,
      credentials,
    });
    let tagging = null;
    let topic_tags = [];
    if (taggingSelection?.model) {
      const taggingProvider = taggingSelection?.provider || "cohere";
      const tagPrompt = fillTemplate(
        taggingPromptTemplate ||
          "Bạn là AI gắn nhãn topic cho chunk kiến thức lập trình. Dựa vào taxonomy: {allowedTags}. Môn học: {subject}. Ngôn ngữ: {language}. Trả về JSON duy nhất dạng {\"topic_tags\":[\"tag1\",\"tag2\"],\"confidence\":0.0-1.0}. Chunk: {chunk}",
        {
          allowedTags: (taggingContext?.allowedTags || []).join(", "),
          subject: taggingContext?.subject || "general_programming",
          language: taggingContext?.language || "general",
          chunk: b.text.slice(0, 5000),
        }
      );
      const tr = await runChat({
        provider: taggingProvider,
        model: taggingSelection.model,
        prompt: tagPrompt,
        credentials,
        systemPrompt: "You are a strict classifier. Return JSON only.",
      });
      const parsedTag = safeJsonParse(tr.output);
      topic_tags = normalizeTags(parsedTag, taggingContext?.allowedTags || []);
      tagInput += tr.inputTokens || 0;
      tagOutput += tr.outputTokens || 0;
      tagInputCost += tr.inputCostUsd || 0;
      tagOutputCost += tr.outputCostUsd || 0;
      tagTotalCost += tr.totalCostUsd || 0;
      tagLatency += tr.latency_ms || 0;
      tagging = {
        provider: taggingProvider,
        model: taggingSelection.model,
        inputTokens: tr.inputTokens,
        outputTokens: tr.outputTokens,
        inputCostUsd: tr.inputCostUsd,
        outputCostUsd: tr.outputCostUsd,
        totalCostUsd: tr.totalCostUsd,
        latency_ms: tr.latency_ms,
        topic_tags,
      };
    }
    totalInput += emb.inputTokens || 0;
    totalOutput += emb.outputTokens || 0;
    totalInCost += emb.inputCostUsd || 0;
    totalOutCost += emb.outputCostUsd || 0;
    totalCost += emb.totalCostUsd || 0;
    totalLatency += emb.latency_ms || 0;
    rows.push({
      ...b,
      embedding: {
        inputTokens: emb.inputTokens,
        outputTokens: emb.outputTokens,
        inputCostUsd: emb.inputCostUsd,
        outputCostUsd: emb.outputCostUsd,
        totalCostUsd: emb.totalCostUsd,
        embeddingDimensions: emb.embeddingDimensions,
      },
      topic_tags,
      tagging,
    });
  }
  return {
    functionName: "AI_Chunking_Logic_Embedding",
    file: { name: file.originalname, type: file.mimetype, sizeBytes: file.size },
    parse: { parser: parsed.parserType, words: countWords(markdown), characters: markdown.length },
    processingStrategy: {
      requested: requestedStrategy,
      applied: strategyApplied,
      notes: strategyNotes,
    },
    logicChunking: {
      textBlockCount: textBlocks.length,
      imageBlockCount: imageBlocks.length,
      blockCount: blocks.length,
      droppedBlockCount: droppedBlocks.length,
      droppedBlocks: droppedBlocks.slice(0, 200),
      droppedTextBlocks: droppedTextBlocks.slice(0, 200),
      blocks: rows,
    },
    visionExtraction: {
      provider: visionSelection?.provider || "gemini",
      model: visionSelection?.model || "gemini-3.5-flash",
      visionPromptTemplate: visionPromptTemplate || "",
      embeddedImagesDetected: images.length,
      supportedImagesProcessed: supportedImages.length,
      skippedUnsupportedImages,
      inputTokens: visionInput,
      outputTokens: visionOutput,
      inputCostUsd: Number(visionInputCost.toFixed(8)),
      outputCostUsd: Number(visionOutputCost.toFixed(8)),
      totalCostUsd: Number(visionTotalCost.toFixed(8)),
      latency_ms: Number(visionLatency.toFixed(2)),
    },
    tagging: {
      provider: taggingSelection?.provider || "cohere",
      model: taggingSelection?.model || null,
      inputTokens: tagInput,
      outputTokens: tagOutput,
      inputCostUsd: Number(tagInputCost.toFixed(8)),
      outputCostUsd: Number(tagOutputCost.toFixed(8)),
      totalCostUsd: Number(tagTotalCost.toFixed(8)),
      latency_ms: Number(tagLatency.toFixed(2)),
      taggingPromptTemplate: taggingPromptTemplate || "",
      taggingContext,
    },
    totals: {
      inputTokens: totalInput + visionInput + tagInput,
      outputTokens: totalOutput + visionOutput + tagOutput,
      inputCostUsd: Number((totalInCost + visionInputCost + tagInputCost).toFixed(8)),
      outputCostUsd: Number((totalOutCost + visionOutputCost + tagOutputCost).toFixed(8)),
      totalCostUsd: Number((totalCost + visionTotalCost + tagTotalCost).toFixed(8)),
      latency_ms: Number((totalLatency + visionLatency + tagLatency).toFixed(2)),
    },
    parsedMarkdown: markdown,
    cleanedMarkdown: cleanMarkdownForEmbedding(markdown),
  };
};

export const benchmarkFullFlow = async ({
  file,
  credentials,
  gatekeeperSelection,
  embeddingSelection,
  taggingSelection,
  generationSelection,
  reviewSelection,
  mentorSelection,
  prompts = {},
  generationInput = {},
  mentorInput = {},
}) => {
  const gatekeeper = await benchmarkGatekeeper({
    file,
    selection: gatekeeperSelection,
    credentials,
    promptTemplate: prompts.gatekeeper,
  });

  const embedding = await benchmarkEmbedding({
    file,
    selection: embeddingSelection,
    credentials,
    taggingSelection,
    taggingPromptTemplate: prompts.embedding_tagging,
    taggingContext: generationInput?.taggingContext || {},
  });

  const contextChunks = (embedding.knowledgeChunks || [])
    .slice(0, 20)
    .map((c) => `[topic_tags: ${(c.topic_tags || []).join(", ")}]\n${c.content_text}`);

  const genReview = await benchmarkGenReviewFlow({
    input: {
      ...(generationInput || {}),
      contextChunks,
      generationPromptTemplate: prompts.generation,
      reviewPromptTemplate: prompts.review,
    },
    generationSelection,
    reviewSelection,
    credentials,
  });

  const mentor = await benchmarkCodeMentor({
    input: {
      ...(mentorInput || {}),
      mentorPromptTemplate: prompts.code_mentor,
    },
    selection: mentorSelection,
    credentials,
  });

  const totals = {
    inputTokens:
      (gatekeeper?.ai?.inputTokens || 0) +
      (embedding?.totals?.inputTokens || 0) +
      (genReview?.totals?.inputTokens || 0) +
      (mentor?.inputTokens || 0),
    outputTokens:
      (gatekeeper?.ai?.outputTokens || 0) +
      (embedding?.totals?.outputTokens || 0) +
      (genReview?.totals?.outputTokens || 0) +
      (mentor?.outputTokens || 0),
    inputCostUsd:
      (gatekeeper?.ai?.inputCostUsd || 0) +
      (embedding?.totals?.inputCostUsd || 0) +
      (genReview?.totals?.inputCostUsd || 0) +
      (mentor?.inputCostUsd || 0),
    outputCostUsd:
      (gatekeeper?.ai?.outputCostUsd || 0) +
      (embedding?.totals?.outputCostUsd || 0) +
      (genReview?.totals?.outputCostUsd || 0) +
      (mentor?.outputCostUsd || 0),
  };
  totals.totalCostUsd = Number((totals.inputCostUsd + totals.outputCostUsd).toFixed(8));

  return {
    functionName: "AI_Full_Flow",
    file: { name: file.originalname, type: file.mimetype, sizeBytes: file.size },
    steps: { gatekeeper, embedding, genReview, mentor },
    totals,
  };
};

export const simulateMonthlyCost = ({
  students = 500,
  assumptions = {},
  modelPlan = {},
}) => {
  const reqPerStudent = {
    feGeneration: Number(assumptions.feGenerationPerStudent ?? 30),
    peGeneration: Number(assumptions.peGenerationPerStudent ?? 12),
    byosIngest: Number(assumptions.byosPerStudent ?? 2),
    mentor: Number(assumptions.mentorPerStudent ?? 25),
  };
  const totalRequests = {
    feGeneration: Math.round(students * reqPerStudent.feGeneration),
    peGeneration: Math.round(students * reqPerStudent.peGeneration),
    byosIngest: Math.round(students * reqPerStudent.byosIngest),
    mentor: Math.round(students * reqPerStudent.mentor),
  };

  const tokens = {
    gatekeeperIn: Number(assumptions.gatekeeperInTokens ?? 1400),
    gatekeeperOut: Number(assumptions.gatekeeperOutTokens ?? 120),
    generationIn: Number(assumptions.generationInTokens ?? 2200),
    generationOut: Number(assumptions.generationOutTokens ?? 900),
    reviewIn: Number(assumptions.reviewInTokens ?? 1200),
    reviewOut: Number(assumptions.reviewOutTokens ?? 300),
    mentorIn: Number(assumptions.mentorInTokens ?? 1200),
    mentorOut: Number(assumptions.mentorOutTokens ?? 500),
    embeddingIn: Number(assumptions.embeddingInTokensPerByos ?? 22000),
    taggingIn: Number(assumptions.taggingInTokensPerByos ?? 4200),
    taggingOut: Number(assumptions.taggingOutTokensPerByos ?? 700),
  };

  const models = {
    gatekeeper: modelPlan.gatekeeper || "gemini-3.1-flash-lite",
    generation: modelPlan.generation || "gemini-3.5-flash",
    review: modelPlan.review || "gemini-3.5-flash",
    mentor: modelPlan.mentor || "gpt-4o-mini",
    embedding: modelPlan.embedding || "embed-multilingual-v3.0",
    tagging: modelPlan.tagging || "gemini-3.1-flash-lite",
  };

  const cost = {
    gatekeeper: calcCostBreakdown(
      models.gatekeeper,
      totalRequests.byosIngest * tokens.gatekeeperIn,
      totalRequests.byosIngest * tokens.gatekeeperOut
    ),
    generation: calcCostBreakdown(
      models.generation,
      (totalRequests.feGeneration + totalRequests.peGeneration) * tokens.generationIn,
      (totalRequests.feGeneration + totalRequests.peGeneration) * tokens.generationOut
    ),
    review: calcCostBreakdown(
      models.review,
      (totalRequests.feGeneration + totalRequests.peGeneration) * tokens.reviewIn,
      (totalRequests.feGeneration + totalRequests.peGeneration) * tokens.reviewOut
    ),
    mentor: calcCostBreakdown(
      models.mentor,
      totalRequests.mentor * tokens.mentorIn,
      totalRequests.mentor * tokens.mentorOut
    ),
    embedding: calcCostBreakdown(
      models.embedding,
      totalRequests.byosIngest * tokens.embeddingIn,
      0
    ),
    tagging: calcCostBreakdown(
      models.tagging,
      totalRequests.byosIngest * tokens.taggingIn,
      totalRequests.byosIngest * tokens.taggingOut
    ),
  };

  const total = Object.values(cost).reduce(
    (acc, c) => {
      acc.inputCostUsd += c.inputCostUsd;
      acc.outputCostUsd += c.outputCostUsd;
      acc.totalCostUsd += c.totalCostUsd;
      return acc;
    },
    { inputCostUsd: 0, outputCostUsd: 0, totalCostUsd: 0 }
  );

  return {
    students,
    assumptions: { reqPerStudent, tokens },
    requestEstimation: totalRequests,
    modelPlan: models,
    costBreakdown: cost,
    totals: {
      inputCostUsd: Number(total.inputCostUsd.toFixed(8)),
      outputCostUsd: Number(total.outputCostUsd.toFixed(8)),
      totalCostUsd: Number(total.totalCostUsd.toFixed(8)),
    },
  };
};

export const modelOptions = {
  providers: {
    openai: ["gpt-4o-mini", "gpt-4o", "gpt-5.1", "text-embedding-3-small"],
    gemini: ["gemini-3.1-flash-lite", "gemini-3.1-flash", "gemini-3.5-flash"],
    cohere: [
      "embed-multilingual-v3.0",
      "embed-english-v3.0",
      "command-r7b-12-2024",
      "command-r-08-2024",
      "command-r-plus-08-2024",
      "command-a-03-2025",
      "command-a-plus-05-2026",
      "command-a-reasoning-08-2025",
      "command-a-translate-08-2025",
      "command-a-vision-07-2025",
    ],
  },
};

export const listProviderModels = async ({ credentials = {} }) => {
  const out = {
    openai: { status: "skipped", models: [] },
    gemini: { status: "skipped", models: [] },
    cohere: { status: "skipped", models: [] },
  };

  try {
    if (credentials.openaiApiKey || config.openaiApiKey) {
      const openai = buildOpenAI(credentials);
      const r = await openai.models.list();
      out.openai = { status: "ok", models: (r?.data || []).map((m) => m.id).sort() };
    }
  } catch (e) {
    out.openai = { status: "failed", models: [], error: e.message };
  }

  try {
    const geminiKey = credentials.geminiApiKey || config.geminiApiKey;
    if (geminiKey) {
      const resp = await fetch(`https://generativelanguage.googleapis.com/v1beta/models?key=${encodeURIComponent(geminiKey)}`);
      const text = await resp.text();
      const json = text ? JSON.parse(text) : {};
      if (!resp.ok) throw new Error(json?.error?.message || `Gemini list models failed ${resp.status}`);
      const models = (json?.models || [])
        .filter((m) => (m.supportedGenerationMethods || []).includes("generateContent"))
        .map((m) => (m.name || "").replace("models/", ""))
        .filter(Boolean);
      out.gemini = { status: "ok", models: models.sort() };
    }
  } catch (e) {
    out.gemini = { status: "failed", models: [], error: e.message };
  }

  try {
    const cohereKey = credentials.cohereApiKey || config.cohereApiKey;
    if (cohereKey) {
      const base = credentials.cohereBaseUrl || config.cohereBaseUrl;
      let resp = await fetch(`${base}/v2/models`, { headers: { Authorization: `Bearer ${cohereKey}` } });
      if (!resp.ok) {
        resp = await fetch(`${base}/v1/models`, { headers: { Authorization: `Bearer ${cohereKey}` } });
      }
      const text = await resp.text();
      const json = text ? JSON.parse(text) : {};
      if (!resp.ok) throw new Error(json?.message || `Cohere list models failed ${resp.status}`);
      const items = json?.models || json?.data || [];
      const models = items.map((m) => m?.name || m?.id).filter(Boolean);
      out.cohere = { status: "ok", models: models.sort() };
    }
  } catch (e) {
    out.cohere = { status: "failed", models: [], error: e.message };
  }

  return { runAt: new Date().toISOString(), providers: out };
};
