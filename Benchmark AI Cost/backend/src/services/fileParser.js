import path from "node:path";
import { PDFParse } from "pdf-parse";
import mammoth from "mammoth";
import XLSX from "xlsx";
import { parseOffice } from "officeparser";
import { GoogleGenerativeAI } from "@google/generative-ai";
import OpenAI from "openai";
import JSZip from "jszip";
import { config } from "../config.js";

const IMAGE_EXTENSIONS = new Set([".png", ".jpg", ".jpeg", ".webp", ".gif"]);

const toMarkdownParagraphs = (text = "") =>
  text
    .split(/\r?\n\s*\r?\n/g)
    .map((block) => block.trim())
    .filter(Boolean)
    .map((block) => `${block}\n`)
    .join("\n");

const worksheetToMarkdown = (worksheet, sheetName) => {
  const rows = XLSX.utils.sheet_to_json(worksheet, { header: 1, raw: false });
  if (!rows.length) return `## ${sheetName}\n\n_(Empty sheet)_\n`;

  const header = rows[0].map((cell) => `${cell ?? ""}`.trim());
  const normalizedHeader = header.map((cell, idx) => cell || `col_${idx + 1}`);
  const separator = normalizedHeader.map(() => "---");

  const bodyRows = rows.slice(1).map((row) =>
    normalizedHeader.map((_, idx) => `${row?.[idx] ?? ""}`.replace(/\|/g, "\\|").trim())
  );

  const lines = [
    `## ${sheetName}`,
    "",
    `| ${normalizedHeader.join(" | ")} |`,
    `| ${separator.join(" | ")} |`,
    ...bodyRows.map((row) => `| ${row.join(" | ")} |`),
    "",
  ];

  return lines.join("\n");
};

const parseImageWithVision = async (buffer, mimeType, options = {}) => {
  const provider = options?.visionProvider || "gemini";
  const model = options?.visionModel || config.geminiModel;
  const visionPrompt =
    options?.visionPrompt ||
    "Extract and structure all visible text from this document image as markdown. Keep headings, lists, and tables if present.";

  if (provider === "openai") {
    const openaiApiKey = options?.credentials?.openaiApiKey || config.openaiApiKey;
    const openaiBaseUrl = options?.credentials?.openaiBaseUrl || config.openaiBaseUrl;
    if (!openaiApiKey) throw new Error("Missing OpenAI API key for image extraction");
    const openai = new OpenAI({ apiKey: openaiApiKey, ...(openaiBaseUrl ? { baseURL: openaiBaseUrl } : {}) });
    const dataUrl = `data:${mimeType || "image/png"};base64,${buffer.toString("base64")}`;
    const resp = await openai.chat.completions.create({
      model,
      messages: [
        {
          role: "user",
          content: [
            { type: "text", text: visionPrompt },
            { type: "image_url", image_url: { url: dataUrl } },
          ],
        },
      ],
      temperature: 0.2,
    });
    return {
      markdown: resp.choices?.[0]?.message?.content || "",
      usage: {
        promptTokenCount: Number(resp.usage?.prompt_tokens || 0),
        candidatesTokenCount: Number(resp.usage?.completion_tokens || 0),
      },
      parserType: "openai_vision",
      visionModel: model,
    };
  }

  const apiKey = options?.credentials?.geminiApiKey || config.geminiApiKey;
  if (!apiKey) throw new Error("Missing Gemini API key for image extraction");
  const genAI = new GoogleGenerativeAI(apiKey);
  const geminiModel = genAI.getGenerativeModel({ model });
  const result = await geminiModel.generateContent([
    { text: visionPrompt },
    {
      inlineData: {
        mimeType,
        data: buffer.toString("base64"),
      },
    },
  ]);
  const response = result.response;
  return {
    markdown: response.text?.() || "",
    usage: response.usageMetadata || {},
    parserType: "gemini_vision",
    visionModel: model,
  };
};

