namespace Remote.Adb.Desktop.Emulators;

/// <summary>A selectable form-factor in the create wizard (e.g. key <c>phone</c>, label "Phone").</summary>
public sealed record FormFactorOption(string Key, string Label);
