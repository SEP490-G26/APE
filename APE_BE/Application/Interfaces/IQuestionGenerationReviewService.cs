/**
 * IQuestionGenerationReviewService.cs
 * Interface for the Multi-Agent Question Generation, Rule Review, Rubric Scoring, and Self-Repair subsystem.
 * Step 4 (Core) in the APE AI Pipeline.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for generating and iteratively reviewing exam/practice questions (FE & PE)
/// from context chunks using multi-agent loops.
/// </summary>
public interface IQuestionGenerationReviewService
{
    /// <summary>
    /// Executes the end-to-end question generation and review pipeline:
    /// Generator Agent -> Rule Review -> Reviewer Agent (Rubric) -> Repair Agent -> Final result.
    /// </summary>
    /// <param name="request">Generation request containing difficulty, question type, count, and context pack.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GenerationReviewResultDto"/> containing verified questions and review metadata.</returns>
    Task<GenerationReviewResultDto> RunAsync(GenerationReviewRequestDto request, CancellationToken cancellationToken = default);
}
