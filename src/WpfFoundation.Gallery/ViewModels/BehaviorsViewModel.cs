using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Behaviors;

namespace WpfFoundation.Gallery.ViewModels;

public enum NoteColumn
{
    Title,
    Trail,
    Distance
}

public sealed record FieldNote(string Title, string Trail, double DistanceKilometers);

/// <summary>Fictional field notes for the sorting, sizing, drop and converter examples.</summary>
public sealed partial class BehaviorsViewModel : ObservableObject
{
    public BehaviorsViewModel()
    {
        NoteSort = new ColumnSort<FieldNote>(CreateComparer, () => !IsSortingLocked);
        AddDroppedFilesCommand = new RelayCommand<string[]>(AddDroppedFiles, CanAddDroppedFiles);
    }

    public ObservableCollection<FieldNote> Notes { get; } =
    [
        new("Fog over the quarry", "Old Mill Loop", 6.4),
        new("Heron at dawn", "Reed Marsh Path", 3.1),
        new("Lost trail marker", "Granite Ridge", 11.8),
        new("Moss survey", "Cedar Hollow", 4.7),
        new("Wind on the summit", "Granite Ridge", 9.2)
    ];

    public ColumnSort<FieldNote> NoteSort { get; }

    /// <summary>A sorting guard: while it is on, header clicks are ignored.</summary>
    [ObservableProperty]
    public partial bool IsSortingLocked { get; set; }

    [ObservableProperty]
    public partial bool IsArchived { get; set; }

    public ObservableCollection<string> DroppedFiles { get; } = [];

    public RelayCommand<string[]> AddDroppedFilesCommand { get; }

    private static bool CanAddDroppedFiles(string[]? paths) =>
        paths is { Length: > 0 } && paths.All(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase));

    private void AddDroppedFiles(string[]? paths)
    {
        foreach (string path in paths ?? [])
        {
            DroppedFiles.Add(Path.GetFileName(path));
        }
    }

    private static IComparer<FieldNote> CreateComparer(object column, ListSortDirection direction)
    {
        Comparison<FieldNote> compare = (NoteColumn)column switch
        {
            NoteColumn.Trail => (x, y) => string.Compare(x.Trail, y.Trail, StringComparison.CurrentCulture),
            NoteColumn.Distance => (x, y) => x.DistanceKilometers.CompareTo(y.DistanceKilometers),
            _ => (x, y) => string.Compare(x.Title, y.Title, StringComparison.CurrentCulture)
        };
        return Comparer<FieldNote>.Create(direction == ListSortDirection.Ascending ? compare : (x, y) => compare(y, x));
    }
}
