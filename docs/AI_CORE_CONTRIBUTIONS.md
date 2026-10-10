# 🧠 AI Core Subsystem & Engineering Contributions

**Project:** APE — AI-Powered Examination and Programming Practice Platform  
**Contributor:** Phạm Quốc Minh  
**Role:** AI Core & Prototype Developer  
**Capstone Group:** SEP490_G26 — FPT University Hanoi  

---

## 1. Overview & Professional Context

This document provides a detailed technical breakdown of the design, implementation, and empirical benchmarking of the **AI Subsystem** within the APE platform.

### Methodology & Development Clarification
The engineering of the AI Core subsystem was conducted leveraging modern **AI-assisted software development tools**. The contributor focused on:
- High-level system requirements analysis and architecture design.
- Multi-stage pipeline workflow orchestration.
- Structured prompt engineering, evaluation rubrics, and pedagogical policies.
- Prototype testbed development (`Prototype_AI_Module`) and empirical benchmarking.
- Service integration, cross-system debugging, and output quality verification.

*Note: This document reflects system architecture design, integration coordination, and empirical research contributions rather than manual, hand-authored coding of the entire backend or full test suite.*

---

## 2. Core AI Subsystem Architecture

The APE AI subsystem is structured as an orchestrated **5-Stage Pipeline** designed to convert raw academic materials into pedagogically sound examination items and diagnostic feedback:

```
[Raw Document / Input]
         │
         ▼
 ┌───────────────┐
 │ 1. Gatekeeper │  ---> Pre-checks: Subject domain, content safety, prompt injection
 └───────┬───────┘
         ▼
 ┌───────────────┐
 │ 2. Extraction │  ---> Text parsing, Vision OCR for UML diagrams, chapter detection
 └───────┬───────┘
         ▼
 ┌───────────────┐
 │  3. Chunking  │  ---> Semantic boundary chunking + Cohere 1024-dim vector embeddings
 └───────┬───────┘
         ▼
 ┌───────────────┐
 │ 4. Generation │  ---> Generator Agent + Deterministic Rules + LLM Reviewer + Self-Repair
 └───────┬───────┘
         ▼
 ┌───────────────┐
 │ 5. Code Mentor│  ---> Judge0 error diagnostics -> Socratic hints without answer leakage
 └───────────────┘
```

### Stage 1: Safety Gatekeeper & Anti-Injection Filtering
- **Objective:** Prevent malicious prompt injection, ensure document relevance to target curricula (e.g., Java OOP / Programming Fundamentals), and reject off-topic or abusive input before incurring substantial LLM costs.
- **Implementation:** Two-tier inspection combining deterministic string heuristics with lightweight model inference (`deepseek-v4-flash` / `gpt-4o-mini`).

### Stage 2: Multimodal Extraction & Chapter Structure Detection
- **Objective:** Ingest heterogeneous courseware (`.pdf`, `.docx`, `.pptx`).
- **Implementation:**
  - Standard text normalization and markdown cleanup.
  - **Vision OCR Integration:** Utilized multimodal LLM vision capabilities (`gpt-4o`) to transcribe architectural diagrams, UML class charts, and code screenshots embedded within presentation slides.
  - **Hierarchical Structure Detection:** High-signal header extraction to infer chapter boundaries (`chapterKeys`) for scoped retrieval.

### Stage 3: Semantic Chunking & Cohere Vector Embedding
- **Objective:** Transform normalized academic text into searchable vector representations.
- **Implementation:**
  - Semantic sliding window chunking (500–1,000 tokens) with a 100-token overlap to maintain context continuity.
  - Vectorization using **Cohere `embed-multilingual-v3.0`** (1,024-dimensional float vectors), enabling cross-lingual semantic matching between Vietnamese course syllabi and English programming concepts.

