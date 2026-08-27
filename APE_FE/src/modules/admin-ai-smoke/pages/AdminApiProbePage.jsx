import { useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { getApiBaseUrl } from "../../../lib/env";
import { ROUTES } from "../../../lib/routes";
import { http } from "../../../services/http";

const PRESET_REQUESTS = [
  { label: "Student Documents", method: "GET", path: "/api/student/documents?page=1&limit=10" },
  { label: "System Courses", method: "GET", path: "/api/student/courses?page=1&limit=20" },
  { label: "System AI Docs", method: "GET", path: "/api/ai/courses/PRO192/documents" },
  { label: "BYOS Preview", method: "GET", path: "/api/student/documents/<documentId>/preview" },
  { label: "BYOS Draft", method: "GET", path: "/api/student/documents/<documentId>/extraction-draft" },
  { label: "BYOS Chapters", method: "GET", path: "/api/student/documents/<documentId>/chapters" }
];

function prettyJson(value) {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value ?? "");
  }
}

async function runManualProbe({ method, path, body, bearerToken }) {
  const headers = {};
  if (bearerToken?.trim()) {
    headers.Authorization = `Bearer ${bearerToken.trim()}`;
  }
  if (method !== "GET") {
    headers["Content-Type"] = "application/json";
  }

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    method,
    headers,
    body: method !== "GET" && body.trim() ? body.trim() : undefined
  });

  let payload = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  if (!response.ok) {
    throw new Error(payload?.error || payload?.message || `Request failed with status ${response.status}.`);
  }

  return payload;
}

export function AdminApiProbePage() {
  const [method, setMethod] = useState("GET");
  const [path, setPath] = useState("/api/student/documents?page=1&limit=10");
  const [body, setBody] = useState("");
  const [authMode, setAuthMode] = useState("session");
  const [bearerToken, setBearerToken] = useState("");
  const [responseText, setResponseText] = useState("");
  const [statusText, setStatusText] = useState("");
  const [isRunning, setIsRunning] = useState(false);

  const selectedPreset = useMemo(
    () => PRESET_REQUESTS.find((item) => item.method === method && item.path === path) || null,
    [method, path]
  );

  async function handleRun() {
    if (!path.trim()) {
      setStatusText("Path is required.");
      return;
    }

    setIsRunning(true);
    setStatusText("");
    setResponseText("");

    try {
      const payload = authMode === "manual"
        ? await runManualProbe({
            method,
            path: path.trim(),
            body,
            bearerToken
          })
        : await http(path.trim(), {
            method,
            ...(method !== "GET" && body.trim() ? { body: body.trim() } : {})
          });
      setStatusText("Request completed.");
      setResponseText(prettyJson(payload));
    } catch (error) {
      setStatusText(error.message || "Request failed.");
      setResponseText("");
    } finally {
      setIsRunning(false);
    }
  }

  function applyPreset(preset) {
    setMethod(preset.method);
    setPath(preset.path);
    setBody("");
    setStatusText("");
    setResponseText("");
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiApiProbe}
      heroIcon="search"
      heroTitle="AI Smoke Tests"
      heroSubtitle="API probe"
      title="FE <> BE API Probe"
      subtitle="Run authenticated requests from the FE runtime to inspect the real backend payload and catch contract drift early."
    >
      <section className="admin-ai-settings-grid">
        <section className="admin-table-panel admin-ai-list-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Preset Requests</h2>
              <p>Quick launch common endpoints used by BYOS and generation flows.</p>
            </div>
          </div>
          <div className="admin-ai-card-list">
            {PRESET_REQUESTS.map((preset) => (
              <button
                key={`${preset.method}:${preset.path}`}
                type="button"
                className={`admin-ai-select-card${selectedPreset?.path === preset.path && selectedPreset?.method === preset.method ? " is-active" : ""}`}
                onClick={() => applyPreset(preset)}
              >
                <div className="admin-ai-select-card__header">
                  <strong>{preset.label}</strong>
                  <span className="admin-ai-badge is-neutral">{preset.method}</span>
                </div>
                <p>{preset.path}</p>
              </button>
            ))}
          </div>
        </section>

        <section className="admin-table-panel admin-ai-form-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Manual Request</h2>
              <p>Use relative API paths. Auth headers come from the current FE session.</p>
            </div>
          </div>

          <div className="admin-ai-form-grid">
            <Field label="Method">
              <select className="admin-ai-select" value={method} onChange={(event) => setMethod(event.target.value)}>
                <option value="GET">GET</option>
                <option value="POST">POST</option>
              </select>
            </Field>

            <Field label="Auth Mode">
              <select className="admin-ai-select" value={authMode} onChange={(event) => setAuthMode(event.target.value)}>
                <option value="session">Use FE session</option>
                <option value="manual">Paste bearer token</option>
              </select>
            </Field>

            <Field label="Path" className="admin-ai-field--full">
              <input className="ui-text-input" value={path} onChange={(event) => setPath(event.target.value)} />
            </Field>

            {authMode === "manual" ? (
              <Field
                label="Bearer Token"
                className="admin-ai-field--full"
                hint="Dev-only bypass for protected APIs without logging into the FE. Paste the raw JWT only, without the Bearer prefix."
              >
                <textarea
                  className="admin-ai-textarea"
                  value={bearerToken}
                  onChange={(event) => setBearerToken(event.target.value)}
                  placeholder="eyJhbGciOi..."
                />
              </Field>
            ) : null}

            <Field
              label="JSON Body"
              className="admin-ai-field--full"
              hint="Only used for POST. Leave empty for GET or body-less POST."
            >
              <textarea
                className="admin-ai-textarea"
                value={body}
                onChange={(event) => setBody(event.target.value)}
                placeholder='{"documentId":"...","chapterKeys":["..."]}'
              />
            </Field>
          </div>

          <div className="admin-ai-actions">
            <span className="admin-ai-inline-note">
              {statusText || (authMode === "manual"
                ? "Manual bearer mode bypasses FE login but still calls the real protected backend route."
                : "Run one request to inspect the live payload shape.")}
            </span>
            <div className="admin-ai-actions__group">
              <Button variant="primary" onClick={handleRun} disabled={isRunning}>
                {isRunning ? "Running..." : "Run Request"}
              </Button>
            </div>
          </div>

          <div className="ai-preview-block">
            {responseText || "No response yet."}
          </div>
        </section>
      </section>
    </AdminScaffold>
  );
}
