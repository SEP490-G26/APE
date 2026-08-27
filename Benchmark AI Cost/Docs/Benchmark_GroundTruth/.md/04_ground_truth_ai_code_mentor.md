# 04 - Ground Truth: AI_Code_Mentor

## 1) Feature Scope
Given problem statement + student submission, AI mentor must:
1. Identify what is wrong.
2. Explain why.
3. Suggest correction direction (not full leakage if policy forbids).
4. Provide complexity estimation where relevant.

## 2) Ground-Truth Case Set (seed)
| case_id | language | domain | difficulty | bug_type | expected diagnosis | expected fix direction | expected complexity note |
|---|---|---|---|---|---|---|---|
| MENT_C_0001 | C | Intro | easy | off_by_one | loop bound excludes last element | adjust loop boundary | O(n) unchanged |
| MENT_C_0002 | C | Intro | medium | uninitialized_var | variable used before assignment | initialize before use | - |
| MENT_C_0003 | C | Intro | hard | pointer_null | potential null dereference | add null checks | O(1) checks |
| MENT_JOOP_0001 | Java | OOP | easy | encapsulation_break | direct field mutation bypassing getter/setter | use private fields + accessor methods | - |
| MENT_JOOP_0002 | Java | OOP | medium | override_mistake | method signature mismatch so no real override | align signature + `@Override` | - |
| MENT_JOOP_0003 | Java | OOP | hard | polymorphism_logic | wrong dynamic dispatch assumption | refactor inheritance/use interface contract | - |
| MENT_DSA_0001 | Java | DSA_OOP | medium | wrong_complexity_claim | says O(log n) but implementation is linear scan | correct complexity statement | O(n) |
| MENT_DSA_0002 | Java | DSA_OOP | hard | invalid_edge_case | fails empty input / single node edge | handle edge branch before main logic | same asymptotic |
| MENT_DSA_0003 | Java | DSA_OOP | hard | recursion_base_case | missing base case causes stack overflow | add proper base condition | depends on algorithm |
| MENT_MIX_0001 | C/Java | mixed | hard | multiple | syntax + logic + complexity errors together | prioritize critical compile/runtime issues first | include corrected complexity |

## 3) Required Response Structure (mentor)
```json
{
  "verdict": "correct|partially_correct|incorrect",
  "issues": [
    { "type": "...", "location": "line/function", "explanation": "..." }
  ],
  "fix_guidance": ["..."],
  "complexity": {
    "time": "O(...)|unknown",
    "space": "O(...)|unknown",
    "confidence": "low|medium|high"
  }
}
```

## 4) Ground-Truth Expectations
| Dimension | Requirement |
|---|---|
| correctness | Must catch principal bug category for case |
| precision | Mention concrete location/symptom |
| actionability | Suggest practical fix steps |
| pedagogical clarity | Explanation understandable for student level |
| non-leak policy | No full final answer if policy disallows |

## 5) Mentor Error Taxonomy
| error_code | Description |
|---|---|
| MENT_MISS_MAJOR | misses core defect |
| MENT_FALSE_BUG | flags non-existent defect |
| MENT_VAGUE | explanation too generic |
| MENT_BAD_COMPLEXITY | wrong asymptotic analysis |
| MENT_POLICY_LEAK | reveals prohibited full solution |
