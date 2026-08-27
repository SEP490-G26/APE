import { performance } from "node:perf_hooks";
import { PRICING_PER_1M } from "../constants/pricing.js";

const toNumber = (value) => (Number.isFinite(Number(value)) ? Number(value) : 0);

const extractUsage = (rawUsage = {}) => {
  const inputTokens = toNumber(
    rawUsage.prompt_tokens ?? rawUsage.input_tokens ?? rawUsage.promptTokenCount
  );

  const outputTokens = toNumber(
    rawUsage.completion_tokens ?? rawUsage.output_tokens ?? rawUsage.candidatesTokenCount
  );

  return { inputTokens, outputTokens };
};

const calculateCostUsd = ({ model, inputTokens, outputTokens }) => {
  const price = PRICING_PER_1M[model];
  if (!price) return 0;

  const inputCost = (inputTokens / 1_000_000) * price.input;
  const outputCost = (outputTokens / 1_000_000) * price.output;

  return Number((inputCost + outputCost).toFixed(8));
};

export const runBenchmarkTask = async ({ taskName, model, executor }) => {
  const startedAt = performance.now();

  try {
    const { usage = {}, raw } = await executor();
    const endedAt = performance.now();
    const latencyMs = Number((endedAt - startedAt).toFixed(2));

    const { inputTokens, outputTokens } = extractUsage(usage);
    const totalCostUsd = calculateCostUsd({ model, inputTokens, outputTokens });

    return {
      taskName,
      model,
      latency_ms: latencyMs,
      inputTokens,
      outputTokens,
      totalCostUsd,
      status: "success",
      raw,
    };
  } catch (error) {
    const endedAt = performance.now();
    return {
      taskName,
      model,
      latency_ms: Number((endedAt - startedAt).toFixed(2)),
      inputTokens: 0,
      outputTokens: 0,
      totalCostUsd: 0,
      status: "failed",
      error: error?.message || "Unknown error",
    };
  }
};
