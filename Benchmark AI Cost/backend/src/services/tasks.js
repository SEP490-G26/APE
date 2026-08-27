import { openai, geminiModel, geminiModelName } from "./clients.js";
import { mockPayloads } from "../mockPayloads.js";
import { runBenchmarkTask } from "./benchmarkRunner.js";
import { config } from "../config.js";
import { embedText } from "./embeddingProvider.js";

const runGeminiTextTask = async (prompt) => {
  const result = await geminiModel.generateContent(prompt);
  const response = result.response;

  return {
    usage: response.usageMetadata || {},
    raw: response.text?.() || "",
  };
};

const runGeminiVisionTask = async ({ prompt, base64Image }) => {
  const result = await geminiModel.generateContent([
    { text: prompt },
    {
      inlineData: {
        mimeType: "image/png",
        data: base64Image,
      },
    },
  ]);

  const response = result.response;

  return {
    usage: response.usageMetadata || {},
    raw: response.text?.() || "",
  };
};

const runEmbeddingTask = async (inputText) => {
  const response = await embedText(inputText);

  return {
    usage: {
      input_tokens: response.inputTokens,
      output_tokens: 0,
    },
    raw: { embeddingDimensions: response.embeddingDimensions, provider: response.provider },
    model: response.model,
  };
};

const runOpenAIChatTask = async (prompt) => {
  const response = await openai.chat.completions.create({
    model: config.openaiChatModel,
    messages: [
      { role: "system", content: "You are a precise academic assistant." },
      { role: "user", content: prompt },
    ],
    temperature: 0.2,
  });

  return {
    usage: response.usage || {},
    raw: response.choices?.[0]?.message?.content || "",
  };
};

export const runAllBenchmarks = async () => {
  const jobs = [
    runBenchmarkTask({
      taskName: "AI_Gatekeeper",
      model: geminiModelName,
      executor: () => runGeminiTextTask(mockPayloads.AI_Gatekeeper.prompt),
    }),
    runBenchmarkTask({
      taskName: "AI_Vision_Parse",
      model: geminiModelName,
      executor: () => runGeminiVisionTask(mockPayloads.AI_Vision_Parse),
    }),
    runBenchmarkTask({
      taskName: "AI_Embedding",
      model:
        config.embeddingProvider === "cohere"
          ? config.cohereEmbeddingModel
          : config.openaiEmbeddingModel,
      executor: () => runEmbeddingTask(mockPayloads.AI_Embedding.inputText),
    }),
    runBenchmarkTask({
      taskName: "AI_Question_Gen",
      model: geminiModelName,
      executor: () => runGeminiTextTask(mockPayloads.AI_Question_Gen.prompt),
    }),
    runBenchmarkTask({
      taskName: "AI_Review_Agent",
      model: config.openaiChatModel,
      executor: () => runOpenAIChatTask(mockPayloads.AI_Review_Agent.prompt),
    }),
    runBenchmarkTask({
      taskName: "AI_Code_Mentor",
      model: config.openaiChatModel,
      executor: () => runOpenAIChatTask(mockPayloads.AI_Code_Mentor.prompt),
    }),
    runBenchmarkTask({
      taskName: "AI_Summary_Analyzer",
      model: geminiModelName,
      executor: () => runGeminiTextTask(mockPayloads.AI_Summary_Analyzer.prompt),
    }),
  ];

  const results = await Promise.all(jobs);

  const totalCostUsd = Number(
    results.reduce((sum, item) => sum + (item.totalCostUsd || 0), 0).toFixed(8)
  );

  return {
    ranAt: new Date().toISOString(),
    totalTasks: results.length,
    totalCostUsd,
    results,
  };
};
