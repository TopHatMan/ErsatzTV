#!/usr/bin/env python3
from pathlib import Path

path = Path("ErsatzTV/Pages/ScheduleItemsEditor.razor")
text = path.read_text(encoding="utf-8-sig")


def replace_once(old: str, new: str) -> None:
    global text
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match, found {count}: {old[:120]!r}")
    text = text.replace(old, new)


replace_once(
'''            <MudButton Class="ml-3" Variant="Variant.Filled" Color="Color.Default" OnClick="@AddScheduleItem" StartIcon="@Icons.Material.Filled.PlaylistAdd" Disabled="@(_schedule?.Items is null)">
                Add Schedule Item
            </MudButton>
''',
'''            <MudButton Class="ml-3" Variant="Variant.Filled" Color="Color.Default" OnClick="@AddScheduleItem" StartIcon="@Icons.Material.Filled.PlaylistAdd" Disabled="@(_schedule?.Items is null)">
                Add Schedule Item
            </MudButton>
            <MudButton Class="ml-3"
                       Variant="Variant.Filled"
                       Color="Color.Secondary"
                       OnClick="@OpenBulkEditor"
                       StartIcon="@Icons.Material.Filled.Edit"
                       Disabled="@(_bulkSelectedItems.Count == 0)">
                Bulk Edit (@_bulkSelectedItems.Count)
            </MudButton>
''')

replace_once(
'''                <MudMenuItem Icon="@Icons.Material.Filled.PlaylistAdd" Label="Add Schedule Item" OnClick="@AddScheduleItem"/>
''',
'''                <MudMenuItem Icon="@Icons.Material.Filled.PlaylistAdd" Label="Add Schedule Item" OnClick="@AddScheduleItem"/>
                <MudMenuItem Icon="@Icons.Material.Filled.Edit"
                             Label="@($"Bulk Edit ({_bulkSelectedItems.Count})")"
                             OnClick="@OpenBulkEditor"
                             Disabled="@(_bulkSelectedItems.Count == 0)"/>
''')

replace_once(
'''    <ColGroup>
        <MudHidden Breakpoint="Breakpoint.Xs">
''',
'''    <ColGroup>
        <col style="width: 48px;"/>
        <MudHidden Breakpoint="Breakpoint.Xs">
''')

replace_once(
'''    <HeaderContent>
        <MudTh>Start Time</MudTh>
''',
'''    <HeaderContent>
        <MudTh Style="width: 48px">
            <MudCheckBox T="bool"
                         Value="@AllScheduleItemsSelected"
                         ValueChanged="@ToggleSelectAllScheduleItems"/>
        </MudTh>
        <MudTh>Start Time</MudTh>
''')

replace_once(
'''    <RowTemplate>
        <MudTd DataLabel="Start Time">
''',
'''    <RowTemplate>
        <MudTd DataLabel="Select">
            <MudCheckBox T="bool"
                         Value="@_bulkSelectedItems.Contains(context)"
                         ValueChanged="@(selected => SetScheduleItemSelected(context, selected))"/>
        </MudTd>
        <MudTd DataLabel="Start Time">
''')

