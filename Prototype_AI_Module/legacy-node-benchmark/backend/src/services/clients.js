import OpenAI from "openai";
import { GoogleGenerativeAI } from "@google/generative-ai";
import { config } from "../config.js";

const openaiClientConfig = {
  apiKey: config.openaiApiKey,
};

if (config.openaiBaseUrl) {
  openaiClientConfig.baseURL = config.openaiBaseUrl;
}

export const openai = new OpenAI(openaiClientConfig);

const genAI = new GoogleGenerativeAI(config.geminiApiKey || "");

export const geminiModelName = config.geminiModel;
export const geminiModel = genAI.getGenerativeModel({ model: geminiModelName });
