import OpenAI from "openai";
import { GoogleGenerativeAI } from "@google/generative-ai";
import { performance } from "node:perf_hooks";
import { config } from "../config.js";
import { mockPayloads } from "../mockPayloads.js";

const MODEL_PRICING = {
  "gpt-4o-mini": { input: 0.15, output: 0.6 },
  "gemini-3.1-flash": { input: 0.075, output: 0.3 },
  "gemini-3.1-flash-lite": { input: 0.075, output: 0.3 },
  "gemini-3.5-flash": { input: 0.075, output: 0.3 },
  "text-embedding-3-small": { input: 0.02, output: 0 },
};

const getCost = (model, inputTokens, outputTokens) => {
  const price = MODEL_PRICING[model];
  if (!price) return 0;
  return Number((((inputTokens / 1_000_000) * price.input) + ((outputTokens / 1_000_000) * price.output)).toFixed(8));
};

const buildOpenAI = (credentials = {}) => {
  const apiKey = credentials.openaiApiKey || config.openaiApiKey;
  const baseURL = credentials.openaiBaseUrl || config.openaiBaseUrl;
  const client = new OpenAI({ apiKey, ...(baseURL ? { baseURL } : {}) });
  return client;
};

const buildGemini = (credentials = {}, model) => {
  const apiKey = credentials.geminiApiKey || config.geminiApiKey;
  const genAI = new GoogleGenerativeAI(apiKey || "");
  return genAI.getGenerativeModel({ model });
};

const runOpenAIChat = async ({ openai, model, prompt, system = "You are a precise assistant." }) => {
  const resp = await openai.chat.completions.create({
    model,
    messages: [
      { role: "system", content: system },
      { role: "user", content: prompt },
    ],
    temperature: 0.2,
  });

  return {
    inputTokens: Number(resp.usage?.prompt_tokens || 0),
    outputTokens: Number(resp.usage?.completion_tokens || 0),
    rawOutput: resp.choices?.[0]?.message?.content || "",
  };
};

const runGeminiChat = async ({ model, credentials, prompt }) => {
  const m = buildGemini(credentials, model);
  const result = await m.generateContent(prompt);
  const response = result.response;
  return {
    inputTokens: Number(response?.usageMetadata?.promptTokenCount || 0),
    outputTokens: Number(response?.usageMetadata?.candidatesTokenCount || 0),
    rawOutput: response?.text?.() || "",
  };
};

const runOpenAIEmbedding = async ({ openai, model, input }) => {
  const resp = await openai.embeddings.create({ model, input });
  return {
    inputTokens: Number(resp.usage?.prompt_tokens ?? resp.usage?.input_tokens ?? 0),
    outputTokens: 0,
    rawOutput: { dimensions: resp.data?.[0]?.embedding?.length || 0 },
  };
};

const runCohereEmbedding = async ({ credentials, model, input }) => {
  const apiKey = credentials.cohereApiKey || config.cohereApiKey;
  const baseUrl = credentials.cohereBaseUrl || config.cohereBaseUrl;
  const response = await fetch(`${baseUrl}/v2/embed`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      input_type: "search_document",
      embedding_types: ["float"],
      texts: [input],
    }),
  });

  const text = await response.text();
  const json = text ? JSON.parse(text) : {};
  if (!response.ok) {
    throw new Error(json?.message || `Cohere embedding failed ${response.status}`);
  }

  const tokens = Number(json?.meta?.billed_units?.input_tokens || 0);
  return {
    inputTokens: tokens,
    outputTokens: 0,
    rawOutput: { dimensions: json?.embeddings?.float?.[0]?.length || 0 },
    overrideCost: Number(((tokens / 1_000_000) * (config.cohereInputCostPer1M || 0)).toFixed(8)),
  };
};

const timeTask = async (fn) => {
  const t0 = performance.now();
  const out = await fn();
  const t1 = performance.now();
  return { ...out, latency_ms: Number((t1 - t0).toFixed(2)) };
};

