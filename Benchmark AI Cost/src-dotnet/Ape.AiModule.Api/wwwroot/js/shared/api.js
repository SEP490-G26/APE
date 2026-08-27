import { safeParseJson } from './dom.js';

export async function fetchJson(url, options = {}) {
  const response = await fetch(url, {
    headers: {
      Accept: 'application/json',
      ...(options.headers ?? {})
    },
    ...options
  });

  const text = await response.text();
  const payload = text ? safeParseJson(text) ?? { raw: text } : null;
  if (!response.ok) {
    throw payload ?? { status: response.status, message: response.statusText };
  }

  return payload;
}

export function postJson(url, body) {
  return fetchJson(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
}

export function putJson(url, body) {
  return fetchJson(url, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
}

export async function uploadFile(file) {
  const formData = new FormData();
  formData.append('file', file);
  const response = await fetch('/api/ai-module/uploads/ingest', {
    method: 'POST',
    body: formData
  });

  const text = await response.text();
  const payload = text ? safeParseJson(text) ?? { raw: text } : null;
  if (!response.ok) {
    throw payload ?? { status: response.status, message: response.statusText };
  }

  return payload;
}
