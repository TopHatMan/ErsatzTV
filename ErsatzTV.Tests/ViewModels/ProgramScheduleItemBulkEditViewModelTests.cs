using ErsatzTV.Application.Filler;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Domain.Filler;
using ErsatzTV.ViewModels;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.ViewModels;

[TestFixture]
public class ProgramScheduleItemBulkEditViewModelTests
{
    [Test]
    public void Should_Apply_Only_Enabled_Fields()
    {
        FillerPresetViewModel originalPreRoll = Filler(1, "Original", FillerKind.PreRoll);
        FillerPresetViewModel replacementPreRoll = Filler(2, "Replacement", FillerKind.PreRoll);
        var first = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.TelevisionShow,
            PlaybackOrder = PlaybackOrder.Random,
            GuideMode = GuideMode.Normal,
            PreRollFiller = originalPreRoll
        };
        var second = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.TelevisionShow,
            PlaybackOrder = PlaybackOrder.Shuffle,
            GuideMode = GuideMode.Normal,
            PreRollFiller = originalPreRoll
        };

        var bulkEdit = new ProgramScheduleItemBulkEditViewModel
        {
            ApplyPlaybackOrder = true,
            PlaybackOrder = PlaybackOrder.RotateStart,
            ApplyPreRollFiller = true,
            PreRollFiller = replacementPreRoll,
            GuideMode = GuideMode.Filler
        };

        bulkEdit.ApplyTo([first, second]);

        first.PlaybackOrder.ShouldBe(PlaybackOrder.RotateStart);
        second.PlaybackOrder.ShouldBe(PlaybackOrder.RotateStart);
        first.PreRollFiller.ShouldBe(replacementPreRoll);
        second.PreRollFiller.ShouldBe(replacementPreRoll);
        first.GuideMode.ShouldBe(GuideMode.Normal);
        second.GuideMode.ShouldBe(GuideMode.Normal);
    }

    [Test]
    public void Should_Clear_A_Filler_When_Enabled_With_No_Selection()
    {
        FillerPresetViewModel fallback = Filler(3, "Fallback", FillerKind.Fallback);
        var item = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.Collection,
            FallbackFiller = fallback
        };

        var bulkEdit = new ProgramScheduleItemBulkEditViewModel
        {
            ApplyFallbackFiller = true,
            FallbackFiller = null
        };

        bulkEdit.ApplyTo([item]);

        item.FallbackFiller.ShouldBeNull();
    }

    [Test]
    public void Should_Return_Only_Playback_Orders_Common_To_All_Selected_Items()
    {
        var collectionItem = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.Collection
        };
        var showItem = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.TelevisionShow
        };

        IReadOnlyList<PlaybackOrder> result =
            ProgramScheduleItemBulkEditViewModel.CommonPlaybackOrders([collectionItem, showItem]);

        result.ShouldBe(
            [
                PlaybackOrder.Chronological,
                PlaybackOrder.RotateStart,
                PlaybackOrder.Random,
                PlaybackOrder.Shuffle
            ]);
    }

    [Test]
    public void Should_Disable_Playback_Order_For_Playlist_Selections()
    {
        var playlistItem = new ProgramScheduleItemEditViewModel
        {
            CollectionType = CollectionType.Playlist
        };

        ProgramScheduleItemBulkEditViewModel.CommonPlaybackOrders([playlistItem]).ShouldBeEmpty();
    }

    [Test]
    public void Should_Allow_Tail_Filler_Only_When_All_Items_Use_Duration_Filler_Tails()
    {
        var valid = new ProgramScheduleItemEditViewModel
        {
            PlayoutMode = PlayoutMode.Duration,
            TailMode = TailMode.Filler
        };
        var invalid = new ProgramScheduleItemEditViewModel
        {
            PlayoutMode = PlayoutMode.One,
            TailMode = TailMode.None
        };

        ProgramScheduleItemBulkEditViewModel.CanEditTailFiller([valid]).ShouldBeTrue();
        ProgramScheduleItemBulkEditViewModel.CanEditTailFiller([valid, invalid]).ShouldBeFalse();
    }

    private static FillerPresetViewModel Filler(int id, string name, FillerKind kind) => new(
        id,
        name,
        kind,
        default,
        null,
        null,
        null,
        false,
        default,
        null,
        null,
        null,
        null,
        null,
        null,
        false);
}
