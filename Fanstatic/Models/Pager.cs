namespace Fanstatic.Models;

/// <summary>
/// Pagination metadata exposed to templates.
/// </summary>
/// <param name="Count">Total number of pagination pages.</param>
/// <param name="Current">Current page number (1-based).</param>
/// <param name="First">First page number.</param>
/// <param name="Last">Last page number.</param>
/// <param name="Prev">Previous page number, if any.</param>
/// <param name="Next">Next page number, if any.</param>
/// <param name="Pages">All page numbers in the pager range (for navigation).</param>
/// <param name="PageItems">Content items for the current pagination page (the sliced view).</param>
/// <param name="BaseUrl">Root-relative URL of the list page, with trailing slash (e.g. "/blog/").</param>
/// <param name="PaginatePath">URL segment used for paginated pages (e.g. "page").</param>
public sealed record Pager(
    int Count,
    int Current,
    int First,
    int Last,
    int? Prev,
    int? Next,
    IReadOnlyList<int> Pages,
    IReadOnlyList<IPage> PageItems,
    string BaseUrl,
    string PaginatePath);
