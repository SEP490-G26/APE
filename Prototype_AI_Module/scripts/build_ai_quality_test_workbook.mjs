import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const allModels = [
  "GPT 4o",
  "GPT 4o mini",
  "GPT 5.4",
  "GPT 5.4 mini",
  "GPT 5.5",
  "Gemini 2.5",
  "Gemini 2.5 Flash",
  "Gemini 2.5 Pro",
  "Gemini 3.1 Flash Lite",
  "Gemini 3.5 Flash",
];

const extractModels = [...allModels];
const taggingModels = [
  "GPT 4o",
  "GPT 4o mini",
  "GPT 5.4 mini",
  "Gemini 2.5",
  "Gemini 2.5 Flash",
  "Gemini 2.5 Pro",
  "Gemini 3.1 Flash Lite",
  "Gemini 3.5 Flash",
];

const codeMentorModels = [...allModels];
const embeddingModel = "Cohere Multilingual v3.0";

const scoringRows = [
  ["Input Quality Benchmark", "ocr_text_accuracy_score", "Do chinh xac text sau OCR hoac parsing", "0-1", 0.2, 0.75, ""],
  ["Input Quality Benchmark", "content_cleanliness_score", "Muc do loai bo ky tu rac khoang trang va noise", "0-1", 0.2, 0.75, ""],
  ["Input Quality Benchmark", "chunk_boundary_score", "Muc do chia dung cum noi dung va khong cat vo nghia", "0-1", 0.2, 0.75, ""],
  ["Input Quality Benchmark", "tag_relevance_score", "Tag co bam sat noi dung chunk hay khong", "0-1", 0.15, 0.75, ""],
  ["Input Quality Benchmark", "tag_coverage_score", "Muc do bao phu topic quan trong cua tai lieu", "0-1", 0.1, 0.7, ""],
  ["Input Quality Benchmark", "image_description_quality_score", "Chat luong mo ta anh neu co vision", "0-1", 0.05, 0.6, ""],
  ["Input Quality Benchmark", "grounding_traceability_score", "Co truy vet duoc nguon chunk/page/segment hay khong", "0-1", 0.1, 0.8, ""],
  ["Question Generation Benchmark", "schema_valid_rate", "Ty le cau hoi dung schema dich", "0-1", 0.15, 1, ""],
  ["Question Generation Benchmark", "grounded_rate", "Ty le cau hoi bam sat context nguon", "0-1", 0.25, 0.8, ""],
  ["Question Generation Benchmark", "difficulty_alignment_score", "Do khop muc do de kho voi target", "0-1", 0.15, 0.75, ""],
  ["Question Generation Benchmark", "topic_relevance_score", "Do bam sat topic duoc yeu cau", "0-1", 0.15, 0.8, ""],
  ["Question Generation Benchmark", "question_clarity_score", "Muc do ro rang tu than cua cau hoi", "0-1", 0.1, 0.75, ""],
  ["Question Generation Benchmark", "question_usefulness_score", "Muc do co gia tri hoc thuat va su pham", "0-1", 0.1, 0.75, ""],
  ["Question Generation Benchmark", "distractor_quality_score", "Chi ap dung FE - chat luong phuong an nhieu lua chon", "0-1", 0.07, 0.7, "FE only"],
  ["Question Generation Benchmark", "testcase_quality_score", "Chi ap dung PE - chat luong test case danh gia", "0-1", 0.09, 0.7, "PE only"],
  ["Question Generation Benchmark", "rule_based_pass_rate", "Ty le vuot cac luat rule-based reviewer", "0-1", 0.1, 0.8, ""],
  ["Code Mentor Benchmark", "bug_detection_score", "Do dung khi phat hien loi sai", "0-1", 0.2, 0.75, ""],
  ["Code Mentor Benchmark", "correctness_feedback_score", "Do dung cua nhan xet ve tinh dung sai cua code", "0-1", 0.2, 0.75, ""],
  ["Code Mentor Benchmark", "code_quality_feedback_score", "Chat luong nhan xet ve style cau truc va code smell", "0-1", 0.15, 0.7, ""],
  ["Code Mentor Benchmark", "actionability_score", "Muc do de xuat sua loi co the lam theo ngay", "0-1", 0.15, 0.75, ""],
  ["Code Mentor Benchmark", "specificity_score", "Muc do cu the thay vi nhan xet mo ho", "0-1", 0.1, 0.7, ""],
  ["Code Mentor Benchmark", "hallucination_penalty", "Muc phat khi nhan xet sai khong bam vao code", "0-1", -1, 0, "Subtract from final score"],
  ["Code Mentor Benchmark", "grounded_to_code_score", "Muc do nhan xet bam vao source code va context de bai", "0-1", 0.1, 0.8, ""],
  ["Code Mentor Benchmark", "pedagogical_value_score", "Gia tri huong dan hoc tap cho sinh vien", "0-1", 0.1, 0.75, ""],
];

