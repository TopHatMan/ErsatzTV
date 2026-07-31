#!/usr/bin/env python3
from pathlib import Path

ROOT = Path.cwd()

def replace_once(relative_path: str, old: str, new: str) -> None:
    path = ROOT / relative_path
    text = path.read_text(encoding="utf-8-sig")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match in {relative_path}, found {count}")
    path.write_text(text.replace(old, new), encoding="utf-8")

def create_new(relative_path: str, content: str) -> None:
    path = ROOT / relative_path
    if path.exists():
        raise RuntimeError(f"Refusing to overwrite existing file: {relative_path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")

replace_once(
    "ErsatzTV.Core/Domain/PlaybackOrder.cs",
    "    RandomRotation = 7,\n    Marathon = 8\n",
    "    RandomRotation = 7,\n    Marathon = 8,\n    RotateStart = 9\n",
)

create_new(
    "ErsatzTV.Core/Scheduling/RotatingStartMediaCollectionEnumerator.cs",
    'using ErsatzTV.Core.Domain;\nusing ErsatzTV.Core.Extensions;\nusing ErsatzTV.Core.Interfaces.Scheduling;\n\nnamespace ErsatzTV.Core.Scheduling;\n\n/// <summary>\n/// Plays every item in order from a deterministic rotated starting point.\n/// After completing a full cycle, a new starting point is selected while\n/// preserving the same ordered sequence and avoiding the previous start.\n/// </summary>\npublic sealed class RotatingStartMediaCollectionEnumerator : IMediaCollectionEnumerator\n{\n    private readonly Lazy<Option<TimeSpan>> _lazyMinimumDuration;\n    private readonly List<MediaItem> _sortedMediaItems;\n\n    public RotatingStartMediaCollectionEnumerator(\n        IEnumerable<MediaItem> mediaItems,\n        CollectionEnumeratorState state,\n        bool seasonEpisodeOrder)\n    {\n        CurrentIncludeInProgramGuide = Option<bool>.None;\n\n        if (seasonEpisodeOrder)\n        {\n            _sortedMediaItems = SeasonEpisodeMediaCollectionEnumerator.Playable(mediaItems)\n                .OrderBy(identity, new SeasonEpisodeMediaComparer())\n                .ToList();\n        }\n        else\n        {\n            _sortedMediaItems = mediaItems\n                .OrderBy(identity, new ChronologicalMediaComparer())\n                .ToList();\n        }\n\n        _lazyMinimumDuration = new Lazy<Option<TimeSpan>>(() =>\n            _sortedMediaItems.Bind(i => i.GetNonZeroDuration()).OrderBy(identity).HeadOrNone());\n\n        State = new CollectionEnumeratorState\n        {\n            Seed = state.Seed,\n            Index = state.Index,\n            Started = state.Started\n        };\n\n        NormalizeState();\n    }\n\n    public string SchedulingContextName => "Rotate Start, Play in Order";\n\n    public CollectionEnumeratorState State { get; }\n\n    public Option<MediaItem> Current =>\n        _sortedMediaItems.Count == 0 ? None : _sortedMediaItems[State.Index];\n\n    public Option<bool> CurrentIncludeInProgramGuide { get; }\n\n    public Option<TimeSpan> MinimumDuration => _lazyMinimumDuration.Value;\n\n    public int Count => _sortedMediaItems.Count;\n\n    public void MoveNext(Option<DateTimeOffset> scheduledAt)\n    {\n        if (_sortedMediaItems.Count == 0)\n        {\n            return;\n        }\n\n        int cycleStart = StartIndexForSeed(State.Seed);\n        int nextIndex = (State.Index + 1) % _sortedMediaItems.Count;\n\n        if (State.Started && nextIndex == cycleStart)\n        {\n            State.Seed = NextCycleSeed(State.Seed);\n            State.Index = StartIndexForSeed(State.Seed);\n        }\n        else\n        {\n            State.Index = nextIndex;\n        }\n\n        State.Started = true;\n    }\n\n    public void ResetState(CollectionEnumeratorState state)\n    {\n        State.Seed = state.Seed;\n        State.Started = state.Started;\n        State.Index = _sortedMediaItems.Count == 0\n            ? 0\n            : Math.Clamp(state.Index, 0, _sortedMediaItems.Count - 1);\n    }\n\n    private void NormalizeState()\n    {\n        if (_sortedMediaItems.Count == 0)\n        {\n            State.Index = 0;\n            return;\n        }\n\n        if (!State.Started || State.Index < 0 || State.Index >= _sortedMediaItems.Count)\n        {\n            State.Index = StartIndexForSeed(State.Seed);\n        }\n    }\n\n    private int StartIndexForSeed(int seed) =>\n        (int)((uint)seed % (uint)_sortedMediaItems.Count);\n\n    private int NextCycleSeed(int seed)\n    {\n        if (_sortedMediaItems.Count < 2)\n        {\n            return seed;\n        }\n\n        int previousStart = StartIndexForSeed(seed);\n        int nextSeed = AdvanceSeed(seed);\n\n        while (StartIndexForSeed(nextSeed) == previousStart)\n        {\n            nextSeed = unchecked(nextSeed + 1);\n        }\n\n        return nextSeed;\n    }\n\n    private static int AdvanceSeed(int seed) =>\n        unchecked((int)((uint)seed * 1664525u + 1013904223u));\n}\n',
)

