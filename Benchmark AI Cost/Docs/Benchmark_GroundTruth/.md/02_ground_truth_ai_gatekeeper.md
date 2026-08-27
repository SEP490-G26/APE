# 02 - Ground Truth: AI_Gatekeeper

## 1) Feature Scope
Task: Classify whether uploaded material is in supported domain.
Supported domains:
- C programming (intro)
- Java OOP
- DSA in OOP context

Output schema:
```json
{
  "is_supported": true,
  "primary_domain": "C|Java_OOP|DSA_OOP|None",
  "confidence": 0.0,
  "reason": "short rationale"
}
```

## 2) Label Policy
| Label | Meaning |
|---|---|
| Supported | Material is substantially about C, Java OOP, or DSA OOP |
| Unsupported | Material is outside scope (math, philosophy, marketing, law, etc.) |
| Borderline | Mixed domain, weak relation to supported topics |

For benchmark scoring, Borderline maps to:
- `is_supported = false` when less than 40% relevant content.
- `is_supported = true` when at least 40% relevant and has direct programming learning intent.

## 3) Ground-Truth Dataset (sample seed, extend to 120 cases)
| case_id | domain_hint | difficulty | short_input_desc | expected_is_supported | expected_primary_domain |
|---|---|---|---|---:|---|
| GK_C_0001 | C | easy | Intro C syntax, variables, loops, scanf/printf | true | C |
| GK_C_0002 | C | medium | C pointers + memory allocation exercises | true | C |
| GK_JOOP_0001 | Java OOP | easy | Class/object, constructor, encapsulation notes | true | Java_OOP |
| GK_JOOP_0002 | Java OOP | hard | Polymorphism + abstract class + interface comparison | true | Java_OOP |
| GK_DSA_0001 | DSA OOP | medium | Java linked list implementation and complexity | true | DSA_OOP |
| GK_DSA_0002 | DSA OOP | hard | OOP-based graph BFS/DFS with adjacency list class design | true | DSA_OOP |
| GK_UNSUP_0001 | Philosophy | easy | Marxism-Leninism lecture notes | false | None |
| GK_UNSUP_0002 | Economics | medium | Microeconomics demand/supply analysis | false | None |
| GK_AMB_0001 | Mixed | hard | 30% Java examples + 70% project management slides | false | None |
| GK_AMB_0002 | Mixed | hard | 45% Java OOP + 55% software testing process | true | Java_OOP |

## 4) Expected Reason Anchors
| Condition | Expected reason pattern |
|---|---|
| Supported C | mention core C concepts (syntax/pointer/memory/I/O) |
| Supported Java OOP | mention OOP constructs in Java |
| Supported DSA OOP | mention DS/algorithm + OOP implementation context |
| Unsupported | explicitly state out-of-scope domain |
| Borderline | acknowledge mixed content and estimated relevance |

## 5) Error Taxonomy for Analysis
| error_code | Description |
|---|---|
| GK_FP | False positive: out-of-scope but predicted supported |
| GK_FN | False negative: supported but predicted unsupported |
| GK_DOM_WRONG | Supported decision correct but domain label wrong |
| GK_REASON_WEAK | Decision correct but reason lacks evidence |
