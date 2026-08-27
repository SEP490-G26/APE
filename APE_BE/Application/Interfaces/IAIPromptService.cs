namespace Application.Interfaces;

public interface IAIPromptService
{
    string Render(string template, IReadOnlyDictionary<string, string?> variables);
}
