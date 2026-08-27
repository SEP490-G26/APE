using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace BE_IntegrationTests.External;

public sealed class ControlledAICreditPolicyService : IAICreditPolicyService
{
    private readonly DbContext _dbContext;

    public ControlledAICreditPolicyService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<double> CalculateQuestionGenerationCreditsAsync(int generationTokens, int reviewTokens, CancellationToken cancellationToken = default)
        => Task.FromResult(0d);

    public Task<double> CalculateMentorCreditsAsync(int totalTokens, CancellationToken cancellationToken = default)
        => Task.FromResult(0d);

    public Task<double> CalculateByosIngestionCreditsAsync(AIExtractionDraft draft, CancellationToken cancellationToken = default)
        => Task.FromResult(0d);

    public Task<double> CalculateEmbeddingCreditsAsync(IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        => Task.FromResult(0d);

    public Task<CreditChargeResult> DeductAsync(string userId, double chargedCredits, string featureName)
        => Task.FromResult(new CreditChargeResult(0d, 0, 0));

    public Task<AICreditQuote> QuoteQuestionGenerationAsync(int generationTokens, int reviewTokens, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildQuote(AICreditFeatureKeys.QuestionGenerationReview));

    public Task<AICreditQuote> QuoteMentorAsync(int totalTokens, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildQuote(AICreditFeatureKeys.CodeMentor));

    public Task<AICreditQuote> QuoteByosIngestionAsync(AIExtractionDraft draft, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildQuote(AICreditFeatureKeys.ByosIngest));

    public Task<AICreditQuote> QuoteEmbeddingAsync(IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildQuote(AICreditFeatureKeys.ByosIngest));

    public AICreditQuote MergeQuotes(string featureKey, params AICreditQuote[] quotes)
        => BuildQuote(featureKey);

    public Task<CreditChargeResult> ChargeAsync(AICreditChargeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CreditChargeResult(0d, 0, 0));

    public async Task<AICreditRefundResult> RefundAsync(AICreditRefundRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.AICreditTransactions
            .Find(item => item.Id == request.TransactionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Credit transaction not found.");

        if (transaction.RefundCredits <= 0)
        {
            transaction.RefundCredits = transaction.ChargedCredits;
            transaction.RefundReason = request.RefundReason;
            transaction.Status = "refunded";
            transaction.UpdatedAt = DateTime.UtcNow;

            await _dbContext.AICreditTransactions.ReplaceOneAsync(
                item => item.Id == transaction.Id,
                transaction,
                cancellationToken: cancellationToken);
        }

        return new AICreditRefundResult
        {
            TransactionId = transaction.Id,
            Status = transaction.Status,
            RefundedCredits = transaction.RefundCredits,
            RemainingFreeCredit = 0,
            RemainingPaidCredit = 0
        };
    }

    private static AICreditQuote BuildQuote(string featureKey)
    {
        return new AICreditQuote
        {
            FeatureKey = featureKey,
            PolicyVersion = "integration-test",
            CalculatedCredits = 0d
        };
    }
}
