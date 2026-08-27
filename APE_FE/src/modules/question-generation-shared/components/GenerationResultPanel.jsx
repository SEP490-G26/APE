import { Button } from "../../../components/common";

function formatList(items = []) {
  return items.length ? items.join(", ") : "-";
}

function formatCodeFiles(files = []) {
  return files.map((file) => `${file.name || file.filename || "file"}\n${file.content || ""}`).join("\n\n");
}

function formatVnd(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")} VND`;
}

function formatDifficultyProfile(profile = []) {
  if (!Array.isArray(profile) || !profile.length) {
    return "-";
  }

  return profile.map((item) => `${item.difficulty || "-"} ${item.count ?? 0}`).join(", ");
}

function formatQuestionOptions(options = []) {
  if (!Array.isArray(options) || !options.length) {
    return [];
  }

  return options
    .flatMap((option) => String(option || "").split("|"))
    .map((option) => option.trim())
    .filter(Boolean);
}

function buildShortfallHeadline(report) {
  if (!report || typeof report !== "object") {
    return "";
  }

  const requested = Number(report.requestedCount ?? 0);
  const generated = Number(report.generatedCount ?? 0);
  const stopReason = String(report.stopReason || "").toLowerCase();

  if (requested > generated && stopReason === "insufficient_grounded_chunks") {
    return `Only ${generated}/${requested} question(s) could be generated because the selected scope is too thin for grounded output.`;
  }

  if (requested > generated && stopReason === "requested_count_exceeds_grounded_capacity") {
    return `Only ${generated}/${requested} question(s) could be generated from the selected scope.`;
  }

  if (requested > generated) {
    return `Generated ${generated}/${requested} question(s).`;
  }

  return "";
}

export function GenerationResultPanel({
  result,
  showDraftActions = false,
  showConfirmAllAction = true,
  hideCorrectAnswer = false,
  onDeleteQuestion,
  onConfirmAll,
  isMutating = false
}) {
  const hasDraftQuestions = Array.isArray(result?.questions)
    ? result.questions.some((question) => question?.persistedQuestionId && String(question?.persistedStatus || "Draft").toLowerCase() === "draft")
    : false;

  if (!result) {
    return (
      <div className="ai-preview-block">
        Parsed result will appear here after generation completes.
      </div>
    );
  }

  return (
    <div className="ai-generation-result">
      <section className="ai-student-grid ai-student-grid--three">
        <div className="ai-meta-card">
          <span>Source Scope</span>
          <strong>{result.sourceScope || "-"}</strong>
        </div>
        <div className="ai-meta-card">
          <span>Generator Model</span>
          <strong>{result.generatorModel || "-"}</strong>
        </div>
        <div className="ai-meta-card">
          <span>Reviewer Model</span>
          <strong>{result.reviewerModel || "-"}</strong>
        </div>
      </section>

      <section className="ai-result-section">
        <h3>Run Summary</h3>
        <div className="ai-detail-list">
          <div><span>Subject</span><strong>{result.subject || "-"}</strong></div>
          <div><span>Question Type</span><strong>{result.questionType || "-"}</strong></div>
          <div><span>Difficulty Profile</span><strong>{formatDifficultyProfile(result.difficultyProfile || (result.difficulty ? [{ difficulty: result.difficulty, count: result.questions?.length || 0 }] : []))}</strong></div>
          <div><span>Question Count</span><strong>{result.questions?.length || 0}</strong></div>
        </div>
        {result.shortfallReport ? (
          <div className="ai-student-callout">
            <strong>{buildShortfallHeadline(result.shortfallReport) || "Generation shortfall detected."}</strong>
            {result.shortfallReport.summary ? <div>{result.shortfallReport.summary}</div> : null}
            {result.shortfallReport.suggestedActions?.length ? (
              <ul className="ai-bullet-list">
                {result.shortfallReport.suggestedActions.map((item) => <li key={item}>{item}</li>)}
              </ul>
            ) : null}
          </div>
        ) : null}
      </section>

      {result.questions?.length ? (
        <section className="ai-result-section">
          <h3>Questions</h3>
          <div className="ai-chapter-list">
            {result.questions.map((question, index) => (
              <article key={`${question.title}-${index}`} className="ai-chapter-card">
                <strong>{index + 1}. {question.title || "Untitled question"}</strong>
                <p>{question.description || "-"}</p>
                <p>Topics: {formatList(question.topicTags)}</p>
                {question.options?.length ? (
                  <div className="ai-preview-block">
                    <strong>Options</strong>
                    <div className="ai-option-list">
                      {formatQuestionOptions(question.options).map((option, optionIndex) => (
                        <div key={`${question.title}-option-${optionIndex}`}>{option}</div>
                      ))}
                    </div>
                    {!hideCorrectAnswer ? <div>Correct: {formatList(question.correctAnswer)}</div> : null}
                  </div>
                ) : null}
                {question.skeletonCode?.length ? (
                  <pre className="ai-code-block">{formatCodeFiles(question.skeletonCode)}</pre>
                ) : null}
                {question.testCases?.length ? (
                  <div className="ai-student-callout">
                    <strong>Test Cases</strong>
                    <ul className="ai-bullet-list">
                      {question.testCases.map((testCase, testIndex) => (
                        <li key={`${question.title}-tc-${testIndex}`}>
                          {testCase.description || `Test case ${testIndex + 1}`}
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {showDraftActions && question.persistedQuestionId && String(question.persistedStatus || "Draft").toLowerCase() === "draft" ? (
                  <div className="ai-inline-actions ai-inline-actions--footer">
                    <Button
                      variant="ghost"
                      onClick={() => onDeleteQuestion?.(question, index)}
                      disabled={isMutating}
                    >
                      Remove This Draft
                    </Button>
                  </div>
                ) : null}
              </article>
            ))}
          </div>
        </section>
      ) : null}

      {result.metrics ? (
        <section className="ai-result-section">
          <h3>Metrics</h3>
          <div className="ai-detail-list">
            <div><span>Charged</span><strong>{formatVnd(result.metrics.chargedVnd ?? 0)}</strong></div>
            <div><span>Deducted</span><strong>{formatVnd(result.metrics.actualDeductedVnd ?? 0)}</strong></div>
            <div><span>Remaining Wallet</span><strong>{formatVnd(result.metrics.remainingBalanceVnd ?? 0)}</strong></div>
          </div>
        </section>
      ) : null}

      {showDraftActions && showConfirmAllAction && hasDraftQuestions ? (
        <div className="ai-inline-actions ai-inline-actions--footer">
          <Button variant="primary" onClick={onConfirmAll} disabled={isMutating}>
            {isMutating ? "Processing..." : "Confirm Remaining Questions"}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
