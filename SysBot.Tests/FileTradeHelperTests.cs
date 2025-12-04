using FluentAssertions;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using Xunit;

namespace SysBot.Tests
{
    public class FileTradeHelperTests
    {
        [Fact]
        public void TestValidFileSize()
        {
            // PK9-宝可梦朱紫
            FileTradeHelper<PK9>.ValidFileSize(344).Should().Be(true);
            FileTradeHelper<PK9>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PK9>.ValidFileSize(345).Should().Be(false);
            FileTradeHelper<PK9>.ValidFileSize(344 * 10).Should().Be(true);
            FileTradeHelper<PK9>.ValidFileSize(344 * 960).Should().Be(true);
            FileTradeHelper<PK9>.ValidFileSize(344 * 961).Should().Be(false);

            // PA8-宝可梦阿尔宙斯
            FileTradeHelper<PA8>.ValidFileSize(376).Should().Be(true);
            FileTradeHelper<PA8>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PA8>.ValidFileSize(375).Should().Be(false);
            FileTradeHelper<PA8>.ValidFileSize(360 * 10).Should().Be(true);
            FileTradeHelper<PA8>.ValidFileSize(360 * 960).Should().Be(true);
            FileTradeHelper<PA8>.ValidFileSize(360 * 961).Should().Be(false);

            // PB8-宝可梦BDSP
            FileTradeHelper<PB8>.ValidFileSize(344).Should().Be(true);
            FileTradeHelper<PB8>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PB8>.ValidFileSize(345).Should().Be(false);
            FileTradeHelper<PB8>.ValidFileSize(344 * 10).Should().Be(true);
            FileTradeHelper<PB8>.ValidFileSize(344 * 1200).Should().Be(true);
            FileTradeHelper<PB8>.ValidFileSize(344 * 1201).Should().Be(false);

            // PK8-宝可梦剑盾
            FileTradeHelper<PK8>.ValidFileSize(344).Should().Be(true);
            FileTradeHelper<PK8>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PK8>.ValidFileSize(345).Should().Be(false);
            FileTradeHelper<PK8>.ValidFileSize(344 * 10).Should().Be(true);
            FileTradeHelper<PK8>.ValidFileSize(344 * 960).Should().Be(true);
            FileTradeHelper<PK8>.ValidFileSize(344 * 961).Should().Be(false);

            // PB7-宝可梦Lets Go皮卡丘伊布
            FileTradeHelper<PB7>.ValidFileSize(260).Should().Be(true);
            FileTradeHelper<PB7>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PB7>.ValidFileSize(259).Should().Be(false);
            FileTradeHelper<PB7>.ValidFileSize(260 * 10).Should().Be(true);
            FileTradeHelper<PB7>.ValidFileSize(260 * 960).Should().Be(true);
            FileTradeHelper<PB7>.ValidFileSize(260 * 961).Should().Be(false);

            // PA9-宝可梦ZA
            FileTradeHelper<PA9>.ValidFileSize(344).Should().Be(true);
            FileTradeHelper<PA9>.ValidFileSize(0).Should().Be(false);
            FileTradeHelper<PA9>.ValidFileSize(407).Should().Be(false);
            FileTradeHelper<PA9>.ValidFileSize(408).Should().Be(true);
            FileTradeHelper<PA9>.ValidFileSize(408 * 10).Should().Be(true);
            FileTradeHelper<PA9>.ValidFileSize(408 * 960).Should().Be(true);
            FileTradeHelper<PA9>.ValidFileSize(408 * 961).Should().Be(false);
        }
    }
}
