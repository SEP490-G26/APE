import { useEffect, useMemo, useRef, useState } from "react";
import Editor from "@monaco-editor/react";
import { Button, Field } from "../../../components/common";
import { AdminScaffold } from "../../../components/admin/AdminScaffold";
import { ROUTES } from "../../../lib/routes";
import { useInFlightGuard } from "../../../shared/guards";
import { ConfirmModal, InFlightNotice, ToastNotification } from "../../../shared/ui";
import {
  activateRuleArtifactVersion,
  createRuleArtifact,
  createRuleArtifactVersion,
  deleteRuleArtifact,
  deleteRuleArtifactVersion,
  getRuleArtifacts
} from "../../../services/adminAiArtifactService";

function formatDateTime(value) {
  return value ? new Date(value).toLocaleString("vi-VN") : "Unknown";
}

function buildEmptyDraft() {
  return {
    artifactType: "Policy",
    artifactKey: "",
    description: "",
    summary: "",
    scope: {
      subjectCode: "",
      questionType: "",
      language: ""
    },
    baseVersion: "",
    changeReason: "",
    changeNotes: "",
    activateNow: true,
    contentText: "{\n  \n}"
  };
}

function normalizeText(value) {
  return value?.trim() || "";
}

function parseJson(text) {
  return JSON.parse(text);
}

function validateContentJsonByType(artifactType, parsed) {
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    return "Content JSON must be a JSON object.";
  }

  const type = String(artifactType || "").trim();
  if (!type) {
    return "Artifact type is required.";
  }

  if (type === "Prompt") {
    const systemPrompt = parsed.systemPrompt ?? parsed.SystemPrompt;
    const userPrompt = parsed.userPrompt ?? parsed.UserPrompt;

    if (typeof systemPrompt !== "string" || !systemPrompt.trim()) {
      return "Prompt content must include a non-empty 'systemPrompt' field.";
    }

    if (typeof userPrompt !== "string" || !userPrompt.trim()) {
      return "Prompt content must include a non-empty 'userPrompt' field.";
    }

    return null;
  }

  if (type === "Taxonomy") {
    if (!Array.isArray(parsed.subjects) || parsed.subjects.length === 0) {
      return "Taxonomy content must include a non-empty 'subjects' array.";
    }

    for (const [index, subject] of parsed.subjects.entries()) {
      if (!subject || typeof subject !== "object" || Array.isArray(subject)) {
        return `Taxonomy subject at index ${index} must be a JSON object.`;
      }

      if (typeof subject.subjectCode !== "string" || !subject.subjectCode.trim()) {
        return `Taxonomy subject at index ${index} must include a non-empty 'subjectCode'.`;
      }

      if ("tags" in subject) {
        if (!Array.isArray(subject.tags)) {
          return `Taxonomy subject '${subject.subjectCode}' must have 'tags' as an array.`;
        }

        for (const [tagIndex, tag] of subject.tags.entries()) {
          if (!tag || typeof tag !== "object" || Array.isArray(tag)) {
            return `Taxonomy tag at '${subject.subjectCode}' index ${tagIndex} must be a JSON object.`;
          }
          if (typeof tag.canonical !== "string" || !tag.canonical.trim()) {
            return `Taxonomy tag at '${subject.subjectCode}' index ${tagIndex} must include a non-empty 'canonical'.`;
          }
        }
      }
    }

    return null;
  }

  // Policy / Rubric / GroundTruth: currently treated as generic JSON objects by BE.
  return null;
}