const parsePdf = async (buffer) => {
  const parser = new PDFParse({ data: buffer });
  try {
    const parsed = await parser.getText();
    return {
      markdown: toMarkdownParagraphs(parsed.text || ""),
      usage: {},
      parserType: "pdf_parse",
    };
  } finally {
    await parser.destroy();
  }
};

const parseDocxOrDoc = async (buffer, ext) => {
  if (ext === ".doc") {
    throw new Error("Legacy .doc is not reliably parseable. Please convert to .docx for accurate extraction.");
  }

  const { value } = await mammoth.convertToMarkdown({ buffer });
  return {
    markdown: value || "",
    usage: {},
    parserType: "mammoth_docx",
  };
};

const parsePptxOrPpt = async (buffer, ext) => {
  if (ext === ".ppt") {
    throw new Error("Legacy .ppt is not reliably parseable. Please convert to .pptx for accurate extraction.");
  }

  const ast = await parseOffice(buffer, { fileType: "pptx" });
  const md = await ast.to("md");

  return {
    markdown: md?.value || "",
    usage: {},
    parserType: "officeparser_pptx",
  };
};

const parseSpreadsheet = (buffer) => {
  const workbook = XLSX.read(buffer, { type: "buffer" });
  const markdown = workbook.SheetNames.map((name) => worksheetToMarkdown(workbook.Sheets[name], name)).join("\n");

  return {
    markdown,
    usage: {},
    parserType: "xlsx",
  };
};

const parsePlainText = (buffer) => ({
  markdown: buffer.toString("utf-8"),
  usage: {},
  parserType: "plain_text",
});

export const parseFileToMarkdown = async ({ buffer, mimetype, originalname }, options = {}) => {
  const ext = path.extname(originalname || "").toLowerCase();

  if (IMAGE_EXTENSIONS.has(ext) || (mimetype || "").startsWith("image/")) {
    return parseImageWithVision(buffer, mimetype || "image/png", options);
  }

  if (ext === ".pdf" || mimetype === "application/pdf") {
    return parsePdf(buffer);
  }

  if (ext === ".docx" || ext === ".doc") {
    return parseDocxOrDoc(buffer, ext);
  }

  if (ext === ".pptx" || ext === ".ppt") {
    return parsePptxOrPpt(buffer, ext);
  }

  if ([".xlsx", ".xls", ".csv"].includes(ext)) {
    return parseSpreadsheet(buffer);
  }

  if ([".txt", ".md"].includes(ext)) {
    return parsePlainText(buffer);
  }

  throw new Error(`Unsupported file type: ${ext || mimetype || "unknown"}`);
};

const imageMimeFromName = (name = "") => {
  const n = name.toLowerCase();
  if (n.endsWith(".png")) return "image/png";
  if (n.endsWith(".jpg") || n.endsWith(".jpeg")) return "image/jpeg";
  if (n.endsWith(".webp")) return "image/webp";
  if (n.endsWith(".gif")) return "image/gif";
  return null;
};

export const extractEmbeddedImagesFromOfficeBuffer = async ({ buffer, originalname }) => {
  const ext = path.extname(originalname || "").toLowerCase();
  if (![".docx", ".pptx", ".xlsx"].includes(ext)) return [];

  const zip = await JSZip.loadAsync(buffer);
  const paths = Object.keys(zip.files);
  const mediaPrefix = ext === ".docx" ? "word/media/" : ext === ".pptx" ? "ppt/media/" : "xl/media/";
  const imagePaths = paths.filter((p) => p.startsWith(mediaPrefix) && !zip.files[p].dir);

  const out = [];
  for (let i = 0; i < imagePaths.length; i += 1) {
    const p = imagePaths[i];
    const bytes = await zip.files[p].async("nodebuffer");
    const mimeType = imageMimeFromName(p);
    out.push({
      imageIndex: i,
      name: p.split("/").pop(),
      path: p,
      mimeType,
      buffer: bytes,
    });
  }
  return out;
};