function pad(num, size = 3) {
  return String(num).padStart(size, "0");
}

function headerStyle(range, fill = "#1F4E78") {
  range.format = {
    fill,
    font: { bold: true, color: "#FFFFFF", size: 11 },
    wrapText: true,
    horizontalAlignment: "Center",
    verticalAlignment: "Center",
    borders: { preset: "all", style: "thin", color: "#D9E2F3" },
  };
}

function applyGrid(range) {
  range.format.borders = { preset: "all", style: "thin", color: "#D9D9D9" };
  range.format.verticalAlignment = "Center";
}

function setColumnWidths(sheet, widths) {
  widths.forEach((width, index) => {
    sheet.getRangeByIndexes(0, index, 1, 1).format.columnWidth = width;
  });
}

function buildExtractionRows() {
  const rows = [];
  let index = 1;
  for (const extractModel of extractModels) {
    for (const taggingModel of taggingModels) {
      rows.push([
        `EXT-${pad(index)}`,
        "",
        "",
        extractModel,
        taggingModel,
        embeddingModel,
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
      ]);
      index += 1;
    }
  }
  return rows;
}

function buildGenerationRows() {
  const rows = [];
  let index = 1;
  for (const model of allModels) {
    rows.push([
      `GEN-${pad(index)}`,
      "SameModelDualRole",
      "",
      "",
      "",
      model,
      model,
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
      "",
    ]);
    index += 1;
  }
  for (const generatorModel of allModels) {
    for (const reviewerModel of allModels) {
      if (generatorModel === reviewerModel) continue;
      rows.push([
        `GEN-${pad(index)}`,
        "DualAgent",
        "",
        "",
        "",
        generatorModel,
        reviewerModel,
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
      ]);
      index += 1;
    }
  }
  return rows;
}

function buildMentorRows() {
  return codeMentorModels.map((model, index) => [
    `MENT-${pad(index + 1)}`,
    "",
    "",
    "",
    model,
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
    "",
  ]);
}

function extractionFormula(row) {
  return `=IF(COUNTA(G${row}:M${row})=0,"",ROUND(G${row}*'Scoring Dictionary'!$E$2+H${row}*'Scoring Dictionary'!$E$3+I${row}*'Scoring Dictionary'!$E$4+J${row}*'Scoring Dictionary'!$E$5+K${row}*'Scoring Dictionary'!$E$6+L${row}*'Scoring Dictionary'!$E$7+M${row}*'Scoring Dictionary'!$E$8,3))`;
}

function extractionPassFormula(row) {
  return `=IF(N${row}="","",IF(AND(N${row}>=0.8,G${row}>='Scoring Dictionary'!$F$2,I${row}>='Scoring Dictionary'!$F$4,J${row}>='Scoring Dictionary'!$F$5),"PASS","FAIL"))`;
}