### Stage 4: Hybrid RAG, Multi-Agent Question Generation & 3-Tier Review Loop
- **Objective:** Generate valid Multiple-Choice (FE) and Practical Coding (PE) items strictly grounded in the ingested material.
- **Workflow:**
  1. **Hybrid Retrieval:** Filters chunks by chapter/topic metadata combined with vector cosine similarity under a strict token budget.
  2. **Generator Agent (`gpt-5.4` / `deepseek-v4-pro`):** Synthesizes question stems, options with pedagogical rationales (FE), or problem specifications with skeleton code, solution code, and visible/hidden test suites (PE).
  3. **Tier 1 (Deterministic C# Rules):** Validates schema compliance, class structure, anti-duplicate fingerprints (`QuestionFingerprinting.Build`), and filters low-value duplicate templates.
  4. **Tier 2 (LLM Reviewer Agent `gpt-5.4-mini` / `deepseek-v4-flash`):** Evaluates distractor plausibility, Bloom taxonomy alignment, and edge-case coverage against structured rubrics.
  5. **Tier 3 (Self-Repair Loop):** Feeds identified issues back into a bounded correction loop (up to 3 attempts) for autonomous JSON and logic repair.

### Stage 5: Socratic Code Mentor
- **Objective:** Provide actionable debugging assistance for student code submissions without providing the direct solution.
- **Implementation:**
  - Extracts compiler errors, runtime exceptions, and failing test case outputs from the **Judge0** execution sandbox.
  - Guides the student using diagnostic questions, conceptual hints, and algorithmic checkpoints adhering to Socratic pedagogical guidelines.

---

## 3. Prototype Research & Benchmarking (`Prototype_AI_Module`)

Prior to production backend integration, an independent prototype and test harness was engineered under `Prototype_AI_Module/src-dotnet`:

### Key Features of the Prototype Subsystem
1. **Dedicated Operator UI:** In-browser dashboard for triggering individual pipeline stages, inspecting prompt inputs/outputs, and testing candidate models in isolation.
2. **Model Catalog & Cost Tracking:** Automated tracking of prompt tokens, completion tokens, execution latencies, and estimated USD/VND inference costs.
3. **Empirical Model Comparison:**
   - Evaluated **OpenAI** (`gpt-4o`, `gpt-4o-mini`), **DeepSeek** (`deepseek-v4-pro`, `deepseek-v4-flash`), and **Google Gemini** (`gemini-2.5-flash`).
   - Identified that lightweight models (`deepseek-v4-flash`, `gpt-4o-mini`) were highly cost-effective for Gatekeeper and Review stages, while higher-tier models were required for complex PE coding generation.

---

## 4. System Integration & Reliability Engineering

### 4.1 Database-First Dynamic Configuration
- AI models, prompt templates, temperature parameters, and token ceilings are stored in MongoDB (`system_settings` and `ai_agents`).
- Enables dynamic switching of active models and prompt adjustments without redeploying backend binaries.

### 4.2 Multi-Provider Routing & Circuit Breaker Fallback
- Implemented client-level try/catch failover between primary (OpenAI) and secondary (DeepSeek) providers.
- Safeguards the application against transient HTTP timeouts, network partition issues, or upstream provider outages.

### 4.3 Token Economics & Fair Micro-Billing
- Implemented real-time token tracking mapped to VND debit transactions.
- **Post-Pay Billing:** Balances are deducted only upon successful item generation.
- **Absorbed Loss Protection:** In edge cases where token usage exceeds the student's remaining balance, excess costs are absorbed by the platform, ensuring wallet balances do not drop below zero.

---

## 5. Known Engineering Limitations & Trade-offs

During development and evaluation, several key engineering trade-offs were documented:

### 1. Pipeline Latency & Synchronous HTTP Request Execution
- **Observation:** Multi-stage generation (RAG retrieval $\rightarrow$ LLM generation $\rightarrow$ multi-tier review $\rightarrow$ self-repair) can take between 15 to 45 seconds depending on upstream model response times.
- **Current State:** AI requests are processed **synchronously within the HTTP request lifecycle**.
- **Deferred Solution:** An asynchronous job queue architecture (persisting jobs to MongoDB, returning a `jobId`, and polling via background workers) was designed (`docs/plan/03-ai-async-job-orchestration-performance-plan.md`) but intentionally deferred to prioritize stabilizing core business logic and pedagogical output quality before graduation defense.

### 2. Token Costs vs. Context Comprehensiveness
- **Observation:** Ingesting large slide decks with multiple embedded architectural diagrams leads to high token consumption during Vision OCR and embedding phases.
- **Mitigation:** Applied strict token budgeting during chunk assembly (`ai_context_packs`) and enforced a minimum balance gate (`AI balance >= 1,000 VND`) before initiating generation.

---

## 6. Key Takeaways & Lessons Learned

1. **Deterministic Guards Enhance LLM Stability:** Relying solely on prompt instructions for output formatting leads to occasional JSON parse failures. Combining LLM generation with deterministic C# validation rules and bounded self-repair loops drastically improved system reliability.
2. **Prototyping Accelerates System Integration:** Validating model behavior, prompt structures, and token pricing in an isolated test harness (`Prototype_AI_Module`) prevented regressions in the primary application and established realistic latency baselines.
3. **Transparent Engineering Attribution:** Utilizing AI-assisted engineering tools significantly elevated productivity in scaffolding and debugging complex architectures; maintaining disciplined code review, architecture governance, and rigorous testing ensured system integrity.
