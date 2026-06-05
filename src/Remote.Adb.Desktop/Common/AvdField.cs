using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Remote.Adb.Desktop.Common;

/// <summary>
/// One label → value line in a details pane that flips between read-only display and an editor. Carries the
/// underlying <c>config.ini</c> key so a save can collect just the touched fields, an optional fixed set of
/// <see cref="Choices"/> (rendered as a dropdown), an optional <see cref="ReadText"/> override for pretty
/// read-mode display, and an optional validation rule surfaced inline via <see cref="INotifyDataErrorInfo"/>.
/// Bound directly in templates, so it does not derive from <c>ViewModelBase</c>.
/// </summary>
public partial class AvdField : ObservableValidator
{
    private readonly string? _displayValue;
    private readonly Func<string?, string?>? _validate;
    private string _original;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditor))]
    [NotifyPropertyChangedFor(nameof(ShowChoiceEditor))]
    [NotifyPropertyChangedFor(nameof(ShowTextEditor))]
    [NotifyPropertyChangedFor(nameof(RowVisible))]
    private bool _isEditing;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AvdField), nameof(ValidateValue))]
    [NotifyPropertyChangedFor(nameof(ReadText))]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    [NotifyPropertyChangedFor(nameof(RowVisible))]
    private string? _value;

    public AvdField(
        string key,
        string label,
        string? value,
        bool isReadOnly = false,
        IReadOnlyList<string>? choices = null,
        string? displayValue = null,
        Func<string?, string?>? validate = null)
    {
        Key = key;
        Label = label;
        Choices = choices;
        IsReadOnly = isReadOnly;
        _displayValue = displayValue;
        _validate = validate;
        _original = (value ?? string.Empty).Trim();
        _value = _original;
    }

    public IReadOnlyList<string>? Choices { get; }

    public bool HasChoices => Choices is { Count: > 0 };

    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    public bool IsDirty => !string.Equals((Value ?? string.Empty).Trim(), _original, StringComparison.Ordinal);

    public bool IsReadOnly { get; }

    public string Key { get; }

    public string Label { get; }

    /// <summary>Read-mode text: the pretty display override if supplied, else the raw value.</summary>
    public string? ReadText => string.IsNullOrEmpty(_displayValue) ? Value : _displayValue;

    /// <summary>Visible iff it has something to show (read) or is editable (edit).</summary>
    public bool RowVisible => IsEditing ? !IsReadOnly || HasValue : HasValue;

    public bool ShowChoiceEditor => ShowEditor && HasChoices;

    public bool ShowEditor => IsEditing && !IsReadOnly;

    public bool ShowTextEditor => ShowEditor && !HasChoices;

    /// <summary>Accepts the current value as the new baseline (after a successful save).</summary>
    public void Commit() => _original = (Value ?? string.Empty).Trim();

    /// <summary>Discards edits, restoring the baseline value and clearing any validation errors.</summary>
    public void Reset()
    {
        Value = _original;
        ClearErrors();
    }

    /// <summary>Runs validation; returns whether the field is currently valid.</summary>
    public bool Validate()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    public static ValidationResult? ValidateValue(string? value, ValidationContext context)
    {
        var field = (AvdField) context.ObjectInstance;
        var error = field._validate?.Invoke(value);
        return error is null ? ValidationResult.Success : new ValidationResult(error);
    }
}