function generationFormula(row) {
  return `=IF(COUNTA(H${row}:P${row})=0,"",ROUND(IF(D${row}="FE",H${row}*0.15+I${row}*0.23+J${row}*0.15+K${row}*0.15+L${row}*0.10+M${row}*0.10+N${row}*0.07+P${row}*0.05,IF(D${row}="PE",H${row}*0.15+I${row}*0.23+J${row}*0.15+K${row}*0.15+L${row}*0.08+M${row}*0.10+O${row}*0.09+P${row}*0.05,H${row}*'Scoring Dictionary'!$E$9+I${row}*'Scoring Dictionary'!$E$10+J${row}*'Scoring Dictionary'!$E$11+K${row}*'Scoring Dictionary'!$E$12+L${row}*'Scoring Dictionary'!$E$13+M${row}*'Scoring Dictionary'!$E$14+P${row}*'Scoring Dictionary'!$E$17)),3))`;
}

function generationPassFormula(row) {
  return `=IF(Q${row}="","",IF(AND(Q${row}>=0.8,H${row}>='Scoring Dictionary'!$F$9,I${row}>='Scoring Dictionary'!$F$10,P${row}>='Scoring Dictionary'!$F$17),"PASS","FAIL"))`;
}

function mentorRawFormula(row) {
  return `=IF(COUNTA(F${row}:M${row})=0,"",ROUND(F${row}*'Scoring Dictionary'!$E$18+G${row}*'Scoring Dictionary'!$E$19+H${row}*'Scoring Dictionary'!$E$20+I${row}*'Scoring Dictionary'!$E$21+J${row}*'Scoring Dictionary'!$E$22+L${row}*'Scoring Dictionary'!$E$24+M${row}*'Scoring Dictionary'!$E$25,3))`;
}

function mentorAdjustedFormula(row) {
  return `=IF(N${row}="","",ROUND(MAX(0,N${row}-K${row}),3))`;
}

function mentorPassFormula(row) {
  return `=IF(O${row}="","",IF(AND(O${row}>=0.8,I${row}>='Scoring Dictionary'!$F$21,L${row}>='Scoring Dictionary'!$F$24),"PASS","FAIL"))`;
}

function styleScoreColumns(sheet, ranges) {
  for (const rangeRef of ranges) {
    const range = sheet.getRange(rangeRef);
    range.format.numberFormat = "0.00";
    range.conditionalFormats.add("cellIs", {
      operator: "greaterThanOrEqual",
      formula: 0.8,
      format: { fill: "#E2F0D9", font: { color: "#215E21", bold: true } },
    });
    range.conditionalFormats.add("cellIs", {
      operator: "between",
      formula: [0.5, 0.7999],
      format: { fill: "#FFF2CC", font: { color: "#7F6000" } },
    });
    range.conditionalFormats.add("cellIs", {
      operator: "lessThan",
      formula: 0.5,
      format: { fill: "#FCE4D6", font: { color: "#9C0006" } },
    });
  }
}

function styleResultColumn(sheet, rangeRef) {
  const range = sheet.getRange(rangeRef);
  range.format.numberFormat = "0.000;-0.000;;";
  range.conditionalFormats.add("cellIs", {
    operator: "greaterThanOrEqual",
    formula: 0.8,
    format: { fill: "#E2F0D9", font: { color: "#215E21", bold: true } },
  });
  range.conditionalFormats.add("cellIs", {
    operator: "between",
    formula: [0.0001, 0.7999],
    format: { fill: "#FCE4D6", font: { color: "#9C0006", bold: true } },
  });
}

function stylePassColumn(range) {
  range.conditionalFormats.add("containsText", {
    text: "PASS",
    format: { fill: "#E2F0D9", font: { color: "#215E21", bold: true } },
  });
  range.conditionalFormats.add("containsText", {
    text: "FAIL",
    format: { fill: "#F4CCCC", font: { color: "#990000", bold: true } },
  });
}

