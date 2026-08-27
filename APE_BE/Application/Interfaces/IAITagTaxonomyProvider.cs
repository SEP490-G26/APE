using Application.Common;

namespace Application.Interfaces;

public interface IAITagTaxonomyProvider
{
    AITagTaxonomyCatalog GetCatalog();
}
