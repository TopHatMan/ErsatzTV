using ErsatzTV.Core.Health;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Core.Tests.Health;

[TestFixture]
public class MediaPathHelperTests
{
    [Test]
    public void GetLocation_Unknown_For_Blank_Paths()
    {
        MediaPathHelper.GetLocation(null!).ShouldBe("(unknown)");
        MediaPathHelper.GetLocation(string.Empty).ShouldBe("(unknown)");
        MediaPathHelper.GetLocation("   ").ShouldBe("(unknown)");
    }

    [TestCase(@"D:\Shows\Broken\episode.mkv", @"D:\")]
    [TestCase(@"e:\Movies\bad.mkv", @"E:\")]
    [TestCase(@"C:\", @"C:\")]
    public void GetLocation_Windows_Drive_Root(string path, string expected)
    {
        MediaPathHelper.GetLocation(path).ShouldBe(expected);
    }

    [TestCase(@"\\nas\media\TV\show.mkv", @"\\nas\media")]
    [TestCase(@"//nas/media/Movies/bad.mkv", @"\\nas\media")]
    public void GetLocation_Unc_Share(string path, string expected)
    {
        MediaPathHelper.GetLocation(path).ShouldBe(expected);
    }

    [TestCase("/mnt/disk1/tv/show.mkv", "/mnt/disk1")]
    [TestCase("/media/bad-drive/movies/x.mkv", "/media/bad-drive")]
    [TestCase("/tv", "/tv")]
    [TestCase("/", "/")]
    public void GetLocation_Unix_First_Two_Segments(string path, string expected)
    {
        MediaPathHelper.GetLocation(path).ShouldBe(expected);
    }

    [Test]
    public void IsWindowsDriveLocation_Detects_Drive_Letters()
    {
        MediaPathHelper.IsWindowsDriveLocation(@"D:\").ShouldBeTrue();
        MediaPathHelper.IsWindowsDriveLocation(@"\\nas\media").ShouldBeFalse();
        MediaPathHelper.IsWindowsDriveLocation("/mnt/disk1").ShouldBeFalse();
    }
}