replace_once(
    "ErsatzTV.Core/Scheduling/PlayoutBuilder.cs",
    """        switch (playbackOrder)
        {
            case PlaybackOrder.Chronological:
""",
    """        switch (playbackOrder)
        {
            case PlaybackOrder.RotateStart:
                return new RotatingStartMediaCollectionEnumerator(
                    mediaItems,
                    state,
                    collectionKey.CollectionType is CollectionType.TelevisionShow or CollectionType.TelevisionSeason);
            case PlaybackOrder.Chronological:
""",
)

replace_once(
    "ErsatzTV.Application/ProgramSchedules/Commands/ProgramScheduleItemCommandBase.cs",
    """                case PlaybackOrder.SeasonEpisode:
                case PlaybackOrder.RandomRotation:
                    return BaseError.New($"Invalid playback order for multi collection: '{item.PlaybackOrder}'");
""",
    """                case PlaybackOrder.SeasonEpisode:
                case PlaybackOrder.RandomRotation:
                case PlaybackOrder.RotateStart:
                    return BaseError.New($"Invalid playback order for multi collection: '{item.PlaybackOrder}'");
""",
)

replace_once(
    "ErsatzTV/Pages/ScheduleItemsEditor.razor",
    """                case CollectionType.Collection:
                case CollectionType.SmartCollection:
                case CollectionType.SearchQuery:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
    """                case CollectionType.Collection:
                case CollectionType.SmartCollection:
                case CollectionType.SearchQuery:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.RotateStart">Rotate Start, Play in Order</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
)

replace_once(
    "ErsatzTV/Pages/ScheduleItemsEditor.razor",
    """                case CollectionType.TelevisionShow:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.SeasonEpisode">Season, Episode</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
    """                case CollectionType.TelevisionShow:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.SeasonEpisode">Season, Episode</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.RotateStart">Rotate Start, Play in Order</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
)