bulk_markup = r'''@if (_showBulkEditor)
{
    <MudPaper Class="pa-6 mt-6" Elevation="2">
        <MudText Typo="Typo.h5">Bulk Edit @_bulkSelectedItems.Count Schedule Items</MudText>
        <MudText Typo="Typo.body2" Class="mb-5">
            Only checked settings will change. Content, start times, item order, and schedule item identity are always preserved.
            Click Save Schedule Items after applying these changes.
        </MudText>

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyPlaybackOrder"/>
                <MudText>Playback Order</MudText>
            </div>
            <MudSelect T="PlaybackOrder"
                       @bind-Value="_bulkEdit.PlaybackOrder"
                       Disabled="@(!_bulkEdit.ApplyPlaybackOrder || BulkPlaybackOrders.Count == 0)">
                @foreach (PlaybackOrder playbackOrder in BulkPlaybackOrders)
                {
                    <MudSelectItem Value="@playbackOrder">@PlaybackOrderLabel(playbackOrder)</MudSelectItem>
                }
            </MudSelect>
        </MudStack>
        @if (BulkPlaybackOrders.Count == 0)
        {
            <MudText Typo="Typo.caption" Class="mb-5">
                The selected schedule items do not share a compatible playback order.
            </MudText>
        }

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyGuideMode"/>
                <MudText>Guide Mode</MudText>
            </div>
            <MudSelect T="GuideMode" @bind-Value="_bulkEdit.GuideMode" Disabled="@(!_bulkEdit.ApplyGuideMode)">
                <MudSelectItem Value="GuideMode.Normal">Normal</MudSelectItem>
                <MudSelectItem Value="GuideMode.Filler">Filler</MudSelectItem>
            </MudSelect>
        </MudStack>

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyPreRollFiller"/>
                <MudText>Pre-Roll Filler</MudText>
            </div>
            <MudSelect T="FillerPresetViewModel"
                       @bind-Value="_bulkEdit.PreRollFiller"
                       Disabled="@(!_bulkEdit.ApplyPreRollFiller)"
                       Clearable="true">
                @foreach (FillerPresetViewModel filler in _fillerPresets.Where(f => f.FillerKind == FillerKind.PreRoll))
                {
                    <MudSelectItem Value="@filler">@filler.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyMidRollFiller"/>
                <MudText>Mid-Roll Filler</MudText>
            </div>
            <MudSelect T="FillerPresetViewModel"
                       @bind-Value="_bulkEdit.MidRollFiller"
                       Disabled="@(!_bulkEdit.ApplyMidRollFiller)"
                       Clearable="true">
                @foreach (FillerPresetViewModel filler in _fillerPresets.Where(f => f.FillerKind == FillerKind.MidRoll))
                {
                    <MudSelectItem Value="@filler">@filler.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyPostRollFiller"/>
                <MudText>Post-Roll Filler</MudText>
            </div>
            <MudSelect T="FillerPresetViewModel"
                       @bind-Value="_bulkEdit.PostRollFiller"
                       Disabled="@(!_bulkEdit.ApplyPostRollFiller)"
                       Clearable="true">
                @foreach (FillerPresetViewModel filler in _fillerPresets.Where(f => f.FillerKind == FillerKind.PostRoll))
                {
                    <MudSelectItem Value="@filler">@filler.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool"
                             @bind-Value="_bulkEdit.ApplyTailFiller"
                             Disabled="@(!CanBulkEditTailFiller)"/>
                <MudText>Tail Filler</MudText>
            </div>
            <MudSelect T="FillerPresetViewModel"
                       @bind-Value="_bulkEdit.TailFiller"
                       Disabled="@(!_bulkEdit.ApplyTailFiller || !CanBulkEditTailFiller)"
                       Clearable="true">
                @foreach (FillerPresetViewModel filler in _fillerPresets.Where(f => f.FillerKind == FillerKind.Tail))
                {
                    <MudSelectItem Value="@filler">@filler.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>
        @if (!CanBulkEditTailFiller)
        {
            <MudText Typo="Typo.caption" Class="mb-5">
                Tail filler can only be changed when every selected item uses Duration mode with Tail Mode set to Filler.
            </MudText>
        }

        <MudStack Row="true" Breakpoint="Breakpoint.SmAndDown" Class="form-field-stack gap-md-8 mb-5">
            <div class="d-flex align-center">
                <MudCheckBox T="bool" @bind-Value="_bulkEdit.ApplyFallbackFiller"/>
                <MudText>Fallback Filler</MudText>
            </div>
            <MudSelect T="FillerPresetViewModel"
                       @bind-Value="_bulkEdit.FallbackFiller"
                       Disabled="@(!_bulkEdit.ApplyFallbackFiller)"
                       Clearable="true">
                @foreach (FillerPresetViewModel filler in _fillerPresets.Where(f => f.FillerKind == FillerKind.Fallback))
                {
                    <MudSelectItem Value="@filler">@filler.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>

        <div class="d-flex mt-6">
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       OnClick="@ApplyBulkEdit"
                       Disabled="@(!BulkEditHasChanges)">
                Apply to Selected Items
            </MudButton>
            <MudButton Class="ml-3" Variant="Variant.Text" OnClick="@CancelBulkEdit">Cancel</MudButton>
        </div>
    </MudPaper>
}
'''

replace_once(
'''</MudTable>
@if (_selectedItem is not null)
''',
'''</MudTable>
''' + bulk_markup + '''@if (_selectedItem is not null)
''')

replace_once(
'''    private MudForm _form;
    private bool _success;
''',
'''    private MudForm _form;
    private bool _success;
    private readonly HashSet<ProgramScheduleItemEditViewModel> _bulkSelectedItems = [];
    private ProgramScheduleItemBulkEditViewModel _bulkEdit = new();
    private bool _showBulkEditor;

    private bool AllScheduleItemsSelected =>
        _schedule?.Items is { Count: > 0 } items && items.All(_bulkSelectedItems.Contains);

    private IReadOnlyList<PlaybackOrder> BulkPlaybackOrders =>
        ProgramScheduleItemBulkEditViewModel.CommonPlaybackOrders(_bulkSelectedItems);

    private bool CanBulkEditTailFiller =>
        ProgramScheduleItemBulkEditViewModel.CanEditTailFiller(_bulkSelectedItems);

    private bool BulkEditHasChanges =>
        _bulkEdit.ApplyPlaybackOrder ||
        _bulkEdit.ApplyGuideMode ||
        _bulkEdit.ApplyPreRollFiller ||
        _bulkEdit.ApplyMidRollFiller ||
        _bulkEdit.ApplyPostRollFiller ||
        _bulkEdit.ApplyTailFiller ||
        _bulkEdit.ApplyFallbackFiller;
''')

