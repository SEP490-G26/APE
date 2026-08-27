import dotenv from "dotenv";

dotenv.config();

if (!process.env.GEMINI_API_KEY) {
  console.warn("[WARN] Missing environment variable: GEMINI_API_KEY");
}

if (!process.env.OPENAI_API_KEY && !process.env.COHERE_API_KEY) {
  console.warn("[WARN] Missing OPENAI_API_KEY and COHERE_API_KEY for embedding operations");
}

const embeddingProvider = process.env.EMBEDDING_PROVIDER || (process.env.COHERE_API_KEY ? "cohere" : "openai");

export const config = {
  port: Number(process.env.PORT || 3001),
  openaiApiKey: process.env.OPENAI_API_KEY,
  openaiBaseUrl: process.env.OPENAI_BASE_URL,
  openaiChatModel: process.env.OPENAI_CHAT_MODEL || "gpt-4o-mini",
  openaiEmbeddingModel: process.env.OPENAI_EMBEDDING_MODEL || "text-embedding-3-small",
  cohereApiKey: process.env.COHERE_API_KEY,
  cohereBaseUrl: process.env.COHERE_BASE_URL || "https://api.cohere.com",
  cohereEmbeddingModel: process.env.COHERE_EMBEDDING_MODEL || "embed-multilingual-v3.0",
  cohereInputCostPer1M: Number(process.env.COHERE_EMBED_INPUT_PER_1M || 0),
  embeddingProvider,
  geminiApiKey: process.env.GEMINI_API_KEY,
  geminiModel: process.env.GEMINI_MODEL || "gemini-3.5-flash",
};