function createInstructionsSheet(workbook) {
  const sheet = workbook.worksheets.add("Instructions");
  sheet.showGridLines = false;
  sheet.getRange("A1:H1").merge();
  sheet.getRange("A1").values = [["AI Quality Test Workbook"]];
  sheet.getRange("A1").format = {
    fill: "#163A5F",
    font: { bold: true, color: "#FFFFFF", size: 16 },
    horizontalAlignment: "Center",
    verticalAlignment: "Center",
  };
  sheet.getRange("A3:B9").values = [
    ["Sheet", "Purpose"],
    ["Extraction Review Embedding", "Cham tay cho 80 to hop extraction model x autotagging model, embedding model co dinh Cohere Multilingual v3.0."],
    ["Generation Review", "Cham tay cho 100 case generation-review, da prefill du same model dual role va dual agent."],
    ["Code Mentor", "Cham tay cho 10 model code mentor."],
    ["Summary", "So sanh diem trung binh va ty le pass theo model."],
    ["Scoring Dictionary", "Nguon trong so va nguong pass_threshold dung de tinh diem."],
    ["Lists", "Danh sach model, loai cau hoi, difficulty, trang thai. Sheet nay de hidden."],
  ];
  headerStyle(sheet.getRange("A3:B3"), "#1F4E78");
  applyGrid(sheet.getRange("A4:B9"));
  sheet.getRange("D3:H9").values = [
    ["Quick Use", "", "", "", ""],
    ["1", "Chay test", "Moi dong la 1 case test.", "", ""],
    ["2", "Nhap diem tay", "Chi nhap diem 0 -> 1 o cac cot score.", "", ""],
    ["3", "Nhap metadata", "Dien doc case, subject, difficulty, tester, note neu can.", "", ""],
    ["4", "Xem ket qua", "Cot overall/final score va pass_fail da tu tinh.", "", ""],
    ["5", "So model", "Xem sheet Summary de so sanh trung binh theo model.", "", ""],
  ];
  sheet.getRange("D3:H3").merge();
  headerStyle(sheet.getRange("D3:H3"), "#548235");
  applyGrid(sheet.getRange("D4:H9"));
  sheet.getRange("A11:H15").values = [
    ["Legend", "", "", "", "", "", "", ""],
    ["Nhap tay", "Cac cot score, doc_case, question_type, difficulty, tester, note.", "", "", "", "", "", ""],
    ["Cong thuc", "Cac cot overall_quality_score, final_quality_score, adjusted_quality_score, pass_fail.", "", "", "", "", "", ""],
    ["Score range", "0 = fail ro rang, 0.5 = tam chap nhan, 1 = tot.", "", "", "", "", "", ""],
    ["Note", "Question type FE/PE can duoc chon de cong thuc generation ap dung dung metric distractor/testcase.", "", "", "", "", "", ""],
  ];
  sheet.getRange("A11:H11").merge();
  headerStyle(sheet.getRange("A11:H11"), "#7F6000");
  applyGrid(sheet.getRange("A12:H15"));
  sheet.freezePanes.freezeRows(2);
  setColumnWidths(sheet, [24, 90, 14, 16, 16, 16, 16, 16]);
}

function createScoringSheet(workbook) {
  const sheet = workbook.worksheets.add("Scoring Dictionary");
  const headers = [["sheet_name", "metric_name", "metric_definition", "score_range", "weight", "pass_threshold", "notes"]];
  sheet.getRange(`A1:G${scoringRows.length + 1}`).values = [...headers, ...scoringRows];
  headerStyle(sheet.getRange("A1:G1"), "#7030A0");
  applyGrid(sheet.getRange(`A2:G${scoringRows.length + 1}`));
  sheet.getRange(`E2:F${scoringRows.length + 1}`).format.numberFormat = "0.00";
  sheet.freezePanes.freezeRows(1);
  setColumnWidths(sheet, [28, 30, 44, 12, 12, 14, 28]);
}

