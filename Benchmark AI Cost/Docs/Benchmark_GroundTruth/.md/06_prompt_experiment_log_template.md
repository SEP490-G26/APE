# 06 - Prompt Experiment Log Template

## 1) Purpose
Track prompt evolution and benchmark outcomes across versions.

## 2) Prompt Experiment Record
| exp_id | date | feature | model | dataset_version | prompt_version | temperature | max_tokens | notes |
|---|---|---|---|---|---|---:|---:|---|
| EXP_0001 | 2026-06-25 | AI_Gatekeeper | gemini-3.1-flash-lite | dataset_v1 | gatekeeper_v1 | 0.2 | 512 | baseline |

## 3) Prompt Content Archive
| prompt_version | system_prompt_summary | key_variables | expected_output_schema |
|---|---|---|---|
| gatekeeper_v1 | classify support domain | doc_excerpt | JSON with is_supported/domain/reason |

## 4) Evaluation Summary per Experiment
| exp_id | quality_score | avg_latency_ms | avg_tokens | avg_cost_usd | success_rate | decision |
|---|---:|---:|---:|---:|---:|---|
| EXP_0001 | 82.4 | 910 | 1200 | 0.0007 | 99% | keep |

## 5) Prompt Change Log
| from_version | to_version | reason_for_change | expected_impact | actual_impact |
|---|---|---|---|---|
| gatekeeper_v1 | gatekeeper_v2 | too many false positives in ambiguous docs | reduce FP | TBD |

## 6) Governance Rules
1. Never delete old prompt versions used in reported results.
2. Every version change must map to an experiment ID.
3. Compare using the same fixed dataset before claiming improvement.
