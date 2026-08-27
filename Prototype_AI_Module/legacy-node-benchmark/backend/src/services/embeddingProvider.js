import { openai } from "./clients.js";
import { config } from "../config.js";

const toNumber = (value) => (Number.isFinite(Number(value)) ? Number(value) : 0);

const cohereEmbed = async (text) => {
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
      texts: [text],
    }),
  });

  const raw = await response.text();
  let json = {};
  try {
    json = raw ? JSON.parse(raw) : {};
  } catch {
    throw new Error(`Cohere embed non-JSON response (${response.status}): ${raw.slice(0, 160)}`);
  }

  if (!response.ok) {
    throw new Error(json?.message || `Cohere embed failed with status ${response.status}`);
  }

  const vector = json?.embeddings?.float?.[0] || json?.embeddings?.[0] || [];
  const inputTokens = toNumber(json?.meta?.billed_units?.input_tokens ?? json?.meta?.tokens?.input_tokens);
  const costUsd = Number(((inputTokens / 1_000_000) * config.cohereInputCostPer1M).toFixed(8));

  return {
    provider: "cohere",
    model: config.cohereEmbeddingModel,
    inputTokens,
    costUsd,
    embeddingDimensions: Array.isArray(vector) ? vector.length : 0,
  };
};

const openaiEmbed = async (text) => {
  const response = await openai.embeddings.create({
    model: config.openaiEmbeddingModel,
    input: text,
  });

  const inputTokens = toNumber(response.usage?.prompt_tokens ?? response.usage?.input_tokens);
  const costUsd = Number(((inputTokens / 1_000_000) * 0.02).toFixed(8));

  return {
    provider: "openai",
    model: config.openaiEmbeddingModel,
    inputTokens,
    costUsd,
    embeddingDimensions: response.data?.[0]?.embedding?.length || 0,
  };
};

export const embedText = async (text) => {
  if (config.embeddingProvider === "cohere") {
    if (!config.cohereApiKey) {
      throw new Error("COHERE_API_KEY is missing while EMBEDDING_PROVIDER=cohere");
    }
    return cohereEmbed(text);
  }

  return openaiEmbed(text);
};