function createListsSheet(workbook) {
  const sheet = workbook.worksheets.add("Lists");
  sheet.getRange("A1:D1").values = [["All Models", "Tagging Models", "Question Types", "Difficulties"]];
  headerStyle(sheet.getRange("A1:D1"), "#5B9BD5");
  const maxRows = Math.max(allModels.length, taggingModels.length, 3, 3);
  const rows = [];
  for (let i = 0; i < maxRows; i += 1) {
    rows.push([
      allModels[i] || "",
      taggingModels[i] || "",
      ["FE", "PE", ""][i] || "",
      ["Easy", "Medium", "Hard"][i] || "",
    ]);
  }
  sheet.getRange(`A2:D${maxRows + 1}`).values = rows;
  applyGrid(sheet.getRange(`A2:D${maxRows + 1}`));
  setColumnWidths(sheet, [24, 24, 16, 16]);
  sheet.visibility = "Hidden";
}

function createExtractionSheet(workbook) {
  const sheet = workbook.worksheets.add("Extraction Review Embedding");
  const headers = [[
    "test_id",
    "doc_case",
    "doc_type",
    "extraction_model",
    "tagging_model",
    "embedding_model",
    "ocr_text_accuracy_score",
    "content_cleanliness_score",
    "chunk_boundary_score",
    "tag_relevance_score",
    "tag_coverage_score",
    "image_description_quality_score",
    "grounding_traceability_score",
    "overall_quality_score",
    "pass_fail",
    "tester",
    "test_date",
    "notes",
    "priority_review",
  ]];
  const rows = buildExtractionRows();
  const endRow = rows.length + 1;
  sheet.getRange(`A1:S${endRow}`).values = [...headers, ...rows];
  const formulaRows = [];
  for (let row = 2; row <= endRow; row += 1) {
    formulaRows.push([extractionFormula(row), extractionPassFormula(row)]);
  }
  sheet.getRange(`N2:O${endRow}`).formulas = formulaRows;
  headerStyle(sheet.getRange("A1:S1"), "#1F4E78");
  applyGrid(sheet.getRange(`A2:S${endRow}`));
  sheet.getRange(`G2:M${endRow}`).format.numberFormat = "0.00";
  sheet.getRange(`Q2:Q${endRow}`).format.numberFormat = "yyyy-mm-dd";
  sheet.getRange(`B2:B${endRow}`).dataValidation = { rule: { type: "list", values: ["Case 01", "Case 02", "Case 03", "Case 04", "Case 05", "Case 06"] } };
  sheet.getRange(`C2:C${endRow}`).dataValidation = { rule: { type: "list", values: ["PDF", "DOCX", "PPTX", "Image", "Mixed"] } };
  sheet.getRange(`S2:S${endRow}`).dataValidation = { rule: { type: "list", values: ["High", "Medium", "Low"] } };
  styleResultColumn(sheet, `N2:N${endRow}`);
  stylePassColumn(sheet.getRange(`O2:O${endRow}`));
  sheet.freezePanes.freezeRows(1);
  sheet.freezePanes.freezeColumns(6);
  setColumnWidths(sheet, [12, 14, 14, 22, 22, 24, 14, 14, 14, 14, 14, 16, 16, 14, 12, 16, 14, 36, 14]);
}

