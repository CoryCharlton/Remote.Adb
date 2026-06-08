using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// The result of building the AVD field model: the grouped <see cref="DetailGroup"/>s for display, plus the
/// flat list of every <see cref="AvdField"/> for save/validation.
/// </summary>
public sealed record GroupedFields(IReadOnlyList<DetailGroup> Groups, IReadOnlyList<AvdField> Fields);
