# Benchmark Ground Truth - Master Plan

This folder defines standardized datasets and scoring standards for APE AI benchmark.

Scope language/domain:
- C (intro programming)
- Java OOP
- Data Structures & Algorithms in OOP context

Files:
1. `01_ground_truth_dataset_spec.md`
- Dataset design principles
- Coverage matrix (feature x difficulty x question type)
- Prompt versioning log template

2. `02_ground_truth_ai_gatekeeper.md`
- Ground-truth dataset for topic support classification
- Expected labels and rationale format

3. `03_ground_truth_multi_agent_generation_review.md`
- Ground-truth for exam/question generation + AI review agent
- Expected output schema + correctness anchors

4. `04_ground_truth_ai_code_mentor.md`
- Ground-truth coding submissions and expected mentor diagnosis
- Bug category, fix direction, complexity expectation

5. `05_rubric_scoring.md`
- Unified rubric and weights per feature
- Pass thresholds and reporting metrics

6. `06_prompt_experiment_log_template.md`
- Prompt tuning experiment log format
- Version-to-version comparison template

Usage:
- Keep datasets fixed across model versions for fair comparison.
- Add new versions of prompts/models, not overwrite old benchmark records.
