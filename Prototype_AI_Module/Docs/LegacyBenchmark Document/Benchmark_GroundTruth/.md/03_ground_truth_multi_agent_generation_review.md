# 03 - Ground Truth: Multi-Agent Generation + Review

## 1) Feature Scope
- MultiAgent_1_Gen: Generate FE/PE items from retrieved context.
- MultiAgent_2_Review: Verify correctness, quality, and regenerate decision.

## 2) Standard Output Contract (Generator)
```json
{
  "items": [
    {
      "type": "FE|PE",
      "domain": "C|Java_OOP|DSA_OOP",
      "difficulty": "easy|medium|hard",
      "question": "...",
      "options": ["..."],
      "answer": "...",
      "explanation": "...",
      "tags": ["..."]
    }
  ]
}
```

## 3) Standard Output Contract (Reviewer)
```json
{
  "review_status": "approved|needs_regenerate",
  "issues": ["..."],
  "score": {
    "factuality": 0,
    "alignment": 0,
    "clarity": 0,
    "difficulty_fit": 0
  },
  "regenerate_hint": "..."
}
```

## 4) Ground-Truth Case Matrix (seed)
| case_id | domain | difficulty | task_type | expected core outcome |
|---|---|---|---|---|
| GEN_C_FE_0001 | C | easy | FE | 1 correct answer, basic syntax concept, valid explanation |
| GEN_C_PE_0001 | C | medium | PE | input/output constraints consistent with expected solution |
| GEN_JOOP_FE_0001 | Java_OOP | medium | FE | tests inheritance/polymorphism distinction |
| GEN_JOOP_PE_0001 | Java_OOP | hard | PE | class design + method override + edge-case tests |
| GEN_DSA_FE_0001 | DSA_OOP | medium | FE | complexity question with correct big-O |
| GEN_DSA_PE_0001 | DSA_OOP | hard | PE | data structure implementation + hidden case validity |
| REV_BAD_FACT_0001 | C | medium | Review | must flag wrong factual answer |
| REV_BAD_DIFF_0001 | Java_OOP | easy | Review | must flag mismatch difficulty (too hard) |
| REV_BAD_JSON_0001 | DSA_OOP | medium | Review | must flag invalid schema/format |
| REV_OK_0001 | Java_OOP | medium | Review | should approve with no major issues |

## 5) Hard Ground-Truth Constraints
| Constraint | Required |
|---|---|
| FE single-correctness | Exactly one valid correct option |
| PE test consistency | `expected_output` must match reference logic |
| Difficulty alignment | Easy: basic recall, Medium: applied reasoning, Hard: multi-step synthesis |
| Hallucination control | No unsupported claim outside provided context |
| JSON validity | Must parse under agreed schema |

## 6) Regenerate Trigger Rules (Reviewer GT)
| Condition | Expected `review_status` |
|---|---|
| factual error in answer/explanation | needs_regenerate |
| invalid schema | needs_regenerate |
| difficulty mismatch >= 2 levels | needs_regenerate |
| minor wording issue only | approved |
| all quality dimensions pass threshold | approved |
