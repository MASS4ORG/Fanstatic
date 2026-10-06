using System.Collections;

namespace Fanstatic.Models;

/// <summary>
/// A collection of taxonomy terms with pre-computed orderings by count and by name.
/// </summary>
public sealed class TaxonomyTerms : IReadOnlyList<TaxonomyTerm>
{
    readonly IReadOnlyList<TaxonomyTerm> _byCount;
    readonly Lazy<IReadOnlyList<TaxonomyTerm>> _byName;

    /// <summary>
    /// Initializes a new instance of <see cref="TaxonomyTerms"/> ordered by count descending, then name.
    /// </summary>
    /// <param name="terms">The taxonomy terms to include.</param>
    public TaxonomyTerms(IEnumerable<TaxonomyTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        _byCount = terms
            .OrderByDescending(term => term.Count)
            .ThenBy(term => term.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _byName = new Lazy<IReadOnlyList<TaxonomyTerm>>(() =>
            [.. _byCount.OrderBy(term => term.Name, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// Terms ordered by page count descending, then by name ascending.
    /// </summary>
    public IReadOnlyList<TaxonomyTerm> ByCount => _byCount;

    /// <summary>
    /// Terms ordered by name ascending.
    /// </summary>
    public IReadOnlyList<TaxonomyTerm> ByName => _byName.Value;

    /// <inheritdoc/>
    public int Count => _byCount.Count;

    /// <inheritdoc/>
    public TaxonomyTerm this[int index] => _byCount[index];

    /// <inheritdoc/>
    public IEnumerator<TaxonomyTerm> GetEnumerator() => _byCount.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
