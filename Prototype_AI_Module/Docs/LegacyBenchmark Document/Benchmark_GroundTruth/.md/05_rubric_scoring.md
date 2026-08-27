# 05 - Rubric Scoring (Unified)

## 1) Scoring Scale
- 0 = fail
- 1 = weak
- 2 = acceptable
- 3 = good
- 4 = very good
- 5 = excellent

## 2) Feature Rubric Weights

### 2.1 AI_Gatekeeper
| Criterion | Weight | Description |
|---|---:|---|
| Classification correctness | 50% | `is_supported` correct |
| Domain label accuracy | 20% | C/Java_OOP/DSA_OOP correct when supported |
| Reason quality | 20% | rationale grounded and concise |
| Calibration | 10% | confidence aligns with certainty |

### 2.2 AI_Embedding (retrieval eval)
| Criterion | Weight | Description |
|---|---:|---|
| Recall@k / Hit@k | 40% | retrieval relevance |
| MRR / ranking quality | 25% | correct ordering |
| Latency | 15% | response time |
| Cost efficiency | 20% | token cost per useful retrieval |

### 2.3 MultiAgent_1_Gen
| Criterion | Weight | Description |
|---|---:|---|
| Factual correctness | 35% | answer/explanation correctness |
| Schema validity | 15% | output JSON validity |
| Difficulty alignment | 20% | easy/medium/hard fit |
| Pedagogical quality | 15% | clarity, teachability |
| Diversity/non-duplication | 15% | avoids repetitive patterns |

### 2.4 MultiAgent_2_Review
| Criterion | Weight | Description |
|---|---:|---|
| Defect detection accuracy | 40% | catches real issues |
| False alarm control | 20% | avoids over-flagging |
| Regenerate decision quality | 25% | approve vs regenerate correctness |
| Review explanation quality | 15% | clear and actionable |

### 2.5 AI_Code_Mentor
| Criterion | Weight | Description |
|---|---:|---|
| Bug diagnosis accuracy | 35% | core issue detected |
| Fix guidance usefulness | 25% | practical next steps |
| Complexity analysis correctness | 20% | right asymptotic level |
| Clarity and student-friendliness | 10% | understandable feedback |
| Policy compliance | 10% | no prohibited leakage |

## 3) System Metrics (non-quality)
| Metric | Formula |
|---|---|
| avg_latency_ms | mean(latency_ms) |
| p95_latency_ms | p95(latency_ms) |
| avg_input_tokens | mean(input_tokens) |
| avg_output_tokens | mean(output_tokens) |
| avg_cost_usd | mean(cost_usd) |
| success_rate | success_runs / total_runs |

## 4) Pass Thresholds (initial recommendation)
| Feature | Minimum weighted score | Additional guardrail |
|---|---:|---|
| Gatekeeper | 4.0/5 | FN <= 8%, FP <= 12% |
| Embedding | 3.8/5 | Recall@5 >= 0.80 |
| Gen | 3.8/5 | JSON validity >= 98% |
| Review | 4.0/5 | false alarm <= 10% |
| Mentor | 3.8/5 | major bug miss <= 12% |

## 5) Final Model Selection Formula
Use weighted objective to balance quality and cost:

`FinalScore = 0.65 * QualityScore + 0.20 * CostScore + 0.15 * SpeedScore`

Where each component is normalized to 0-100.