const taskPrompt = {
  gatekeeper: `Classify if this content is within supported scope (C intro, Java OOP, DSA OOP). Return JSON with {is_supported, primary_domain, reason}.\n\n${mockPayloads.AI_Gatekeeper.prompt}`,
  generation: mockPayloads.AI_Question_Gen.prompt,
  review: mockPayloads.AI_Review_Agent.prompt,
  mentor: mockPayloads.AI_Code_Mentor.prompt,
  embedding: mockPayloads.AI_Embedding.inputText,
};

export const runFunctionBenchmarks = async ({ selections = {}, credentials = {} }) => {
  const openai = buildOpenAI(credentials);
  const normalized = {
    gatekeeper: selections.gatekeeper || { provider: "gemini", model: "gemini-3.1-flash-lite" },
    embedding: selections.embedding || { provider: "cohere", model: "embed-multilingual-v3.0" },
    generation: selections.generation || { provider: "gemini", model: "gemini-3.5-flash" },
    review: selections.review || { provider: "gemini", model: "gemini-3.5-flash" },
    mentor: selections.mentor || { provider: "openai", model: "gpt-4o-mini" },
  };

  const runOne = async (taskName, selection, inputPrompt, kind = "chat") => {
    try {
      let result;
      if (kind === "embedding") {
        result = await timeTask(async () => {
          if (selection.provider === "cohere") {
            return runCohereEmbedding({ credentials, model: selection.model, input: inputPrompt });
          }
          return runOpenAIEmbedding({ openai, model: selection.model, input: inputPrompt });
        });
      } else {
        result = await timeTask(async () => {
          if (selection.provider === "gemini") {
            return runGeminiChat({ model: selection.model, credentials, prompt: inputPrompt });
          }
          return runOpenAIChat({ openai, model: selection.model, prompt: inputPrompt });
        });
      }

      const totalCostUsd = result.overrideCost ?? getCost(selection.model, result.inputTokens, result.outputTokens);
      return {
        taskName,
        provider: selection.provider,
        model: selection.model,
        latency_ms: result.latency_ms,
        inputTokens: result.inputTokens,
        outputTokens: result.outputTokens,
        totalCostUsd,
        status: "success",
        rawOutputPreview: typeof result.rawOutput === "string" ? result.rawOutput.slice(0, 500) : result.rawOutput,
      };
    } catch (error) {
      return {
        taskName,
        provider: selection.provider,
        model: selection.model,
        latency_ms: 0,
        inputTokens: 0,
        outputTokens: 0,
        totalCostUsd: 0,
        status: "failed",
        error: error?.message || "Unknown error",
      };
    }
  };

  const outputs = [];
  outputs.push(await runOne("AI_Gatekeeper", normalized.gatekeeper, taskPrompt.gatekeeper));
  outputs.push(await runOne("AI_Embedding", normalized.embedding, taskPrompt.embedding, "embedding"));
  outputs.push(await runOne("AI_MultiAgent_Generation", normalized.generation, taskPrompt.generation));
  outputs.push(await runOne("AI_MultiAgent_Review", normalized.review, taskPrompt.review));
  outputs.push(await runOne("AI_Code_Mentor", normalized.mentor, taskPrompt.mentor));

  const totalCostUsd = Number(outputs.reduce((sum, item) => sum + (item.totalCostUsd || 0), 0).toFixed(8));
  const totalLatencyMs = Number(outputs.reduce((sum, item) => sum + (item.latency_ms || 0), 0).toFixed(2));

  return {
    runAt: new Date().toISOString(),
    mode: "benchmark_ai_functions",
    selections: normalized,
    credentialsMeta: {
      openaiBaseUrl: credentials.openaiBaseUrl || config.openaiBaseUrl || "https://api.openai.com/v1",
      hasOpenAIKey: Boolean(credentials.openaiApiKey || config.openaiApiKey),
      hasGeminiKey: Boolean(credentials.geminiApiKey || config.geminiApiKey),
      hasCohereKey: Boolean(credentials.cohereApiKey || config.cohereApiKey),
    },
    results: outputs,
    totals: {
      totalCostUsd,
      totalLatencyMs,
      totalInputTokens: outputs.reduce((s, i) => s + (i.inputTokens || 0), 0),
      totalOutputTokens: outputs.reduce((s, i) => s + (i.outputTokens || 0), 0),
    },
  };
};
