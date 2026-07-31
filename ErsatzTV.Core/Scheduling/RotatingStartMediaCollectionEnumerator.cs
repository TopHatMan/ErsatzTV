using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Extensions;
using ErsatzTV.Core.Interfaces.Scheduling;

namespace ErsatzTV.Core.Scheduling;

/// <summary>
/// Plays every item in order from a seeded pseudo-random starting point.
/// After completing a full cycle, a new starting point is selected from anywhere
/// in the collection while preserving the normal ordered sequence.
/// </summary>
public sealed class RotatingStartMediaCollectionEnumerator : IMediaCollectionEnumerator
{
    private readonly Lazy<Option<TimeSpan>> _lazyMinimumDuration;
    private readonly List<MediaItem> _sortedMediaItems;

    public RotatingStartMediaCollectionEnumerator(
        IEnumerable<MediaItem> mediaItems,
        CollectionEnumeratorState state,
        bool seasonEpisodeOrder)
    {
        CurrentIncludeInProgramGuide = Option<bool>.None;

        if (seasonEpisodeOrder)
        {
            _sortedMediaItems = SeasonEpisodeMediaCollectionEnumerator.Playable(mediaItems)
                .OrderBy(identity, new SeasonEpisodeMediaComparer())
                .ToList();
        }
        else
        {
            _sortedMediaItems = mediaItems
                .OrderBy(identity, new ChronologicalMediaComparer())
                .ToList();
        }

        _lazyMinimumDuration = new Lazy<Option<TimeSpan>>(() =>
            _sortedMediaItems.Bind(i => i.GetNonZeroDuration()).OrderBy(identity).HeadOrNone());

        State = new CollectionEnumeratorState
        {
            Seed = state.Seed,
            Index = state.Index,
            Started = state.Started
        };

        NormalizeState();
    }

    public string SchedulingContextName => "Random Start, Play in Order";

    public CollectionEnumeratorState State { get; }

    public Option<MediaItem> Current =>
        _sortedMediaItems.Count == 0 ? None : _sortedMediaItems[State.Index];

    public Option<bool> CurrentIncludeInProgramGuide { get; }

    public Option<TimeSpan> MinimumDuration => _lazyMinimumDuration.Value;

    public int Count => _sortedMediaItems.Count;

    public void MoveNext(Option<DateTimeOffset> scheduledAt)
    {
        if (_sortedMediaItems.Count == 0)
        {
            return;
        }

        int cycleStart = StartIndexForSeed(State.Seed);
        int nextIndex = (State.Index + 1) % _sortedMediaItems.Count;

        if (State.Started && nextIndex == cycleStart)
        {
            State.Seed = NextCycleSeed(State.Seed);
            State.Index = StartIndexForSeed(State.Seed);
        }
        else
        {
            State.Index = nextIndex;
        }

        State.Started = true;
    }

    public void ResetState(CollectionEnumeratorState state)
    {
        State.Seed = state.Seed;
        State.Started = _sortedMediaItems.Count > 0;
        State.Index = _sortedMediaItems.Count == 0
            ? 0
            : Math.Clamp(state.Index, 0, _sortedMediaItems.Count - 1);
    }

    private void NormalizeState()
    {
        if (_sortedMediaItems.Count == 0)
        {
            State.Index = 0;
            return;
        }

        if (!State.Started || State.Index < 0 || State.Index >= _sortedMediaItems.Count)
        {
            State.Index = StartIndexForSeed(State.Seed);
        }
    }

    private int StartIndexForSeed(int seed) =>
        (int)((uint)seed % (uint)_sortedMediaItems.Count);

    private int NextCycleSeed(int seed)
    {
        if (_sortedMediaItems.Count < 2)
        {
            return seed;
        }

        int previousStart = StartIndexForSeed(seed);
        int nextSeed = AdvanceSeed(seed);

        while (StartIndexForSeed(nextSeed) == previousStart)
        {
            nextSeed = unchecked(nextSeed + 1);
        }

        return nextSeed;
    }

    private static int AdvanceSeed(int seed) =>
        unchecked((int)((uint)seed * 1664525u + 1013904223u));
}
