using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.AI;

public class DbFirstAIAgentRoutingConfigService : IAIAgentRoutingConfigService
{
    private readonly DbContext _dbContext;

    public DbFirstAIAgentRoutingConfigService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AIAgent?> GetActiveAgentAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var role = MapFeatureToRole(featureName);
        if (role is null)
        {
            return null;
        }

        var filter = Builders<AIAgent>.Filter.Eq(item => item.AgentRole, role.Value);
        return await _dbContext.AIAgents.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    private static AIAgentRole? MapFeatureToRole(string featureName)
    {
        return featureName.Trim() switch
        {
            Application.Options.AIFeatureNames.Gatekeeper => AIAgentRole.Gatekeeper,
            Application.Options.AIFeatureNames.ExtractedContent => AIAgentRole.Extraction,
            Application.Options.AIFeatureNames.ExtractedStructure => AIAgentRole.Extraction,
            Application.Options.AIFeatureNames.Embedding => AIAgentRole.Embedding,
            Application.Options.AIFeatureNames.AutoTagging => AIAgentRole.Tagging,
            Application.Options.AIFeatureNames.QuestionGeneration => AIAgentRole.QuestionGenerator,
            Application.Options.AIFeatureNames.QuestionReview => AIAgentRole.Reviewer,
            Application.Options.AIFeatureNames.CodeMentor => AIAgentRole.Mentor,
            _ => null
        };
    }
}
