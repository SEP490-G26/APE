export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

export function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

export function stringifyJson(value) {
  return JSON.stringify(value ?? {}, null, 2);
}

export function setJson(targetId, value) {
  const element = document.getElementById(targetId);
  if (!element) {
    return;
  }

  element.textContent = typeof value === "string" ? value : stringifyJson(value);
}

export function setHtml(targetId, html) {
  const element = document.getElementById(targetId);
  if (!element) {
    return;
  }

  element.innerHTML = html;
}

export function safeParseJson(text) {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

export function parseJsonInput(text, fallback = null) {
  if (!text || !text.trim()) {
    return fallback;
  }

  try {
    return JSON.parse(text);
  } catch (error) {
    throw new Error(`Invalid JSON input: ${error.message}`);
  }
}

export function parseCsvList(text) {
  return String(text ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export async function copyText(value) {
  await navigator.clipboard.writeText(String(value ?? ""));
}

export function exportJsonFile(name, data) {
  const blob = new Blob([stringifyJson(data)], { type: "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${name}-${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export async function readFileText(file) {
  return await file.text();
}
