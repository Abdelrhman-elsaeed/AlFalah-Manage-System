namespace AlFalah.Shared.Models;

/// <summary>
/// Common paging query parameters for list endpoints.
/// </summary>
public class PagedQuery
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 1,
            > 100 => 100,
            _ => value
        };
    }

    public string? SortBy { get; set; }
    public bool SortDesc { get; set; } = false;
}