function createGenerationSheet(workbook) {
  const sheet = workbook.worksheets.add("Generation Review");
  const headers = [[
    "test_id",
    "scenario_type",
    "subject_code",
    "question_type",
    "difficulty",
    "generator_model",
    "reviewer_model",
    "schema_valid_rate",
    "grounded_rate",
    "difficulty_alignment_score",
    "topic_relevance_score",
    "question_clarity_score",
    "question_usefulness_score",
    "distractor_quality_score",
    "testcase_quality_score",
    "rule_based_pass_rate",
    "final_quality_score",
    "pass_fail",
    "tester",
    "test_date",
    "notes",
  ]];
  const rows = buildGenerationRows();
  const endRow = rows.length + 1;
  sheet.getRange(`A1:U${endRow}`).values = [...headers, ...rows];
  const formulas = [];
  for (let row = 2; row <= endRow; row += 1) {
    formulas.push([generationFormula(row), generationPassFormula(row)]);
  }
  sheet.getRange(`Q2:R${endRow}`).formulas = formulas;
  headerStyle(sheet.getRange("A1:U1"), "#8B5E34");
  applyGrid(sheet.getRange(`A2:U${endRow}`));
  sheet.getRange(`H2:P${endRow}`).format.numberFormat = "0.00";
  sheet.getRange(`T2:T${endRow}`).format.numberFormat = "yyyy-mm-dd";
  sheet.getRange(`C2:C${endRow}`).dataValidation = { rule: { type: "list", values: ["C", "JAVA_OOP", "DSA_JAVA", "Other"] } };
  sheet.getRange(`D2:D${endRow}`).dataValidation = { rule: { type: "list", formula1: "='Lists'!$C$2:$C$3" } };
  sheet.getRange(`E2:E${endRow}`).dataValidation = { rule: { type: "list", formula1: "='Lists'!$D$2:$D$4" } };
  styleResultColumn(sheet, `Q2:Q${endRow}`);
  stylePassColumn(sheet.getRange(`R2:R${endRow}`));
  sheet.freezePanes.freezeRows(1);
  sheet.freezePanes.freezeColumns(7);
  setColumnWidths(sheet, [12, 20, 14, 14, 14, 22, 22, 12, 12, 14, 14, 14, 14, 14, 14, 14, 14, 12, 16, 14, 36]);
}

function createMentorSheet(workbook) {
  const sheet = workbook.worksheets.add("Code Mentor");
  const headers = [[
    "test_id",
    "submission_case",
    "subject_code",
    "language",
    "mentor_model",
    "bug_detection_score",
    "correctness_feedback_score",
    "code_quality_feedback_score",
    "actionability_score",
    "specificity_score",
    "hallucination_penalty",
    "grounded_to_code_score",
    "pedagogical_value_score",
    "overall_quality_score",
    "adjusted_quality_score",
    "pass_fail",
    "tester",
    "test_date",
    "notes",
  ]];
  const rows = buildMentorRows();
  const endRow = rows.length + 1;
  sheet.getRange(`A1:S${endRow}`).values = [...headers, ...rows];
  const formulas = [];
  for (let row = 2; row <= endRow; row += 1) {
    formulas.push([mentorRawFormula(row), mentorAdjustedFormula(row), mentorPassFormula(row)]);
  }
  sheet.getRange(`N2:P${endRow}`).formulas = formulas;
  headerStyle(sheet.getRange("A1:S1"), "#5D4383");
  applyGrid(sheet.getRange(`A2:S${endRow}`));
  sheet.getRange(`F2:M${endRow}`).format.numberFormat = "0.00";
  sheet.getRange(`R2:R${endRow}`).format.numberFormat = "yyyy-mm-dd";
  sheet.getRange(`B2:B${endRow}`).dataValidation = { rule: { type: "list", values: ["Submission 01", "Submission 02", "Submission 03", "Submission 04", "Submission 05", "Submission 06"] } };
  sheet.getRange(`C2:C${endRow}`).dataValidation = { rule: { type: "list", values: ["C", "JAVA_OOP", "DSA_JAVA", "Other"] } };
  sheet.getRange(`D2:D${endRow}`).dataValidation = { rule: { type: "list", values: ["C", "Java", "Python", "C#", "Other"] } };
  styleResultColumn(sheet, `N2:O${endRow}`);
  stylePassColumn(sheet.getRange(`P2:P${endRow}`));
  sheet.freezePanes.freezeRows(1);
  sheet.freezePanes.freezeColumns(5);
  setColumnWidths(sheet, [12, 16, 14, 14, 22, 14, 14, 14, 14, 12, 14, 14, 14, 14, 14, 12, 16, 14, 36]);
}

