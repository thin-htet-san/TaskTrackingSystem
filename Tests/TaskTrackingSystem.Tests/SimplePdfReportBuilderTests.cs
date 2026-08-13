using System.Text;
using TaskTrackingSystem.Shared;
using Xunit;

namespace TaskTrackingSystem.Tests;

public sealed class SimplePdfReportBuilderTests
{
    [Fact]
    public void BuildTableReport_GeneratesPdfForBurmeseText()
    {
        var pdf = SimplePdfReportBuilder.BuildTableReport(
            "လုပ်ငန်း အစီရင်ခံစာ",
            ["ရှာဖွေရန်: လုပ်ငန်းအားလုံး"],
            ["လုပ်ငန်း", "စီမံကိန်း", "အခြေအနေ"],
            [["မြန်မာစာ စမ်းသပ်လုပ်ငန်း", "စီမံကိန်းတစ်", "လုပ်ဆောင်ဆဲ"]]);

        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