replace_once(
'''                _schedule = new ProgramScheduleItemsEditViewModel
                {
                    Name = name,
                    ShuffleScheduleItems = shuffleScheduleItems,
                    Items = items.Map(ProjectToEditViewModel).ToList()
                };

                if (_schedule.Items.Count == 1)
''',
'''                _schedule = new ProgramScheduleItemsEditViewModel
                {
                    Name = name,
                    ShuffleScheduleItems = shuffleScheduleItems,
                    Items = items.Map(ProjectToEditViewModel).ToList()
                };
                _bulkSelectedItems.Clear();
                _showBulkEditor = false;

                if (_schedule.Items.Count == 1)
''')

replace_once(
'''    private void RemoveScheduleItem(ProgramScheduleItemEditViewModel item)
    {
        _selectedItem = null;
        _schedule.Items.Remove(item);
    }
''',
'''    private void RemoveScheduleItem(ProgramScheduleItemEditViewModel item)
    {
        _selectedItem = null;
        _bulkSelectedItems.Remove(item);
        _schedule.Items.Remove(item);
        if (_bulkSelectedItems.Count == 0)
        {
            _showBulkEditor = false;
        }
    }
''')

bulk_methods = r'''    private void ToggleSelectAllScheduleItems(bool selected)
    {
        _bulkSelectedItems.Clear();
        if (selected && _schedule?.Items is not null)
        {
            _bulkSelectedItems.UnionWith(_schedule.Items);
        }

        if (_bulkSelectedItems.Count == 0)
        {
            _showBulkEditor = false;
        }
    }

    private void SetScheduleItemSelected(ProgramScheduleItemEditViewModel item, bool selected)
    {
        if (selected)
        {
            _bulkSelectedItems.Add(item);
        }
        else
        {
            _bulkSelectedItems.Remove(item);
        }

        if (_bulkSelectedItems.Count == 0)
        {
            _showBulkEditor = false;
        }
    }

    private void OpenBulkEditor()
    {
        if (_bulkSelectedItems.Count == 0)
        {
            return;
        }

        _bulkEdit = new ProgramScheduleItemBulkEditViewModel();
        IReadOnlyList<PlaybackOrder> playbackOrders = BulkPlaybackOrders;
        if (playbackOrders.Count > 0)
        {
            _bulkEdit.PlaybackOrder = playbackOrders[0];
        }

        _showBulkEditor = true;
    }

    private void ApplyBulkEdit()
    {
        List<ProgramScheduleItemEditViewModel> selected = _bulkSelectedItems
            .Where(item => _schedule.Items.Contains(item))
            .ToList();
        if (selected.Count == 0)
        {
            _showBulkEditor = false;
            return;
        }

        IReadOnlyList<PlaybackOrder> playbackOrders =
            ProgramScheduleItemBulkEditViewModel.CommonPlaybackOrders(selected);
        if (_bulkEdit.ApplyPlaybackOrder && !playbackOrders.Contains(_bulkEdit.PlaybackOrder))
        {
            Snackbar.Add("The selected schedule items do not share that playback order.", Severity.Error);
            return;
        }

        if (_bulkEdit.ApplyTailFiller &&
            !ProgramScheduleItemBulkEditViewModel.CanEditTailFiller(selected))
        {
            Snackbar.Add(
                "Tail filler requires all selected items to use Duration mode with Tail Mode set to Filler.",
                Severity.Error);
            return;
        }

        _bulkEdit.ApplyTo(selected);
        _showBulkEditor = false;
        Snackbar.Add(
            $"Updated {selected.Count} schedule items. Click Save Schedule Items to persist the changes.",
            Severity.Success);
    }

    private void CancelBulkEdit() => _showBulkEditor = false;

    private static string PlaybackOrderLabel(PlaybackOrder playbackOrder) => playbackOrder switch
    {
        PlaybackOrder.RotateStart => "Random Start, Play in Order",
        PlaybackOrder.SeasonEpisode => "Season, Episode",
        PlaybackOrder.ShuffleInOrder => "Shuffle In Order",
        PlaybackOrder.MultiEpisodeShuffle => "Multi-Episode Shuffle",
        _ => playbackOrder.ToString()
    };

'''

replace_once(
'''    private async Task SelectedItemChanged(ProgramScheduleItemEditViewModel vm)
''',
bulk_methods + '''    private async Task SelectedItemChanged(ProgramScheduleItemEditViewModel vm)
''')

path.write_text(text, encoding="utf-8")
print("Applied schedule item bulk editing")
