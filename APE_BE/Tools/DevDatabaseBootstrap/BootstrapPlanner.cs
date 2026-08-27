namespace DevDatabaseBootstrap;

public enum FixtureDocumentAction
{
    Insert,
    ReplaceFixture,
    RejectCollision
}

public static class BootstrapPlanner
{
    public static FixtureDocumentAction DecideAction(
        bool documentExistsByDeterministicId,
        bool existingDeterministicDocumentBelongsToFixture)
    {
        if (!documentExistsByDeterministicId)
        {
            return FixtureDocumentAction.Insert;
        }

        return existingDeterministicDocumentBelongsToFixture
            ? FixtureDocumentAction.ReplaceFixture
            : FixtureDocumentAction.RejectCollision;
    }
}
