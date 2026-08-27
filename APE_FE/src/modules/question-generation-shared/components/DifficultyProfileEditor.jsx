const DIFFICULTIES = ["Easy", "Medium", "Hard"];

function makeProfileRow(difficulty, count) {
  return {
    id: `${difficulty.toLowerCase()}-${Math.random().toString(36).slice(2, 8)}`,
    difficulty,
    count
  };
}

export function getDifficultyProfileLimits(questionType, bucketCount = 1) {
  if (questionType === "PE") {
    return { minTotal: 1, maxTotal: 10 };
  }

  return { minTotal: 10, maxTotal: 50 };
}

export function buildDefaultDifficultyProfile(questionType, difficulty = "Medium") {
  const { minTotal } = getDifficultyProfileLimits(questionType, 1);
  return [makeProfileRow(difficulty, minTotal)];
}

export function normalizeDifficultyProfileRows(rows = []) {
  return rows.map((row) => ({
    difficulty: row.difficulty,
    count: Number(row.count) || 0
  }));
}

export function getDifficultyProfileTotal(rows = []) {
  return normalizeDifficultyProfileRows(rows).reduce((sum, row) => sum + row.count, 0);
}

export function validateDifficultyProfile(rows = [], questionType) {
  const normalized = normalizeDifficultyProfileRows(rows);
  const filledRows = normalized.filter((row) => row.difficulty && row.count > 0);
  const bucketCount = filledRows.length;
  const total = getDifficultyProfileTotal(rows);
  const limits = getDifficultyProfileLimits(questionType, bucketCount || 1);
  const duplicates = new Set();

  for (const row of filledRows) {
    if (duplicates.has(row.difficulty)) {
      return `Duplicate difficulty \"${row.difficulty}\" is not allowed.`;
    }

    duplicates.add(row.difficulty);
  }

  if (!bucketCount || total <= 0) {
    return "Please choose at least one difficulty row with a positive count.";
  }

  if (bucketCount > 3) {
    return "You can request at most 3 difficulty buckets.";
  }

  if (total < limits.minTotal) {
    return `${questionType} generation requires at least ${limits.minTotal} question.`;
  }

  if (total > limits.maxTotal) {
    return `${questionType} generation must not exceed ${limits.maxTotal} questions in one run.`;
  }

  return "";
}

export function DifficultyProfileEditor({
  questionType,
  rows,
  onChange,
  disabled = false
}) {
  const normalizedRows = rows.length ? rows : buildDefaultDifficultyProfile(questionType);
  const total = getDifficultyProfileTotal(normalizedRows);
  const limits = getDifficultyProfileLimits(questionType, normalizedRows.length);
  const selectedDifficulties = normalizedRows.map((row) => row.difficulty);

  function patchRow(rowId, key, value) {
    onChange(normalizedRows.map((row) => (
      row.id === rowId ? { ...row, [key]: value } : row
    )));
  }

  function addRow() {
    if (normalizedRows.length >= 3) {
      return;
    }

    const nextDifficulty = DIFFICULTIES.find((item) => !selectedDifficulties.includes(item)) || DIFFICULTIES[0];
    onChange([
      ...normalizedRows,
      makeProfileRow(nextDifficulty, 1)
    ]);
  }

  function removeRow(rowId) {
    if (normalizedRows.length <= 1) {
      return;
    }

    onChange(normalizedRows.filter((row) => row.id !== rowId));
  }

  return (
    <div className="difficulty-profile-editor">
      <div className="difficulty-profile-editor__header">
        <div>
          <span>Difficulty profile</span>
          <strong>{total} total questions</strong>
        </div>
        <button
          type="button"
          className="ai-chip ai-chip--toggle"
          onClick={addRow}
          disabled={disabled || normalizedRows.length >= 3}
        >
          Add row
        </button>
      </div>

      <div className="difficulty-profile-editor__rows">
        {normalizedRows.map((row) => {
          const availableDifficulties = DIFFICULTIES.filter((item) => item === row.difficulty || !selectedDifficulties.includes(item));

          return (
            <div key={row.id} className="difficulty-profile-editor__row">
              <label className="student-filter-field">
                <span>Difficulty</span>
                <select
                  value={row.difficulty}
                  onChange={(event) => patchRow(row.id, "difficulty", event.target.value)}
                  disabled={disabled}
                >
                  {availableDifficulties.map((item) => (
                    <option key={item} value={item}>{item}</option>
                  ))}
                </select>
              </label>

              <label className="student-filter-field">
                <span>Count</span>
                <input
                  type="number"
                  min={1}
                  max={limits.maxTotal}
                  value={row.count}
                  onChange={(event) => patchRow(row.id, "count", event.target.value)}
                  disabled={disabled}
                />
              </label>

              <button
                type="button"
                className="difficulty-profile-editor__remove"
                onClick={() => removeRow(row.id)}
                disabled={disabled || normalizedRows.length <= 1}
                aria-label={`Remove ${row.difficulty} row`}
              >
                Remove
              </button>
            </div>
          );
        })}
      </div>

      <p className="ai-row-subcopy">
        {questionType} {normalizedRows.length > 1 ? "mixed" : "single"} mode. Total allowed per run: up to {limits.maxTotal}.
      </p>
    </div>
  );
}
