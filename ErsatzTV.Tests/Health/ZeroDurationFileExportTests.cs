using ErsatzTV.Application.Health;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Health;

[TestFixture]
public class ZeroDurationFileExportTests
{
    [Test]
    public void ToCsv_Escapes_Commas_And_Quotes()
    {
        var files = new List<ZeroDurationFileViewModel>
        {
            new(1, "Movie", "Foo, \"Bar\"", @"D:\Shows\bad.mkv", "Movies", "Normal")
        };

        string csv = ZeroDurationFileExport.ToCsv(files);

        csv.ShouldContain("MediaItemId,Kind,Title,Path,Location,Library,State");
        csv.ShouldContain("1,Movie,\"Foo, \"\"Bar\"\"\",D:\\Shows\\bad.mkv,D:\\,Movies,Normal");
    }

    [Test]
    public void ToPathList_Writes_One_Path_Per_Line()
    {
        var files = new List<ZeroDurationFileViewModel>
        {
            new(1, "Movie", "A", @"D:\a.mkv", "Movies", "Normal"),
            new(2, "Episode", "B", @"E:\b.mkv", "TV", "Normal")
        };

        string list = ZeroDurationFileExport.ToPathList(files);
        list.ShouldBe($"D:\\a.mkv{Environment.NewLine}E:\\b.mkv{Environment.NewLine}");
    }
}
