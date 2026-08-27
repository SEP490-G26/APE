# ROLE AND CONTEXT
You are an Expert Full-Stack Developer (Node.js & React) specializing in AI Integration and FinOps (Financial Operations).
My goal is to build a "Benchmark & AI Cost Estimation Tool" for an EdTech Platform. This tool will simulate 7 core AI tasks, call the actual AI APIs (OpenAI and Google Gemini), measure the latency, extract token usage, calculate the exact USD cost, and display the results on a simple UI Dashboard.

# TECH STACK
- Backend: Node.js, Express, dotenv.
- AI SDKs: `openai` (for text-embedding-3-small & gpt-4o-mini), `@google/generative-ai` (for Gemini 1.5 Flash).
- Frontend: Vite + React + TailwindCSS + Recharts (for simple cost/latency visualization).
- Data Storage: In-memory array or a simple local JSON file (no database needed for this prototype).

# CORE REQUIREMENTS

## 1. Cost Constants & Formulas (Configure these in a constants file)
- Gemini 3.5 Flash: Input = $0.075 / 1M tokens | Output = $0.30 / 1M tokens.
- OpenAI text-embedding-3-small: Input = $0.02 / 1M tokens.
- GPT-4o mini: Input = $0.15 / 1M tokens | Output = $0.60 / 1M tokens.

## 2. The Benchmark Runner (Backend Logic)
Create a unified Wrapper/Middleware function for all AI calls that does the following:
1. Starts a high-resolution timer (`performance.now()`).
2. Executes the API call.
3. Stops the timer to calculate `latency_ms`.
4. Extracts `prompt_tokens` and `completion_tokens` directly from the API response object (Do NOT use manual counting libraries, read the exact usage from the API response).
5. Calculates the `cost_usd` based on the Model used.
6. Returns the standardized result object: `{ taskName, model, latency_ms, inputTokens, outputTokens, totalCostUsd, status }`.

## 3. The 7 AI Tasks to Simulate (Create mock input data for each)
Implement endpoints to trigger these specific simulations:
1. `AI_Gatekeeper`: Send a 1000-word mock syllabus text. Task: Check if it's related to IT. (Model: Gemini 1.5 Flash).
2. `AI_Vision_Parse`: Send a base64 mock image of a document. Task: Extract text to Markdown. (Model: Gemini 1.5 Flash).
3. `AI_Embedding`: Send a 500-word text chunk. Task: Convert to vector. (Model: text-embedding-3-small).
4. `AI_Question_Gen`: Send a 2000-word RAG context. Task: Generate 5 JSON multiple-choice questions. (Model: Gemini 1.5 Flash).
5. `AI_Review_Agent`: Send the JSON from Task 4 + RAG context. Task: Verify logic and accuracy. (Model: GPT-4o mini).
6. `AI_Code_Mentor`: Send a mock buggy Java code + test cases. Task: Explain the bug and provide time complexity. (Model: GPT-4o mini).
7. `AI_Summary_Analyzer`: Send a mock JSON array of 50 student practice sessions. Task: Provide a study recommendation. (Model: Gemini 1.5 Flash).

## 4. Frontend UI (Dashboard)
Create a clean, modern, dark-mode UI with Tailwind:
- A "Run All Benchmarks" button.
- A Data Table displaying the results of the 7 tasks (Columns: Task, Model, Latency, Input Tokens, Output Tokens, Cost ($)).
- A Bar Chart (using Recharts) comparing the Latency (ms) of the tasks.
- A Summary Card showing the "Total Cost for this Run" and "Estimated Cost for 1,000 Users/Month" (Total Cost * 1000 * 30).

# EXECUTION STEPS
1. Initialize the monorepo structure (backend and frontend folders).
2. Generate the `package.json` files with required dependencies.
3. Write the Backend code (Express setup, AI SDK initialization, Benchmark Wrapper, and the 7 Task routes). Ensure you mock the payload strings so I don't have to provide them.
4. Write the Frontend code (React components, API fetching logic, Table, and Chart).
5. Provide the exact terminal commands to run both servers. Do not skip any code, provide fully working files.