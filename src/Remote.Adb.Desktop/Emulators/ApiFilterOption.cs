namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// A selectable API-level filter in the create wizard. Carries the numeric <see cref="ApiLevel"/> (null = "all
/// levels") so filtering keys off the value rather than a reconstructed display string; <see cref="ToString"/>
/// supplies the dropdown label.
/// </summary>
public sealed record ApiFilterOption(int? ApiLevel, string Label)
{
    public override string ToString() => Label;
}
