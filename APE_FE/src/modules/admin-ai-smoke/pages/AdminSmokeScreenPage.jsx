import { useEffect, useMemo, useState } from "react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { useInFlightGuard } from "../../../shared/guards";
import { InFlightNotice } from "../../../shared/ui";
import { getSmokeInitialState, runSmokeTest } from "../../../services/adminAiSmokeService";

function formatValue(value) {
  if (Array.isArray(value)) {
    return value.join(", ");
  }
  if (value && typeof value === "object") {
    return JSON.stringify(value);
  }
  return value === undefined || value === null || value === "" ? "N/A" : String(value);
}

function getInputValue(input) {
  if (input.type === "checkbox") {
    return Boolean(input.value);
  }
  return input.value ?? "";
}

function inferLanguageFromFileName(fileName) {
  const normalized = String(fileName || "").toLowerCase();
  if (!normalized) {
    return "";
  }

  if (normalized.endsWith(".java")) {
    return "java";
  }

  if (normalized.endsWith(".c") || normalized.endsWith(".h") || normalized.endsWith(".cpp")) {
    return "c";
  }

  return "en";
}

export function AdminSmokeScreenPage({
  activeRoute,
  heroIcon,
  heroTitle,
  heroSubtitle,
  title,
  subtitle,
  workflowStep,
  kind,
  fields,
  parsedSections
}) {
  const fieldStateKey = useMemo(
    () => JSON.stringify(fields.map((field) => [field.key, field.initialValue])),
    [fields]
  );
  const initialState = useMemo(
    () => getSmokeInitialState(kind, fields),
    [fieldStateKey, kind]
  );
  const [formState, setFormState] = useState(initialState);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [isRunning, setIsRunning] = useState(false);
  const [copiedLabel, setCopiedLabel] = useState("");

  useInFlightGuard(isRunning, "Running smoke test...");

  useEffect(() => {
    setFormState(initialState);
    setResult(null);
    setError(null);
  }, [initialState]);

  async function handleRun() {
    setIsRunning(true);
    setError(null);
    try {
      const response = await runSmokeTest(kind, formState);
      setResult(response);
    } catch (nextError) {
      setResult(null);
      setError(nextError);
    } finally {
      setIsRunning(false);
    }
  }

  async function handleCopy(label, value) {
    if (!navigator?.clipboard) {
      return;
    }
    await navigator.clipboard.writeText(JSON.stringify(value, null, 2));
    setCopiedLabel(label);
    window.setTimeout(() => setCopiedLabel(""), 1200);
  }

  async function handleFileChange(field, file) {
    if (!file) {
      return;
    }

    try {
      const content = await file.text();
      setFormState((current) => {
        const next = {
          ...current,
          [field.key]: file.name,
          file_name: file.name
        };

        if ("content" in current) {
          next.content = content;
        }

        if ("raw_text" in current && !current.raw_text) {
          next.raw_text = content;
        }

        if ("language" in current && !current.language) {
          next.language = inferLanguageFromFileName(file.name);
        }

        return next;
      });
      setError(null);
    } catch {
      setError({
        error: "This file cannot be read in the browser. Please choose a text-based file for the smoke test.",
        errorCode: "smoke.file_read_failed"
      });
    }
  }

  return (
    <AdminScaffold
      activeRoute={activeRoute}
      heroIcon={heroIcon}
      heroTitle={heroTitle}
      heroSubtitle={heroSubtitle}
      title={title}
      subtitle={subtitle}
    >
      {isRunning ? <InFlightNotice title="Running smoke test" description="The request is in-flight and navigation is guarded." /> : null}

      {workflowStep ? (
        <section className="admin-smoke-flow">
          <div className="admin-smoke-flow__header">
            <strong>Recommended Smoke Flow</strong>
            <span>{workflowStep}</span>
          </div>
          <div className="admin-smoke-flow__steps">
            <article className={`admin-smoke-flow__step${workflowStep === "Step 1" ? " is-active" : ""}`}>
              <span>Step 1</span>
              <strong>Gatekeeper -&gt; Extracted Content -&gt; Embedding + AutoTagging</strong>
              <p>Validate source safety, normalized content, and tagging readiness before generation.</p>
            </article>
            <article className={`admin-smoke-flow__step${workflowStep === "Step 2" ? " is-active" : ""}`}>
              <span>Step 2</span>
              <strong>Generation + Review</strong>
              <p>Run the generator and reviewer only after upstream content checks are stable.</p>
            </article>
            <article className={`admin-smoke-flow__step${workflowStep === "Step 3" ? " is-active" : ""}`}>
              <span>Step 3</span>
              <strong>Code Mentor</strong>
              <p>Verify mentor guidance after the content and generation pipeline is behaving correctly.</p>
            </article>
          </div>
        </section>
      ) : null}

      <section className="admin-smoke-layout">
        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Input</h2>
              <p>Form input and runtime override for this smoke flow.</p>
            </div>
          </div>

          <div className="admin-ai-detail-panel">
            <div className="admin-ai-form-grid">
              {fields.map((field) => (
                <Field key={field.key} label={field.label} hint={field.hint} className={field.fullWidth ? "admin-ai-field--full" : ""}>
                  {field.type === "textarea" ? (
                    <textarea
                      className="admin-ai-textarea"
                      value={getInputValue({ type: field.type, value: formState[field.key] })}
                      onChange={(event) => setFormState((current) => ({ ...current, [field.key]: event.target.value }))}
                    />
                  ) : field.type === "select" ? (
                    <select
                      className="admin-ai-select"
                      value={getInputValue({ type: field.type, value: formState[field.key] })}
                      onChange={(event) => setFormState((current) => ({ ...current, [field.key]: event.target.value }))}
                    >
                      {field.options.map((option) => (
                        <option key={option.value} value={option.value}>{option.label}</option>
                      ))}
                    </select>
                  ) : field.type === "checkbox" ? (
                    <label className="admin-ai-toggle">
                      <input
                        type="checkbox"
                        checked={Boolean(formState[field.key])}
                        onChange={(event) => setFormState((current) => ({ ...current, [field.key]: event.target.checked }))}
                      />
                      <span>{field.checkboxLabel || field.label}</span>
                    </label>
                  ) : field.type === "file" ? (
                    <div className="admin-ai-file-field">
                      <input
                        className="ui-text-input"
                        type="file"
                        accept={field.accept}
                        onChange={(event) => handleFileChange(field, event.target.files?.[0])}
                      />
                      <small>{formState[field.key] ? `Da chon: ${formState[field.key]}` : "Chua chon file"}</small>
                    </div>
                  ) : (
                    <input
                      className="ui-text-input"
                      type={field.type || "text"}
                      value={getInputValue({ type: field.type, value: formState[field.key] })}
                      onChange={(event) => setFormState((current) => ({ ...current, [field.key]: event.target.value }))}
                    />
                  )}
                </Field>
              ))}
            </div>

            <div className="admin-ai-actions">
              <span className="admin-ai-inline-note">{copiedLabel ? `${copiedLabel} copied.` : ""}</span>
              <div className="admin-ai-actions__group">
                <Button variant="secondary" onClick={() => handleCopy("Request", formState)}>Copy Request</Button>
                <Button variant="secondary" onClick={() => result && handleCopy("Response", result)} disabled={!result}>Copy Response</Button>
                <Button variant="secondary" onClick={() => { setFormState(initialState); setResult(null); setError(null); }}>Reset</Button>
                <Button variant="primary" onClick={handleRun} disabled={isRunning}>{isRunning ? "Running..." : "Run"}</Button>
              </div>
            </div>
          </div>
        </section>

        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Parsed Result</h2>
              <p>Operational parsed view for quick debugging.</p>
            </div>
          </div>

          {result ? (
            <div className="admin-ai-detail-panel">
              <div className="admin-ai-health-meta admin-ai-health-meta--summary">
                <div>
                  <span>Provider</span>
                  <strong>{formatValue(result.metadata?.provider)}</strong>
                </div>
                <div>
                  <span>Effective Model</span>
                  <strong>{formatValue(result.metadata?.effective_model)}</strong>
                </div>
                <div>
                  <span>Fallback</span>
                  <strong>{result.metadata?.fallback_used ? "Used" : "No"}</strong>
                </div>
              </div>

              <div className="admin-ai-feedback-copy">
                {parsedSections.map((section) => (
                  <section key={section.title}>
                    <h3>{section.title}</h3>
                    <div className="admin-ai-meta-stack">
                      {section.keys.map((key) => (
                        <span key={key}>
                          {key}: {formatValue(result.parsed?.[key])}
                        </span>
                      ))}
                    </div>
                  </section>
                ))}
              </div>
            </div>
          ) : (
            <div className="admin-empty-state">Run the smoke test to inspect parsed output.</div>
          )}
        </section>

        <section className="admin-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Raw JSON / Debug</h2>
              <p>Raw response plus fallback-chain diagnostics.</p>
            </div>
          </div>

          <div className="admin-ai-detail-panel">
            {error ? (
              <div className="admin-ai-feedback-copy">
                <section>
                  <h3>Error</h3>
                  <div className="admin-ai-meta-stack">
                    <span>error: {error.error || error.message}</span>
                    <span>errorCode: {error.errorCode || "N/A"}</span>
                    <span>traceId: {error.traceId || "N/A"}</span>
                  </div>
                </section>
                {error.errorCode === "ai.runtime.fallback_chain_failed" ? (
                  <section>
                    <h3>Fallback Debug</h3>
                    <div className="admin-ai-health-meta admin-ai-health-meta--summary">
                      <div>
                        <span>Primary Attempt</span>
                        <strong>{formatValue(error.details?.primary?.provider)} / {formatValue(error.details?.primary?.model)}</strong>
                        <small>{formatValue(error.details?.primary?.reason_code)}</small>
                      </div>
                      <div>
                        <span>Fallback Attempt</span>
                        <strong>{formatValue(error.details?.fallback?.provider)} / {formatValue(error.details?.fallback?.model)}</strong>
                        <small>{formatValue(error.details?.fallback?.reason_code)}</small>
                      </div>
                      <div>
                        <span>Feature</span>
                        <strong>{formatValue(error.details?.feature)}</strong>
                        <small>{formatValue(error.details?.execution_type)}</small>
                      </div>
                    </div>
                  </section>
                ) : null}
                <pre className="admin-ai-json-block">{JSON.stringify(error, null, 2)}</pre>
              </div>
            ) : result ? (
              <pre className="admin-ai-json-block">{JSON.stringify(result.raw, null, 2)}</pre>
            ) : (
              <div className="admin-empty-state">No raw response yet.</div>
            )}
          </div>
        </section>
      </section>
    </AdminScaffold>
  );
}
