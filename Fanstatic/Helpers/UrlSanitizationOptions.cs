namespace Fanstatic.Helpers;

/// <summary>
/// Options for the <see cref="UrlExtension"/> class.
/// Basically to force lowercase and to change the replacement character.
/// </summary>
public readonly record struct UrlSanitizationOptions
{
    public bool LowerCase { get; init; }

    public char? ReplacementChar { get; init; }

    public bool ReplaceDot { get; init; }

    public UrlSanitizationOptions()
    {
        LowerCase = true;
        ReplacementChar = '-';
        ReplaceDot = false;
    }

    public UrlSanitizationOptions(bool lowerCase, char? replacementChar = '-', bool replaceDot = false)
    {
        LowerCase = lowerCase;
        ReplacementChar = replacementChar;
        ReplaceDot = replaceDot;
    }
}
