import { performance } from "node:perf_hooks";
import { parseFileToMarkdown } from "./fileParser.js";
import { EMBEDDING_CHUNK_WORDS } from "../constants/upload.js";
import { embedText } from "./embeddingProvider.js";
import { config } from "../config.js";

const countWords = (text = "") => {
  const trimmed = text.trim();
  if (!trimmed) return 0;
  return trimmed.split(/\s+/).length;
};

const chunkByWords = (text = "", chunkWords = EMBEDDING_CHUNK_WORDS) => {
  const words = text.trim().split(/\s+/).filter(Boolean);
  if (!words.length) return [];

  const chunks = [];
  for (let i = 0; i < words.length; i += chunkWords) {
    chunks.push(words.slice(i, i + chunkWords).join(" "));
  }
  return chunks;
};

const toNumber = (value) => (Number.isFinite(Number(value)) ? Number(value) : 0);

export const runUploadBenchmark = async (file) => {
  const startedAtIso = new Date().toISOString();
  const runStarted = performance.now();
  const timeline = [];
  timeline.push({ ts: startedAtIso, phase: "upload_received", details: { fileName: file.originalname } });

  const parseStarted = performance.now();
  const parseResult = await parseFileToMarkdown(file);
  const parseLatency = Number((performance.now() - parseStarted).toFixed(2));
  timeline.push({
    ts: new Date().toISOString(),
    phase: "parse_completed",
    details: { parser: parseResult.parserType, latency_ms: parseLatency },
  });

  const markdown = parseResult.markdown || "";
  const totalWords = countWords(markdown);
  const chunks = chunkByWords(markdown);
  const chunkedMarkdown = chunks.map((text, idx) => ({
    chunkIndex: idx,
    words: countWords(text),
    text,
  }));

  const parseInputTokens = toNumber(parseResult.usage?.promptTokenCount);
  const parseOutputTokens = toNumber(parseResult.usage?.candidatesTokenCount);
  const parseCostUsd = Number(
    ((parseInputTokens / 1_000_000) * 0.075 + (parseOutputTokens / 1_000_000) * 0.3).toFixed(8)
  );

  const embeddingStarted = performance.now();
  let embeddingInputTokens = 0;
  let embeddingCostUsd = 0;
  let embeddingStatus = "success";
  let embeddingNote = "";
  let embeddingModelUsed = "";
  const perChunk = [];

  for (let i = 0; i < chunks.length; i += 1) {
    try {
      const chunk = chunks[i];
      const response = await embedText(chunk);

      embeddingInputTokens += response.inputTokens;
      embeddingCostUsd += response.costUsd;
      embeddingModelUsed = response.model;

      perChunk.push({
        chunkIndex: i,
        words: countWords(chunk),
        inputTokens: response.inputTokens,
        costUsd: response.costUsd,
        embeddingDimensions: response.embeddingDimensions,
        model: response.model,
        provider: response.provider,
        tags: ["learning-content", "chunked-500w"],
      });
      timeline.push({
        ts: new Date().toISOString(),
        phase: "embedding_chunk_completed",
        details: {
          chunkIndex: i,
          words: countWords(chunk),
          provider: response.provider,
          model: response.model,
          inputTokens: response.inputTokens,
          costUsd: response.costUsd,
        },
      });
    } catch (error) {
      embeddingStatus = "skipped";
      embeddingNote =
        error?.message ||
        "Embedding model is unavailable on the current endpoint. Step skipped for this run.";
      break;
    }
  }

  const embeddingLatency = Number((performance.now() - embeddingStarted).toFixed(2));
  const totalLatency = Number((performance.now() - runStarted).toFixed(2));
  const totalCostUsd = Number((parseCostUsd + embeddingCostUsd).toFixed(8));

  return {
    runAt: new Date().toISOString(),
    timeline,
    parsedMarkdown: markdown,
    chunkedMarkdown,
    file: {
      name: file.originalname,
      mimeType: file.mimetype,
      sizeBytes: file.size,
      sizeMb: Number((file.size / (1024 * 1024)).toFixed(4)),
    },
    documentStats: {
      characters: markdown.length,
      words: totalWords,
      chunks: chunks.length,
      chunkSizeWords: EMBEDDING_CHUNK_WORDS,
    },
    steps: [
      {
        step: "parse_to_markdown",
        parser: parseResult.parserType,
        model: parseResult.parserType === "gemini_vision" ? "gemini-vision" : "local-parser",
        latency_ms: parseLatency,
        inputTokens: parseInputTokens,
        outputTokens: parseOutputTokens,
        totalCostUsd: parseCostUsd,
        status: "success",
      },
      {
        step: "embedding_and_tagging",
        model:
          embeddingModelUsed ||
          (config.embeddingProvider === "cohere"
            ? config.cohereEmbeddingModel
            : config.openaiEmbeddingModel),
        latency_ms: embeddingLatency,
        inputTokens: embeddingInputTokens,
        outputTokens: 0,
        totalCostUsd: Number(embeddingCostUsd.toFixed(8)),
        status: embeddingStatus,
        note: embeddingNote,
      },
    ],
    embeddingChunks: perChunk,
    rawFlowLog: {
      provider: config.embeddingProvider,
      parse: {
        parserType: parseResult.parserType,
        usage: parseResult.usage || {},
      },
      embedding: {
        status: embeddingStatus,
        chunksAttempted: perChunk.length,
      },
    },
    totals: {
      latency_ms: totalLatency,
      inputTokens: parseInputTokens + embeddingInputTokens,
      outputTokens: parseOutputTokens,
      totalCostUsd,
    },
  };
};