function artifactMatchesSearch(artifact, searchTerm) {
  const normalizedSearch = searchTerm.trim().toLowerCase();
  const haystack = [
    artifact.artifactKey,
    artifact.description,
    artifact.summary,
    artifact.artifactType,
    artifact.scope.subjectCode,
    artifact.scope.questionType,
    artifact.scope.language
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(normalizedSearch);
}

function buildScopeLabel(scope) {
  const parts = [scope.subjectCode, scope.questionType, scope.language].filter(Boolean);
  return parts.length ? parts.join(" / ") : "Global";
}

function buildTypeBadgeClass(type) {
  return `admin-artifact-badge admin-artifact-badge--${String(type || "").toLowerCase()}`;
}

function buildVersionStatus(version) {
  return version.isActive ? "Active" : "Inactive";
}

function buildEditorTheme() {
  return document.documentElement.getAttribute("data-theme") === "dark" ? "vs-dark" : "vs";
}

export function AdminRuleArtifactsPage() {
  const [artifacts, setArtifacts] = useState([]);
  const [selectedArtifactId, setSelectedArtifactId] = useState("");
  const [selectedVersionName, setSelectedVersionName] = useState("");
  const [tab, setTab] = useState("overview");
  const [searchTerm, setSearchTerm] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [modalMode, setModalMode] = useState(null);
  const [draft, setDraft] = useState(buildEmptyDraft);
  const [toast, setToast] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [confirmState, setConfirmState] = useState(null);
  const contentUploadInputRef = useRef(null);

  useInFlightGuard(isSaving, "Saving rule artifact...");

  useEffect(() => {
    if (!toast) {
      return undefined;
    }

    const timeoutId = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(timeoutId);
  }, [toast]);

  useEffect(() => {
    let ignore = false;

    async function bootstrap() {
      setIsLoading(true);
      try {
        const response = await getRuleArtifacts();
        if (ignore) {
          return;
        }
        setArtifacts(response);
        setSelectedArtifactId((current) => current || response[0]?.id || "");
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    bootstrap();
    return () => {
      ignore = true;
    };
  }, []);

  const filteredArtifacts = useMemo(() => {
    return artifacts.filter((artifact) => {
      const hasActiveVersion = Boolean(artifact.activeVersion || artifact.versions?.some((version) => version.isActive));

      if (
        typeFilter !== "all" &&
        String(artifact.artifactType || "").toLowerCase() !== String(typeFilter).toLowerCase()
      ) {
        return false;
      }

      if (statusFilter === "active" && !hasActiveVersion) {
        return false;
      }

      if (statusFilter === "draft" && hasActiveVersion) {
        return false;
      }

      if (searchTerm && !artifactMatchesSearch(artifact, searchTerm)) {
        return false;
      }

      return true;
    });
  }, [artifacts, searchTerm, statusFilter, typeFilter]);

  const artifactTypeOptions = useMemo(() => {
    const discoveredTypes = Array.from(new Set(
      artifacts
        .map((artifact) => String(artifact.artifactType || "").trim())
        .filter(Boolean)
    ));

    const preferredOrder = ["Policy", "Rubric", "GroundTruth", "Prompt", "Taxonomy"];
    const ordered = [
      ...preferredOrder.filter((type) => discoveredTypes.some((item) => item.toLowerCase() === type.toLowerCase())),
      ...discoveredTypes
        .filter((type) => !preferredOrder.some((preferred) => preferred.toLowerCase() === type.toLowerCase()))
        .sort((left, right) => left.localeCompare(right))
    ];

    return ordered;
  }, [artifacts]);

  useEffect(() => {
    if (!filteredArtifacts.length) {
      setSelectedArtifactId("");
      return;
    }

    if (!filteredArtifacts.some((artifact) => artifact.id === selectedArtifactId)) {
      setSelectedArtifactId(filteredArtifacts[0].id);
    }
  }, [filteredArtifacts, selectedArtifactId]);

  const selectedArtifact = useMemo(
    () => artifacts.find((artifact) => artifact.id === selectedArtifactId) || null,
    [artifacts, selectedArtifactId]
  );

  useEffect(() => {
    if (!selectedArtifact?.versions?.length) {
      setSelectedVersionName("");
      return;
    }

    const preferredVersion = selectedArtifact.activeVersion?.version || selectedArtifact.versions[0]?.version || "";
    if (!selectedArtifact.versions.some((version) => version.version === selectedVersionName)) {
      setSelectedVersionName(preferredVersion);
    }
  }, [selectedArtifact, selectedVersionName]);

  const selectedVersion = useMemo(
    () => selectedArtifact?.versions.find((version) => version.version === selectedVersionName) || selectedArtifact?.activeVersion || null,
    [selectedArtifact, selectedVersionName]
  );

  function showToast(type, message) {
    setToast({ type, message });
  }

  function openCreateArtifactModal() {
    setModalMode("create-artifact");
    setDraft(buildEmptyDraft());
  }

  function openCreateVersionModal() {
    if (!selectedArtifact) {
      return;
    }

    setModalMode("create-version");
    setDraft({
      artifactType: selectedArtifact.artifactType,
      artifactKey: selectedArtifact.artifactKey,
      description: selectedArtifact.description,
      summary: selectedArtifact.summary,
      scope: {
        subjectCode: selectedArtifact.scope.subjectCode || "",
        questionType: selectedArtifact.scope.questionType || "",
        language: selectedArtifact.scope.language || ""
      },
      baseVersion: selectedVersion?.version || selectedArtifact.activeVersion?.version || selectedArtifact.versions[0]?.version || "",
      changeReason: "",
      changeNotes: "",
      activateNow: true,
      contentText: JSON.stringify(selectedVersion?.content || selectedArtifact.activeVersion?.content || {}, null, 2)
    });
  }

  function closeModal() {
    setModalMode(null);
    setDraft(buildEmptyDraft());
  }

  function handleUploadContentJson(event) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      try {
        const text = String(reader.result || "");
        const parsed = parseJson(text);
        const formatted = JSON.stringify(parsed, null, 2);
        setDraft((current) => ({ ...current, contentText: formatted }));
        showToast("success", "JSON uploaded.");
      } catch (error) {
        showToast("error", error?.message || "JSON is not valid.");
      } finally {
        // allow re-uploading same file
        event.target.value = "";
      }
    };
    reader.onerror = () => {
      showToast("error", "Unable to read the selected file.");
      event.target.value = "";
    };
    reader.readAsText(file);
  }

  async function handleSave() {
    try {
      const parsedContent = parseJson(draft.contentText);
      const effectiveType = modalMode === "create-version" ? selectedArtifact?.artifactType : draft.artifactType;
      const contentError = validateContentJsonByType(effectiveType, parsedContent);
      if (contentError) {
        showToast("error", contentError);
        return;
      }
      const changeNotes = normalizeText(draft.changeNotes)
        ? draft.changeNotes.split("\n").map((item) => item.trim()).filter(Boolean)
        : [];

      setToast(null);
      setIsSaving(true);

      if (modalMode === "create-artifact") {
        const created = await createRuleArtifact({
          artifactType: draft.artifactType,
          artifactKey: normalizeText(draft.artifactKey),
          description: normalizeText(draft.description),
          summary: normalizeText(draft.summary),
          scope: {
            subjectCode: normalizeText(draft.scope.subjectCode) || null,
            questionType: normalizeText(draft.scope.questionType) || null,
            language: normalizeText(draft.scope.language) || null
          },
          content: parsedContent,
          changeReason: normalizeText(draft.changeReason),
          changeNotes,
          activateNow: draft.activateNow
        });

        setArtifacts((current) => [created, ...current]);
        setSelectedArtifactId(created.id);
        setSelectedVersionName(created.activeVersion?.version || created.versions[0]?.version || "");
        showToast("success", "Rule artifact created.");
      } else if (modalMode === "create-version" && selectedArtifact) {
        const updated = await createRuleArtifactVersion(selectedArtifact.id, {
          baseVersion: draft.baseVersion,
          content: parsedContent,
          changeReason: normalizeText(draft.changeReason),
          changeNotes,
          activateNow: draft.activateNow
        });

        setArtifacts((current) => current.map((artifact) => (artifact.id === updated.id ? updated : artifact)));
        setSelectedArtifactId(updated.id);
        setSelectedVersionName(updated.versions[0]?.version || updated.activeVersion?.version || "");
        showToast("success", "New version created.");
      }

      closeModal();
    } catch (error) {
      showToast("error", error.message || "Could not save rule artifact.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleActivate(versionName) {
    if (!selectedArtifact) {
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      const updated = await activateRuleArtifactVersion(selectedArtifact.id, versionName);
      setArtifacts((current) => current.map((artifact) => (artifact.id === updated.id ? updated : artifact)));
      setSelectedVersionName(versionName);
      setConfirmState(null);
      showToast("success", `${versionName} is now active.`);
    } catch (error) {
      showToast("error", error.message || "Could not activate version.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeleteVersion(versionName) {
    if (!selectedArtifact) {
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      const updated = await deleteRuleArtifactVersion(selectedArtifact.id, versionName);
      setArtifacts((current) => current.map((artifact) => (artifact.id === updated.id ? updated : artifact)));
      setSelectedVersionName(updated.activeVersion?.version || updated.versions[0]?.version || "");
      setConfirmState(null);
      showToast("success", `${versionName} deleted.`);
    } catch (error) {
      showToast("error", error.message || "Could not delete version.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeleteArtifact() {
    if (!selectedArtifact) {
      return;
    }

    setToast(null);
    setIsSaving(true);
    try {
      await deleteRuleArtifact(selectedArtifact.id);
      setArtifacts((current) => current.filter((artifact) => artifact.id !== selectedArtifact.id));
      setSelectedArtifactId("");
      setSelectedVersionName("");
      setConfirmState(null);
      showToast("success", "Artifact deleted.");
    } catch (error) {
      showToast("error", error.message || "Could not delete artifact.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <AdminScaffold
      activeRoute={ROUTES.adminAiRuleArtifacts}
      heroIcon="doc"
      heroTitle="AI Settings"
      heroSubtitle="Versioned rule artifacts"
      title="Rule Artifact Management"
      subtitle="Manage policy, rubric, and ground-truth artifacts with version history, activation control, and JSON editing."
    >
      <ToastNotification toast={toast} />

      {isSaving ? (
        <InFlightNotice
          title="Updating rule artifacts"
          description="The selected artifact stays locked until the mutation completes."
        />
      ) : null}

      <section className="admin-filter-panel">
        <label className="admin-search-box">
          <span className="admin-search-box__icon">⌕</span>
          <input
            type="search"
            placeholder="Search by key, type, description, or scope"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
          />
        </label>

        <div className="admin-filter-group">
          <label className="admin-select-field">
            <span>Type</span>
            <select value={typeFilter} onChange={(event) => setTypeFilter(event.target.value)}>
              <option value="all">All</option>
              {artifactTypeOptions.map((type) => (
                <option key={type} value={type}>{type}</option>
              ))}
            </select>
          </label>

          <label className="admin-select-field">
            <span>Status</span>
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
              <option value="all">All</option>
              <option value="active">Has active version</option>
              <option value="draft">No active version</option>
            </select>
          </label>

          <Button variant="secondary" className="admin-filter-button" onClick={openCreateArtifactModal}>
            New Artifact
          </Button>
        </div>
      </section>

      <section className="admin-artifact-grid">
        <section className="admin-table-panel admin-artifact-table-panel">
          <div className="admin-ai-panel__header">
            <div>
              <h2>Artifact Catalog</h2>
              <p>Grouped by artifact identity with one active version per scope.</p>
            </div>
          </div>

          <div className="admin-table admin-artifact-table">
            <div className="admin-table__head admin-artifact-row admin-artifact-row--head">
              <div>Artifact</div>
              <div>Type</div>
              <div>Scope</div>
              <div>Active</div>
              <div>Versions</div>
              <div>Updated</div>
            </div>

            <div className="admin-table__body">
              {isLoading ? <div className="admin-empty-state">Loading rule artifacts...</div> : null}
              {!isLoading && !filteredArtifacts.length ? <div className="admin-empty-state">No artifacts match the current filters.</div> : null}

              {filteredArtifacts.map((artifact) => (
                <button
                  key={artifact.id}
                  type="button"
                  className={`admin-artifact-row admin-artifact-row--button${artifact.id === selectedArtifactId ? " is-selected" : ""}`}
                  onClick={() => {
                    setSelectedArtifactId(artifact.id);
                    setTab("overview");
                  }}
                >
                  <div className="admin-artifact-cell admin-artifact-cell--identity">
                    <strong>{artifact.artifactKey}</strong>
                    <p>{artifact.description || artifact.summary || "No description yet."}</p>
                  </div>
                  <div>
                    <span className={buildTypeBadgeClass(artifact.artifactType)}>{artifact.artifactType}</span>
                  </div>
                  <div>{buildScopeLabel(artifact.scope)}</div>
                  <div>{artifact.activeVersion?.version || "Draft only"}</div>
                  <div>{artifact.totalVersions}</div>
                  <div>{formatDateTime(artifact.updatedAt)}</div>
                </button>
              ))}
            </div>
          </div>
        </section>

        <section className="admin-table-panel admin-artifact-detail-panel">
          {selectedArtifact ? (
            <>
              <div className="admin-ai-panel__header">
                <div>
                  <h2>{selectedArtifact.artifactKey}</h2>
                  <p>
                    {selectedArtifact.artifactType} artifact. Active version {selectedArtifact.activeVersion?.version || "none"}.
                  </p>
                </div>

                <div className="admin-artifact-header-actions">
                  <Button variant="secondary" onClick={openCreateVersionModal}>
                    New Version
                  </Button>
                  <Button variant="secondary" onClick={() => setConfirmState({ type: "delete-artifact" })}>
                    Delete Artifact
                  </Button>
                </div>
              </div>

              <div className="admin-artifact-meta-strip">
                <span className={buildTypeBadgeClass(selectedArtifact.artifactType)}>{selectedArtifact.artifactType}</span>
                <span className="admin-artifact-meta-pill">Scope: {buildScopeLabel(selectedArtifact.scope)}</span>
                <span className="admin-artifact-meta-pill">Versions: {selectedArtifact.totalVersions}</span>
                <span className="admin-artifact-meta-pill">Updated by: {selectedArtifact.updatedBy || "Unknown"}</span>
              </div>

              <div className="admin-artifact-tabbar" role="tablist" aria-label="Rule artifact detail">
                <button
                  type="button"
                  className={`admin-artifact-tab${tab === "overview" ? " is-active" : ""}`}
                  onClick={() => setTab("overview")}
                >
                  Overview
                </button>
                <button
                  type="button"
                  className={`admin-artifact-tab${tab === "versions" ? " is-active" : ""}`}
                  onClick={() => setTab("versions")}
                >
                  Versions
                </button>
              </div>

              {tab === "overview" ? (
                <div className="admin-artifact-overview">
                  <section className="admin-artifact-info-grid">
                    <div className="admin-artifact-info-card">
                      <span className="admin-artifact-info-card__label">Description</span>
                      <strong>{selectedArtifact.description || "No description"}</strong>
                    </div>
                    <div className="admin-artifact-info-card">
                      <span className="admin-artifact-info-card__label">Summary</span>
                      <strong>{selectedArtifact.summary || "No summary"}</strong>
                    </div>
                    <div className="admin-artifact-info-card">
                      <span className="admin-artifact-info-card__label">Active Version</span>
                      <strong>{selectedArtifact.activeVersion?.version || "Draft only"}</strong>
                    </div>
                    <div className="admin-artifact-info-card">
                      <span className="admin-artifact-info-card__label">Last Update</span>
                      <strong>{formatDateTime(selectedArtifact.updatedAt)}</strong>
                    </div>
                  </section>

                  {selectedVersion ? (
                    <section className="admin-artifact-version-viewer">
                      <div className="admin-artifact-version-viewer__header">
                        <div>
                          <h3>{selectedVersion.version}</h3>
                          <p>
                            {buildVersionStatus(selectedVersion)}. Updated {formatDateTime(selectedVersion.updatedAt)} by {selectedVersion.updatedBy || "Unknown"}.
                          </p>
                        </div>
                        <div className="admin-artifact-inline-actions">
                          {!selectedVersion.isActive ? (
                            <Button
                              variant="secondary"
                              onClick={() => setConfirmState({ type: "activate-version", versionName: selectedVersion.version })}
                            >
                              Activate
                            </Button>
                          ) : null}
                          <Button
                            variant="secondary"
                            onClick={() => setConfirmState({ type: "delete-version", versionName: selectedVersion.version })}
                            disabled={selectedVersion.isActive}
                          >
                            Delete Version
                          </Button>
                        </div>
                      </div>

                      <div className="admin-artifact-version-meta">
                        <div>
                          <span>Reason</span>
                          <strong>{selectedVersion.changeReason || "No change reason recorded."}</strong>
                        </div>
                        <div>
                          <span>Source Path</span>
                          <strong>{selectedVersion.sourcePath || "N/A"}</strong>
                        </div>
                        <div>
                          <span>Activated At</span>
                          <strong>{formatDateTime(selectedVersion.activatedAt)}</strong>
                        </div>
                      </div>

                      <div className="admin-artifact-json-shell">
                        <Editor
                          height="360px"
                          defaultLanguage="json"
                          language="json"
                          theme={buildEditorTheme()}
                          value={JSON.stringify(selectedVersion.content, null, 2)}
                          options={{
                            readOnly: true,
                            minimap: { enabled: false },
                            scrollBeyondLastLine: false,
                            fontSize: 13,
                            wordWrap: "on"
                          }}
                        />
                      </div>
                    </section>
                  ) : (
                    <div className="admin-empty-state">Pick a version to inspect its JSON content.</div>
                  )}
                </div>
              ) : (
                <div className="admin-artifact-version-table">
                  <div className="admin-artifact-version-table__head">
                    <div>Version</div>
                    <div>Status</div>
                    <div>Reason</div>
                    <div>Updated</div>
                    <div>Actions</div>
                  </div>

                  <div className="admin-artifact-version-table__body">
                    {selectedArtifact.versions.map((version) => (
                      <div key={version.id} className="admin-artifact-version-row">
                        <div>
                          <button
                            type="button"
                            className={`admin-artifact-version-link${selectedVersionName === version.version ? " is-active" : ""}`}
                            onClick={() => {
                              setSelectedVersionName(version.version);
                              setTab("overview");
                            }}
                          >
                            {version.version}
                          </button>
                        </div>
                        <div>
                          <span className={`admin-artifact-status ${version.isActive ? "is-active" : ""}`}>
                            {buildVersionStatus(version)}
                          </span>
                        </div>
                        <div>{version.changeReason || "No reason"}</div>
                        <div>{formatDateTime(version.updatedAt)}</div>
                        <div className="admin-artifact-inline-actions">
                          {!version.isActive ? (
                            <Button
                              variant="secondary"
                              onClick={() => setConfirmState({ type: "activate-version", versionName: version.version })}
                            >
                              Activate
                            </Button>
                          ) : null}
                          <Button
                            variant="secondary"
                            onClick={() => setConfirmState({ type: "delete-version", versionName: version.version })}
                            disabled={version.isActive}
                          >
                            Delete
                          </Button>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          ) : (
            <div className="admin-empty-state admin-artifact-detail-empty">
              Select an artifact to inspect its versions, scope, and JSON content.
            </div>
          )}
        </section>
      </section>

      {modalMode ? (
        <div className="admin-artifact-modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="admin-artifact-modal"
            role="dialog"
            aria-modal="true"
            aria-label={modalMode === "create-artifact" ? "Create Rule Artifact" : "Create Rule Artifact Version"}
            onClick={(event) => event.stopPropagation()}
          >
            <div className="admin-artifact-modal__header">
              <div>
                <h2>{modalMode === "create-artifact" ? "Create Rule Artifact" : "Create New Version"}</h2>
                <p>
                  {modalMode === "create-artifact"
                    ? "Create a new versioned policy, rubric, or ground-truth artifact."
                    : `Create a new version for ${selectedArtifact?.artifactKey || "the selected artifact"}.`}
                </p>
              </div>
              <Button variant="secondary" onClick={closeModal}>
                Close
              </Button>
            </div>

            <div className="admin-artifact-modal__body">
              <section className="admin-artifact-form-grid">
                <Field label="Artifact Type">
                  <select
                    className="admin-ai-select"
                    value={draft.artifactType}
                    onChange={(event) => setDraft((current) => ({ ...current, artifactType: event.target.value }))}
                    disabled={modalMode !== "create-artifact"}
                  >
                    <option value="Policy">Policy</option>
                    <option value="Rubric">Rubric</option>
                    <option value="GroundTruth">GroundTruth</option>
                    <option value="Prompt">Prompt</option>
                    <option value="Taxonomy">Taxonomy</option>
                  </select>
                </Field>

                <Field label="Artifact Key">
                  <input
                    className="ui-text-input"
                    value={draft.artifactKey}
                    onChange={(event) => setDraft((current) => ({ ...current, artifactKey: event.target.value }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Description" className="admin-artifact-form-grid__full">
                  <input
                    className="ui-text-input"
                    value={draft.description}
                    onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Summary">
                  <input
                    className="ui-text-input"
                    value={draft.summary}
                    onChange={(event) => setDraft((current) => ({ ...current, summary: event.target.value }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Base Version" hint="Used only when creating a new version.">
                  <select
                    className="admin-ai-select"
                    value={draft.baseVersion}
                    onChange={(event) => setDraft((current) => ({ ...current, baseVersion: event.target.value }))}
                    disabled={modalMode !== "create-version"}
                  >
                    {selectedArtifact?.versions.map((version) => (
                      <option key={version.id} value={version.version}>
                        {version.version}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field label="Subject Code">
                  <input
                    className="ui-text-input"
                    value={draft.scope.subjectCode}
                    onChange={(event) => setDraft((current) => ({
                      ...current,
                      scope: { ...current.scope, subjectCode: event.target.value }
                    }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Question Type">
                  <input
                    className="ui-text-input"
                    value={draft.scope.questionType}
                    onChange={(event) => setDraft((current) => ({
                      ...current,
                      scope: { ...current.scope, questionType: event.target.value }
                    }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Language">
                  <input
                    className="ui-text-input"
                    value={draft.scope.language}
                    onChange={(event) => setDraft((current) => ({
                      ...current,
                      scope: { ...current.scope, language: event.target.value }
                    }))}
                    disabled={modalMode !== "create-artifact"}
                  />
                </Field>

                <Field label="Change Reason" className="admin-artifact-form-grid__full">
                  <input
                    className="ui-text-input"
                    value={draft.changeReason}
                    onChange={(event) => setDraft((current) => ({ ...current, changeReason: event.target.value }))}
                  />
                </Field>

                <Field label="Change Notes" hint="One note per line." className="admin-artifact-form-grid__full">
                  <textarea
                    className="admin-ai-textarea"
                    value={draft.changeNotes}
                    onChange={(event) => setDraft((current) => ({ ...current, changeNotes: event.target.value }))}
                  />
                </Field>

                <Field label="Activation">
                  <label className="admin-ai-toggle">
                    <input
                      type="checkbox"
                      checked={draft.activateNow}
                      onChange={(event) => setDraft((current) => ({ ...current, activateNow: event.target.checked }))}
                    />
                    <span>{draft.activateNow ? "Activate immediately" : "Save as inactive version"}</span>
                  </label>
                </Field>
              </section>

              <section className="admin-artifact-editor-panel">
                <div className="admin-artifact-editor-panel__header">
                  <div>
                    <h3>Content JSON</h3>
                    <p>Paste a valid JSON object. The editor is wired for backend JSON-string payloads.</p>
                  </div>
                  <div className="admin-artifact-editor-panel__actions">
                    <input
                      ref={contentUploadInputRef}
                      type="file"
                      accept=".json,application/json"
                      onChange={handleUploadContentJson}
                      style={{ display: "none" }}
                    />
                    <Button
                      variant="secondary"
                      onClick={() => contentUploadInputRef.current?.click()}
                      disabled={isSaving}
                    >
                      Upload JSON
                    </Button>
                    <Button
                      variant="secondary"
                      onClick={() => {
                        try {
                          const formatted = JSON.stringify(parseJson(draft.contentText), null, 2);
                          setDraft((current) => ({ ...current, contentText: formatted }));
                        } catch (error) {
                          showToast("error", error.message || "JSON is not valid.");
                        }
                      }}
                      disabled={isSaving}
                    >
                      Format JSON
                    </Button>
                  </div>
                </div>

                <div className="admin-artifact-editor-shell">
                  <Editor
                    height="360px"
                    defaultLanguage="json"
                    language="json"
                    theme={buildEditorTheme()}
                    value={draft.contentText}
                    onChange={(value) => setDraft((current) => ({ ...current, contentText: value || "" }))}
                    options={{
                      minimap: { enabled: false },
                      scrollBeyondLastLine: false,
                      fontSize: 13,
                      wordWrap: "on"
                    }}
                  />
                </div>
              </section>
            </div>

            <div className="admin-artifact-modal__footer">
              <span className="admin-ai-inline-note">
                Every version is stored in the database. Activating a version will automatically deactivate the previous active version in the same artifact scope.
              </span>
              <div className="admin-artifact-inline-actions">
                <Button variant="secondary" onClick={closeModal} disabled={isSaving}>
                  Cancel
                </Button>
                <Button variant="primary" onClick={handleSave} disabled={isSaving}>
                  {isSaving ? "Saving..." : modalMode === "create-artifact" ? "Create Artifact" : "Create Version"}
                </Button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={Boolean(confirmState)}
        title={
          confirmState?.type === "activate-version"
            ? "Activate version"
            : confirmState?.type === "delete-version"
              ? "Delete version"
              : "Delete artifact"
        }
        message={
          confirmState?.type === "activate-version"
            ? `Activate ${selectedArtifact?.artifactKey} ${confirmState.versionName}?`
            : confirmState?.type === "delete-version"
              ? `Delete version ${confirmState.versionName} from ${selectedArtifact?.artifactKey}?`
              : selectedArtifact
                ? `Delete artifact ${selectedArtifact.artifactKey}?`
                : ""
        }
        confirmLabel={confirmState?.type === "activate-version" ? "Activate" : "Delete"}
        isConfirming={isSaving}
        onCancel={() => !isSaving && setConfirmState(null)}
        onConfirm={() => {
          if (confirmState?.type === "activate-version") {
            return handleActivate(confirmState.versionName);
          }

          if (confirmState?.type === "delete-version") {
            return handleDeleteVersion(confirmState.versionName);
          }

          return handleDeleteArtifact();
        }}
      />
    </AdminScaffold>
  );
}
