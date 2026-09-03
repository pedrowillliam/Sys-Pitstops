namespace SysPitstops.Api.Contracts;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // The list screens page; the caller may not. Clamping here keeps a stray
    // pageSize=100000 from turning into a full table scan.
    public static (int Page, int PageSize) Clamp(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1),
         Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
