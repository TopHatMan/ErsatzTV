using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Scheduling;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Core.Tests.Scheduling;

[TestFixture]
public class RotatingStartContentTests
{
    [Test]
    public void Should_Play_In_Season_Episode_Order_From_Rotated_Start()
    {
        var state = new CollectionEnumeratorState { Seed = 1 };
        var enumerator = new RotatingStartMediaCollectionEnumerator(Episodes(5), state, true);

        TakeIds(enumerator, 5).ShouldBe([2, 3, 4, 5, 1]);
    }

    [Test]
    public void Should_Select_A_Different_Start_After_Each_Complete_Cycle()
    {
        var state = new CollectionEnumeratorState { Seed = 1 };
        var enumerator = new RotatingStartMediaCollectionEnumerator(Episodes(5), state, true);

        List<int> firstCycle = TakeIds(enumerator, 5);
        List<int> secondCycle = TakeIds(enumerator, 5);

        secondCycle[0].ShouldNotBe(firstCycle[0]);
        secondCycle.Distinct().Count().ShouldBe(5);

        for (var i = 1; i < secondCycle.Count; i++)
        {
            int expected = secondCycle[i - 1] == 5 ? 1 : secondCycle[i - 1] + 1;
            secondCycle[i].ShouldBe(expected);
        }
    }

    [Test]
    public void Should_Restore_The_Current_Cycle_And_Episode_From_State()
    {
        var original = new RotatingStartMediaCollectionEnumerator(
            Episodes(5),
            new CollectionEnumeratorState { Seed = 1 },
            true);

        for (var i = 0; i < 7; i++)
        {
            original.MoveNext(Option<DateTimeOffset>.None);
        }

        CollectionEnumeratorState saved = original.State.Clone();
        var restored = new RotatingStartMediaCollectionEnumerator(Episodes(5), saved, true);

        restored.State.Seed.ShouldBe(saved.Seed);
        restored.State.Index.ShouldBe(saved.Index);
        restored.State.Started.ShouldBe(saved.Started);
        restored.Current.Map(x => x.Id).IfNone(-1)
            .ShouldBe(original.Current.Map(x => x.Id).IfNone(-1));
    }

    [Test]
    public void Reset_State_Should_Respect_An_Explicit_Index_And_Cycle_Boundary()
    {
        var enumerator = new RotatingStartMediaCollectionEnumerator(
            Episodes(5),
            new CollectionEnumeratorState { Seed = 1 },
            true);

        enumerator.ResetState(new CollectionEnumeratorState { Seed = 1, Index = 4 });

        enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(5);
        enumerator.State.Index.ShouldBe(4);
        enumerator.State.Started.ShouldBeTrue();

        enumerator.MoveNext(Option<DateTimeOffset>.None);
        enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(1);

        enumerator.MoveNext(Option<DateTimeOffset>.None);
        enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(4);
        enumerator.State.Seed.ShouldNotBe(1);
    }

    [Test]
    public void Single_Item_Should_Remain_Stable()
    {
        var enumerator = new RotatingStartMediaCollectionEnumerator(
            Episodes(1),
            new CollectionEnumeratorState { Seed = 17 },
            true);

        for (var i = 0; i < 10; i++)
        {
            enumerator.Current.Map(x => x.Id).IfNone(-1).ShouldBe(1);
            enumerator.MoveNext(Option<DateTimeOffset>.None);
        }
    }

    private static List<int> TakeIds(RotatingStartMediaCollectionEnumerator enumerator, int count)
    {
        var result = new List<int>();

        for (var i = 0; i < count; i++)
        {
            result.Add(enumerator.Current.Map(x => x.Id).IfNone(-1));
            enumerator.MoveNext(Option<DateTimeOffset>.None);
        }

        return result;
    }

    private static List<MediaItem> Episodes(int count) =>
        Range(1, count).Map(i => (MediaItem)new Episode
            {
                Id = i,
                EpisodeMetadata =
                [
                    new EpisodeMetadata
                    {
                        ReleaseDate = new DateTime(2020, 1, count + 1 - i),
                        EpisodeNumber = i
                    }
                ],
                Season = new Season { SeasonNumber = 1 }
            })
            .Reverse()
            .ToList();
}
