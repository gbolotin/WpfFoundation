using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfFoundation.Behaviors;

/// <summary>
/// Sort state that <see cref="GridViewSort"/> reads from and updates for a sortable list.
/// </summary>
public interface IColumnSort : INotifyPropertyChanged
{
    /// <summary>The key of the sorted column, or <see langword="null"/> while the list is unsorted.</summary>
    object? Column { get; }

    /// <summary>The direction of the sorted column.</summary>
    ListSortDirection Direction { get; }

    /// <summary>The comparer that orders the list's view, or <see langword="null"/> while the list is unsorted.</summary>
    IComparer? Comparer { get; }

    /// <summary>Sorts by a column: the sorted column toggles its direction, another column sorts ascending.</summary>
    void SortBy(object column);
}

/// <summary>
/// Column sort state for a list: sorting by the sorted column toggles the direction and
/// sorting by another column sorts it ascending. The comparer orders the view, not the source.
/// </summary>
/// <param name="createComparer">Creates the comparer for a column key and direction.</param>
/// <param name="canSort">
/// When it returns <see langword="false"/>, <see cref="SortBy"/> is ignored; for example while a batch
/// is processing rows in the current display order.
/// </param>
public sealed class ColumnSort<TItem>(Func<object, ListSortDirection, IComparer<TItem>> createComparer, Func<bool>? canSort = null)
    : ObservableObject, IColumnSort
{
    private object? column;
    private ListSortDirection direction;
    private Comparer<TItem>? comparer;

    /// <inheritdoc />
    public object? Column => column;

    /// <inheritdoc />
    public ListSortDirection Direction => direction;

    /// <summary>The comparer for the current column and direction, or <see langword="null"/> while unsorted.</summary>
    public IComparer<TItem>? Comparer => comparer;

    IComparer? IColumnSort.Comparer => comparer;

    /// <inheritdoc />
    public void SortBy(object column)
    {
        if (canSort?.Invoke() == false)
        {
            return;
        }

        direction = Equals(this.column, column) && direction == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        this.column = column;
        OnPropertyChanged(nameof(Column));
        OnPropertyChanged(nameof(Direction));
        Refresh();
    }

    /// <summary>Publishes a new comparer so views re-sort items whose values changed since the last sort.</summary>
    public void Refresh()
    {
        if (column is null)
        {
            return;
        }

        comparer = Comparer<TItem>.Create(createComparer(column, direction).Compare);
        OnPropertyChanged(nameof(Comparer));
    }

    /// <summary>Returns items in the sorted order, or in their original order when nothing is sorted.</summary>
    public TItem[] Order(IEnumerable<TItem> items) => comparer is null ? [.. items] : [.. items.Order(comparer)];
}
