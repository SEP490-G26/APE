# Project Context Summary (Quick Reference)

Source files read:
- Docs/DB Schema Table.docx
- Docs/Report3_Software Requirement Specification.docx (read beginning/project description sections only)

## 1) Project overview (from SRS beginning)
- Product: APE (AI-Powered Examination and Programming Practice Platform).
- Target users: FPT University IT students + Super Admin.
- Core purpose: upload learning materials, generate FE/PE practice exams, code execution/grading, AI mentoring, analytics, gamification, credit/payment model.

## 2) External systems/integrations
- Google SSO (authentication)
- Judge0 API (code execution and grading metrics)
- LLM providers (OpenAI/Gemini)
- OCR service
- Payment API

## 3) Main workflows highlighted in SRS intro
- Data ingestion & AI gatekeeper: upload -> OCR -> classify support -> chunk/tag/embed -> store.
- PE generation with validation loops (Generator/Critic/Judge0).
- FE generation with RAG + actor/critic and structured output.
- Practice coding workspace + AI code mentor.
- Analytics + AI study advice.

## 4) High-level data domains from DB schema
- User & payment: users, payment, reports, system settings.
- AI core/logs: AI agents, prompts, AI usage logs.
- Analytics: daily practice summaries, AI knowledge assessments.
- RAG/document: courses, documents, knowledge chunks.
- Practice/execution: question bank, exams, practice sessions, submissions, AI mentor feedbacks.

## 5) DB notes useful for upcoming benchmark design
- Existing tables already include AI usage/cost fields (`tokens_used`, `cost_usd`, `credits_deducted`, timestamps).
- RAG pipeline entities are available (`Documents`, `KnowledgeChunks`) and can map directly to benchmark upload/chunk/embed flow.
- Practice and grading entities are detailed enough to benchmark generator/reviewer/mentor quality against submission outcomes.

## 6) Scope reminder for this summary
- This note intentionally captures only the initial project description and structural context.
- Full SRS details (all UC specs/functional constraints) are not exhaustively summarized yet.
