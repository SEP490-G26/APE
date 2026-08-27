using Domain.Entities;
using MongoDB.Driver;

namespace Infrastructure.Data;

public sealed class DbContext
{
    public DbContext(string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MongoDB connection string is required.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("MongoDB database name is required.", nameof(databaseName));
        }

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        var client = new MongoClient(settings);
        Database = client.GetDatabase(databaseName.Trim());
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>("Users");
    public IMongoCollection<Course> Courses => Database.GetCollection<Course>("Courses");
    public IMongoCollection<Exam> Exams => Database.GetCollection<Exam>("Exams");
    public IMongoCollection<PracticeSession> PracticeSessions => Database.GetCollection<PracticeSession>("PracticeSessions");
    public IMongoCollection<FEQuestion> FEQuestions => Database.GetCollection<FEQuestion>("FEQuestions");
    public IMongoCollection<PEQuestion> PEQuestions => Database.GetCollection<PEQuestion>("PEQuestions");
    public IMongoCollection<FE_Submission> FESubmissions => Database.GetCollection<FE_Submission>("FESubmissions");
    public IMongoCollection<PE_Submission> PE_Submissions => Database.GetCollection<PE_Submission>("PESubmissions");
    public IMongoCollection<Document> Documents => Database.GetCollection<Document>("Documents");
    public IMongoCollection<AIExtractionDraft> AIExtractionDrafts => Database.GetCollection<AIExtractionDraft>("AIExtractionDrafts");
    public IMongoCollection<AIContextPack> AIContextPacks => Database.GetCollection<AIContextPack>("AIContextPacks");
    public IMongoCollection<KnowledgeChunk> KnowledgeChunks => Database.GetCollection<KnowledgeChunk>("KnowledgeChunks");
    public IMongoCollection<AIAgent> AIAgents => Database.GetCollection<AIAgent>("AIAgents");
    public IMongoCollection<AIRuleArtifact> AIRuleArtifacts => Database.GetCollection<AIRuleArtifact>("AI_Rule_Artifacts");
    public IMongoCollection<AIUsageLog> APIUsageLogs => Database.GetCollection<AIUsageLog>("AIUsageLogs");
    public IMongoCollection<AIVndBillingTransaction> AIVndBillingTransactions => Database.GetCollection<AIVndBillingTransaction>("AI_VND_Billing_Transactions");
    public IMongoCollection<AIMentorFeedback> AIMentorFeedbacks => Database.GetCollection<AIMentorFeedback>("AIMentorFeedbacks");
    public IMongoCollection<SystemSetting> SystemSettings => Database.GetCollection<SystemSetting>("SystemSettings");
    public IMongoCollection<Payment> Payments => Database.GetCollection<Payment>("Payments");
    public IMongoCollection<Report> Reports => Database.GetCollection<Report>("Reports");
    public IMongoCollection<TopupPackage> TopupPackages => Database.GetCollection<TopupPackage>("TopupPackages");
}