function createSummarySheet(workbook) {
  const sheet = workbook.worksheets.add("Summary");
  sheet.showGridLines = false;
  sheet.getRange("A1:H1").merge();
  sheet.getRange("A1").values = [["AI Quality Comparison Summary"]];
  sheet.getRange("A1").format = {
    fill: "#203864",
    font: { color: "#FFFFFF", bold: true, size: 15 },
    horizontalAlignment: "Center",
  };

  sheet.getRange("A3:D3").values = [["Extraction Model", "Avg Score", "Pass Count", "Cases"]];
  headerStyle(sheet.getRange("A3:D3"), "#1F4E78");
  const extractionRows = allModels.map((model, idx) => [
    model,
    `=IFERROR(ROUND(AVERAGEIFS('Extraction Review Embedding'!$N$2:$N$81,'Extraction Review Embedding'!$D$2:$D$81,A${idx + 4},'Extraction Review Embedding'!$N$2:$N$81,">0"),3),"")`,
    `=COUNTIFS('Extraction Review Embedding'!$D$2:$D$81,A${idx + 4},'Extraction Review Embedding'!$O$2:$O$81,"PASS")`,
    `=COUNTIF('Extraction Review Embedding'!$D$2:$D$81,A${idx + 4})`,
  ]);
  sheet.getRange(`A4:D${allModels.length + 3}`).values = extractionRows.map((row) => [row[0], "", "", ""]);
  sheet.getRange(`B4:D${allModels.length + 3}`).formulas = extractionRows.map((row) => row.slice(1));
  applyGrid(sheet.getRange(`A4:D${allModels.length + 3}`));

  sheet.getRange("F3:I3").values = [["Tagging Model", "Avg Score", "Pass Count", "Cases"]];
  headerStyle(sheet.getRange("F3:I3"), "#4E7C67");
  const taggingSummaryRows = taggingModels.map((model, idx) => [
    model,
    `=IFERROR(ROUND(AVERAGEIFS('Extraction Review Embedding'!$N$2:$N$81,'Extraction Review Embedding'!$E$2:$E$81,F${idx + 4},'Extraction Review Embedding'!$N$2:$N$81,">0"),3),"")`,
    `=COUNTIFS('Extraction Review Embedding'!$E$2:$E$81,F${idx + 4},'Extraction Review Embedding'!$O$2:$O$81,"PASS")`,
    `=COUNTIF('Extraction Review Embedding'!$E$2:$E$81,F${idx + 4})`,
  ]);
  sheet.getRange(`F4:I${taggingModels.length + 3}`).values = taggingSummaryRows.map((row) => [row[0], "", "", ""]);
  sheet.getRange(`G4:I${taggingModels.length + 3}`).formulas = taggingSummaryRows.map((row) => row.slice(1));
  applyGrid(sheet.getRange(`F4:I${taggingModels.length + 3}`));

  sheet.getRange("A16:D16").values = [["Generator Model", "Avg Score", "Pass Count", "Cases"]];
  headerStyle(sheet.getRange("A16:D16"), "#8B5E34");
  const genStart = 17;
  const generationRows = allModels.map((model, idx) => [
    model,
    `=IFERROR(ROUND(AVERAGEIFS('Generation Review'!$Q$2:$Q$101,'Generation Review'!$F$2:$F$101,A${genStart + idx},'Generation Review'!$Q$2:$Q$101,">0"),3),"")`,
    `=COUNTIFS('Generation Review'!$F$2:$F$101,A${genStart + idx},'Generation Review'!$R$2:$R$101,"PASS")`,
    `=COUNTIF('Generation Review'!$F$2:$F$101,A${genStart + idx})`,
  ]);
  sheet.getRange(`A${genStart}:D${genStart + allModels.length - 1}`).values = generationRows.map((row) => [row[0], "", "", ""]);
  sheet.getRange(`B${genStart}:D${genStart + allModels.length - 1}`).formulas = generationRows.map((row) => row.slice(1));
  applyGrid(sheet.getRange(`A${genStart}:D${genStart + allModels.length - 1}`));

  sheet.getRange("F16:I16").values = [["Reviewer Model", "Avg Score", "Pass Count", "Cases"]];
  headerStyle(sheet.getRange("F16:I16"), "#A56A43");
  const reviewerRows = allModels.map((model, idx) => [
    model,
    `=IFERROR(ROUND(AVERAGEIFS('Generation Review'!$Q$2:$Q$101,'Generation Review'!$G$2:$G$101,F${genStart + idx},'Generation Review'!$Q$2:$Q$101,">0"),3),"")`,
    `=COUNTIFS('Generation Review'!$G$2:$G$101,F${genStart + idx},'Generation Review'!$R$2:$R$101,"PASS")`,
    `=COUNTIF('Generation Review'!$G$2:$G$101,F${genStart + idx})`,
  ]);
  sheet.getRange(`F${genStart}:I${genStart + allModels.length - 1}`).values = reviewerRows.map((row) => [row[0], "", "", ""]);
  sheet.getRange(`G${genStart}:I${genStart + allModels.length - 1}`).formulas = reviewerRows.map((row) => row.slice(1));
  applyGrid(sheet.getRange(`F${genStart}:I${genStart + allModels.length - 1}`));

  const mentorTop = 30;
  sheet.getRange(`A${mentorTop}:D${mentorTop}`).values = [["Code Mentor Model", "Avg Adjusted Score", "Pass Count", "Cases"]];
  headerStyle(sheet.getRange(`A${mentorTop}:D${mentorTop}`), "#5D4383");
  const mentorRows = allModels.map((model, idx) => [
    model,
    `=IFERROR(ROUND(AVERAGEIFS('Code Mentor'!$O$2:$O$11,'Code Mentor'!$E$2:$E$11,A${mentorTop + 1 + idx},'Code Mentor'!$O$2:$O$11,">0"),3),"")`,
    `=COUNTIFS('Code Mentor'!$E$2:$E$11,A${mentorTop + 1 + idx},'Code Mentor'!$P$2:$P$11,"PASS")`,
    `=COUNTIF('Code Mentor'!$E$2:$E$11,A${mentorTop + 1 + idx})`,
  ]);
  sheet.getRange(`A${mentorTop + 1}:D${mentorTop + allModels.length}`).values = mentorRows.map((row) => [row[0], "", "", ""]);
  sheet.getRange(`B${mentorTop + 1}:D${mentorTop + allModels.length}`).formulas = mentorRows.map((row) => row.slice(1));
  applyGrid(sheet.getRange(`A${mentorTop + 1}:D${mentorTop + allModels.length}`));

  sheet.getRange("K3:N8").values = [
    ["Control", "Value", "", ""],
    ["Total Extraction Cases", 80, "", ""],
    ["Total Generation Cases", 100, "", ""],
    ["Total Code Mentor Cases", 10, "", ""],
    ["Embedding Model", embeddingModel, "", ""],
    ["Goal", "Nhap diem tay, workbook tu tinh tong diem va pass_fail", "", ""],
  ];
  headerStyle(sheet.getRange("K3:N3"), "#548235");
  applyGrid(sheet.getRange("K4:N8"));

  styleResultColumn(sheet, "B4:B13");
  styleResultColumn(sheet, "G4:G11");
  styleResultColumn(sheet, `B${genStart}:B${genStart + 9}`);
  styleResultColumn(sheet, `G${genStart}:G${genStart + 9}`);
  styleResultColumn(sheet, `B${mentorTop + 1}:B${mentorTop + 10}`);
  setColumnWidths(sheet, [24, 14, 12, 10, 4, 24, 14, 12, 10, 4, 18, 30, 8, 8]);
}

export async function buildWorkbook({ outputPath }) {
  const workbook = Workbook.create();
  createInstructionsSheet(workbook);
  createScoringSheet(workbook);
  createListsSheet(workbook);
  createExtractionSheet(workbook);
  createGenerationSheet(workbook);
  createMentorSheet(workbook);
  createSummarySheet(workbook);

  const outputDir = path.dirname(outputPath);
  await fs.mkdir(outputDir, { recursive: true });
  const exported = await SpreadsheetFile.exportXlsx(workbook);
  await exported.save(outputPath);

  return { outputPath };
}
