using Domain.Exceptions;

namespace Domain.Entities;

public sealed class CodeFile
{
    public string Filename { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsReadOnly { get; set; }

    public static CodeFile Create(
        string filename,
        string content,
        bool isReadOnly = false)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new ArgumentException(
                "Filename is required.",
                nameof(filename));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Content is required.",
                nameof(content));
        }

        return new CodeFile
        {
            Filename = filename.Trim(),
            Content = content,
            IsReadOnly = isReadOnly
        };
    }
}

public sealed class TestCase
{
    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public bool IsHidden { get; set; } = true;

    public bool IsSample { get; set; }

    public int TimeLimitMs { get; set; } = 2_000;

    public int MemoryLimitKb { get; set; } = 256_000;
}

public sealed class Hint
{
    private Hint()
    {
    }

    private Hint(
        int level,
        string text)
    {
        Level = level;
        Text = text;
    }

    public int Level { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public static Hint Create(
        int level,
        string text)
    {
        if (level <= 0)
        {
            throw new DomainRuleException(
                "Hint level must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainRuleException(
                "Hint text is required.");
        }

        return new Hint(
            level,
            text.Trim());
    }
}