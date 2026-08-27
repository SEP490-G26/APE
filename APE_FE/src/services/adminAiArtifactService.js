import { http } from "./http";

function unwrapData(payload) {
  return payload?.data ?? payload;
}

function normalizeScope(scope = {}) {
  return {
    subjectCode: scope.subjectCode ?? scope.SubjectCode ?? null,
    questionType: scope.questionType ?? scope.QuestionType ?? null,
    language: scope.language ?? scope.Language ?? null
  };
}

function normalizeVersion(item) {
  return {
    id: item?.id ?? item?.Id ?? "",
    version: item?.version ?? item?.Version ?? "v1",
    isActive: Boolean(item?.isActive ?? item?.IsActive),
    changeReason: item?.changeReason ?? item?.ChangeReason ?? "",
    changeNotes: item?.changeNotes ?? item?.ChangeNotes ?? [],
    content: item?.content ?? item?.Content ?? {},
    source: item?.source ?? item?.Source ?? "db",
    sourcePath: item?.sourcePath ?? item?.SourcePath ?? "",
    contentHash: item?.contentHash ?? item?.ContentHash ?? "",
    createdBy: item?.createdBy ?? item?.CreatedBy ?? "",
    updatedBy: item?.updatedBy ?? item?.UpdatedBy ?? "",
    activatedBy: item?.activatedBy ?? item?.ActivatedBy ?? "",
    createdAt: item?.createdAt ?? item?.CreatedAt ?? null,
    updatedAt: item?.updatedAt ?? item?.UpdatedAt ?? null,
    activatedAt: item?.activatedAt ?? item?.ActivatedAt ?? null
  };
}

function normalizeArtifact(item) {
  const versions = Array.isArray(item?.versions ?? item?.Versions)
    ? (item.versions ?? item.Versions).map(normalizeVersion)
    : [];

  return {
    id: item?.id ?? item?.Id ?? "",
    artifactType: item?.artifactType ?? item?.ArtifactType ?? "",
    artifactKey: item?.artifactKey ?? item?.ArtifactKey ?? "",
    description: item?.description ?? item?.Description ?? "",
    summary: item?.summary ?? item?.Summary ?? "",
    scope: normalizeScope(item?.scope ?? item?.Scope),
    versions,
    activeVersion: item?.activeVersion ?? item?.ActiveVersion ? normalizeVersion(item?.activeVersion ?? item?.ActiveVersion) : versions.find((version) => version.isActive) || null,
    totalVersions: Number(item?.totalVersions ?? item?.TotalVersions ?? versions.length),
    updatedAt: item?.updatedAt ?? item?.UpdatedAt ?? null,
    updatedBy: item?.updatedBy ?? item?.UpdatedBy ?? null
  };
}

function stringifyContent(content) {
  return JSON.stringify(content, null, 2);
}

export async function getRuleArtifacts() {
  const payload = await http("/api/admin/ai/rule-artifacts");
  const data = unwrapData(payload);
  return Array.isArray(data) ? data.map(normalizeArtifact) : [];
}

export async function createRuleArtifact(draft) {
  const response = await http("/api/admin/ai/rule-artifacts", {
    method: "POST",
    body: JSON.stringify({
      artifactType: draft.artifactType,
      artifactKey: draft.artifactKey,
      description: draft.description,
      summary: draft.summary,
      scope: draft.scope,
      contentJson: stringifyContent(draft.content),
      changeReason: draft.changeReason,
      changeNotes: draft.changeNotes,
      activateNow: draft.activateNow
    })
  });

  return normalizeArtifact(unwrapData(response));
}

export async function createRuleArtifactVersion(artifactId, draft) {
  const response = await http(`/api/admin/ai/rule-artifacts/${encodeURIComponent(artifactId)}/versions`, {
    method: "POST",
    body: JSON.stringify({
      baseVersion: draft.baseVersion,
      contentJson: stringifyContent(draft.content),
      changeReason: draft.changeReason,
      changeNotes: draft.changeNotes,
      activateNow: draft.activateNow
    })
  });

  return normalizeArtifact(unwrapData(response));
}

export async function activateRuleArtifactVersion(artifactId, versionName) {
  const response = await http(`/api/admin/ai/rule-artifacts/${encodeURIComponent(artifactId)}/versions/${encodeURIComponent(versionName)}/activate`, {
    method: "POST"
  });

  return normalizeArtifact(unwrapData(response));
}

export async function deleteRuleArtifactVersion(artifactId, versionName) {
  const response = await http(`/api/admin/ai/rule-artifacts/${encodeURIComponent(artifactId)}/versions/${encodeURIComponent(versionName)}`, {
    method: "DELETE"
  });

  return normalizeArtifact(unwrapData(response));
}

export async function deleteRuleArtifact(artifactId) {
  const response = await http(`/api/admin/ai/rule-artifacts/${encodeURIComponent(artifactId)}`, {
    method: "DELETE"
  });

  return unwrapData(response);
}
