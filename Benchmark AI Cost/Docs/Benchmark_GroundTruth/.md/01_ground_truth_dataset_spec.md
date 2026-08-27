# 01 - Ground Truth Dataset Specification

## 1) Objective
Create a stable and scientific dataset to benchmark AI feature quality, cost, and latency across model versions.

## 2) Design Principles
| Principle | Description | Requirement |
|---|---|---|
| Fixed benchmark set | Same core test set reused for all model/prompt versions | Mandatory |
| Coverage | Include easy/medium/hard + edge/ambiguous cases | Mandatory |
| Diversity | Vary wording, context, noise, and format | Mandatory |
| Scope alignment | Questions map to each AI feature's responsibility | Mandatory |
| Measurable outputs | Every case has expected answer/range or rubric anchors | Mandatory |
| Reproducibility | Keep metadata, run config, prompt version | Mandatory |

## 3) Feature Coverage Matrix
| Feature | Domain | Suggested Cases | Easy | Medium | Hard | Ambiguous/Noisy |
|---|---|---:|---:|---:|---:|---:|
| AI_Gatekeeper | Topic support classification | 120 | 30 | 40 | 30 | 20 |
| AI_Embedding (retrieval eval) | C/Java/DSA corpus retrieval | 150 queries | 40 | 60 | 30 | 20 |
| MultiAgent_1_Gen | FE/PE question generation | 90 | 20 | 40 | 30 | - |
| MultiAgent_2_Review | Quality review/regenerate decision | 90 | 20 | 40 | 30 | - |
| AI_Code_Mentor | Code diagnosis and fix hints | 120 | 30 | 50 | 40 | - |

## 4) Question Type Distribution
| Type | Example | Target Ratio |
|---|---|---:|
| Basic concept | "What is pointer arithmetic in C?" | 20% |
| Rephrased equivalent | Same question, different phrasing style | 20% |
| Missing constraints | Incomplete prompt requiring assumptions | 15% |
| Ambiguous wording | Intentionally vague or mixed terminology | 15% |
| Synthesis and reasoning | Multi-step analysis, compare alternatives | 30% |

## 5) Metadata Template (per test case)
| Field | Description |
|---|---|
| case_id | Unique ID (`GK_C_0001`) |
| feature | Gatekeeper/Embedding/Gen/Review/Mentor |
| domain | C / Java OOP / DSA OOP |
| difficulty | easy/medium/hard |
| input | Full test input/prompt/context |
| expected_output | Exact or constrained expected output |
| scoring_anchor | Rules for scoring this case |
| notes | Edge-case rationale |

## 6) Benchmark Stability Rules
1. Never remove old cases already used in published comparisons.
2. Add new cases in a new dataset version (`dataset_v1`, `dataset_v2`).
3. Keep a frozen `core_set` for thesis charts and conclusion.

## 7) Recommended Initial Dataset Version
- Dataset version: `dataset_v1`
- Total core cases: 570 (across all features)
- Language focus ratio:
  - C intro: 35%
  - Java OOP: 35%
  - DSA on OOP: 30%