replace_once(
    "ErsatzTV/Pages/ScheduleItemsEditor.razor",
    """                default:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
    """                default:
                    <MudSelectItem Value="PlaybackOrder.Chronological">Chronological</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.RotateStart">Rotate Start, Play in Order</MudSelectItem>
                    <MudSelectItem Value="PlaybackOrder.Random">Random</MudSelectItem>
""",
)

create_new(
    "ErsatzTV.Core.Tests/Scheduling/RotatingStartContentTests.cs",
    'using ErsatzTV.Core.Domain;\nusing ErsatzTV.Core.Scheduling;\nusing NUnit.Framework;\nusing Shouldly;\n\nnamespace ErsatzTV.Core.Tests.Scheduling;\n\n[TestFixture]\npublic class RotatingStartContentTests\n{\n    [Test]\n    public void Should_Play_In_Season_Episode_Order_From_Rotated_Start()\n    {\n        var state = new CollectionEnumeratorState { Seed = 1 };\n        var enumerator = new RotatingStartMediaCollectionEnumerator(Episodes(5), state, true);\n\n        TakeIds(enumerator, 5).ShouldBe([2, 3, 4, 5, 1]);\n    }\n\n    [Test]\n    public void Should_Select_A_Different_Start_After_Each_Complete_Cycle()\n    {\n        var state = new CollectionEnumeratorState { Seed = 1 };\n        var enumerator = new RotatingStartMediaCollectionEnumerator(Episodes(5), state, true);\n\n        List<int> firstCycle = TakeIds(enumerator, 5);\n        List<int> secondCycle = TakeIds(enumerator, 5);\n\n        secondCycle[0].ShouldNotBe(firstCycle[0]);\n        secondCycle.Distinct().Count().ShouldBe(5);\n\n        for (var i = 1; i < secondCycle.Count; i++)\n        {\n            int expected = secondCycle[i - 1] == 5 ? 1 : secondCycle[i - 1] + 1;\n            secondCycle[i].ShouldBe(expected);\n        }\n    }\n\n    [Test]\n    public void Should_Restore_The_Current_Cycle_And_Episode_From_State()\n    {\n        var original = new RotatingStartMediaCollectionEnumerator(\n            Episodes(5),\n            new CollectionEnumeratorState { Seed = 1 },\n            true);\n\n        for (var i = 0; i < 7; i++)\n        {\n            original.MoveNext(Option<DateTimeOffset>.None);\n        }\n\n        CollectionEnumeratorState saved = original.State.Clone();\n        var restored = new RotatingStartMediaCollectionEnumerator(Episodes(5), saved, true);\n\n        restored.State.Seed.ShouldBe(saved.Seed);\n        restored.State.Index.ShouldBe(saved.Index);\n        restored.State.Started.ShouldBe(saved.Started);\n        restored.Current.Map(x => x.Id).IfNone(-1)\n            .ShouldBe(original.Current.Map(x => x.Id).IfNone(-1));\n    }\n\n    [Test]\n    public void Reset_State_Should_Respect_An_Explicit_Index()\n    {\n        var enumerator = new RotatingStartMediaCollectionEnumerator(\n            Episodes(5),\n            new CollectionEnumeratorState { Seed = 1 },\n            true);\n\n        enumerator.ResetState(new CollectionEnumeratorState { Seed = 1, Index = 4 });\n\n        enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(5);\n        enumerator.State.Index.ShouldBe(4);\n    }\n\n    [Test]\n    public void Single_Item_Should_Remain_Stable()\n    {\n        var enumerator = new RotatingStartMediaCollectionEnumerator(\n            Episodes(1),\n            new CollectionEnumeratorState { Seed = 17 },\n            true);\n\n        for (var i = 0; i < 10; i++)\n        {\n            enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(1);\n            enumerator.MoveNext(Option<DateTimeOffset>.None);\n        }\n    }\n\n    private static List<int> TakeIds(RotatingStartMediaCollectionEnumerator enumerator, int count)\n    {\n        var result = new List<int>();\n\n        for (var i = 0; i < count; i++)\n        {\n            result.Add(enumerator.Current.Map(x => x.Id).IfNone(-1));\n            enumerator.MoveNext(Option<DateTimeOffset>.None);\n        }\n\n        return result;\n    }\n\n    private static List<MediaItem> Episodes(int count) =>\n        Range(1, count).Map(i => (MediaItem)new Episode\n            {\n                Id = i,\n                EpisodeMetadata =\n                [\n                    new EpisodeMetadata\n                    {\n                        ReleaseDate = new DateTime(2020, 1, count + 1 - i),\n                        EpisodeNumber = i\n                    }\n                ],\n                Season = new Season { SeasonNumber = 1 }\n            })\n            .Reverse()\n            .ToList();\n}\n',
)

print("Applied Rotate Start, Play in Order")
