using System.ComponentModel.DataAnnotations;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfFoundation.Dialogs;

namespace WpfFoundation.Gallery.ViewModels;

/// <summary>A fictional trail note that the sample form edits.</summary>
public sealed class TrailNote(string title, double distanceKilometers)
{
    public string Title { get; set; } = title;

    public double DistanceKilometers { get; set; } = distanceKilometers;
}

/// <summary>
/// A validated custom form. It edits copies of the note's values and commits them only when accepted,
/// so cancelling leaves the note unchanged.
/// </summary>
public sealed partial class TrailNoteFormViewModel : DialogViewModel
{
    private readonly TrailNote note;
    private string noteTitle;
    private string distance;

    public TrailNoteFormViewModel(TrailNote note)
        : base("Edit trail note")
    {
        this.note = note;
        noteTitle = note.Title;
        distance = note.DistanceKilometers.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Buttons =
        [
            new DialogButton("Save note", AcceptCommand) { IsDefault = true, IsPrimary = true },
            new DialogButton("Cancel", CancelCommand) { IsCancel = true }
        ];
        ErrorsChanged += (_, _) => OnPropertyChanged(nameof(ErrorSummary));
    }

    [Required(ErrorMessage = "Enter a title.")]
    [MaxLength(60, ErrorMessage = "Use at most 60 characters.")]
    public string NoteTitle
    {
        get => noteTitle;
        set => SetProperty(ref noteTitle, value, validate: true);
    }

    [Required(ErrorMessage = "Enter a distance.")]
    [CustomValidation(typeof(TrailNoteFormViewModel), nameof(ValidateDistance))]
    public string Distance
    {
        get => distance;
        set => SetProperty(ref distance, value, validate: true);
    }

    public string ErrorSummary => string.Join(Environment.NewLine, GetErrors().Select(error => error.ErrorMessage));

    public override IReadOnlyList<DialogButton> Buttons { get; }


    public static ValidationResult? ValidateDistance(string value, ValidationContext context) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double kilometers) && kilometers is > 0 and <= 500
            ? ValidationResult.Success
            : new ValidationResult("Enter a distance between 0 and 500 km.");

    protected override void OnAccepted()
    {
        note.Title = NoteTitle;
        note.DistanceKilometers = double.Parse(Distance, System.Globalization.CultureInfo.CurrentCulture);
    }
}

/// <summary>Drives the Dialogs page with fictional notes.</summary>
public sealed partial class DialogsViewModel : ObservableObject
{
    private readonly IDialogService dialogs;
    private readonly IFileDialogService files;
    private readonly TrailNote note = new("Fog over the quarry", 6.4);

    public DialogsViewModel(IDialogService dialogs, IFileDialogService files)
    {
        this.dialogs = dialogs;
        this.files = files;
        NoteSummary = Describe(note);
    }

    [ObservableProperty]
    public partial string LastResult { get; set; } = "No dialog shown yet.";

    [ObservableProperty]
    public partial string NoteSummary { get; set; } = string.Empty;

    [RelayCommand]
    private async Task ShowMessageAsync()
    {
        await dialogs.ShowMessageAsync("Sync finished", "12 field notes were copied to the shared folder.");
        LastResult = "The message was closed.";
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        bool deleted = await dialogs.ConfirmAsync("Delete this note?", "\"Heron at dawn\" will be deleted. You can't undo this.", "Delete", "Keep", isDestructive: true);
        LastResult = deleted ? "Delete was chosen." : "Keep was chosen. Enter and Escape both keep the note.";
    }

    [RelayCommand]
    private async Task ReviewImportAsync()
    {
        string content = string.Join(Environment.NewLine, Enumerable.Range(1, 40).Select(i => $"Note {i}: Granite Ridge, {i * 0.4:0.0} km, imported from the field tablet"));
        bool approved = await dialogs.ReviewAsync("Review import", content, "Import 40 notes");
        LastResult = approved ? "The import was approved." : "The import was cancelled.";
    }

    [RelayCommand]
    private async Task EditNoteAsync()
    {
        bool saved = await dialogs.ShowAsync(new TrailNoteFormViewModel(note));
        NoteSummary = Describe(note);
        LastResult = saved ? "The note was saved." : "The edit was cancelled and nothing changed.";
    }

    [RelayCommand]
    private void PickFiles()
    {
        var picked = files.PickFiles("Text files|*.txt|All files|*.*");
        LastResult = picked.Count == 0 ? "No files were picked." : "Picked " + string.Join(", ", picked.Select(Path.GetFileName));
    }

    [RelayCommand]
    private void PickFolder() => LastResult = files.PickFolder() is { } folder ? "Picked " + folder : "No folder was picked.";

    [RelayCommand]
    private void PickSaveFile() =>
        LastResult = files.PickSaveFile("Text files|*.txt", "Trail notes.txt") is { } path ? "Would save to " + path : "Saving was cancelled.";

    private static string Describe(TrailNote note) => $"{note.Title}, {note.DistanceKilometers:0.0} km";
}
