using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing.PokeSprite;
using SysBot.Pokemon.Discord.Commands.Bots;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    private static readonly Dictionary<int, string> MilestoneImages = new()
    {
        { 1, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/001.png" },
        { 50, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/050.png" },
        { 100, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/100.png" },
        { 150, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/150.png" },
        { 200, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/200.png" },
        { 250, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/250.png" },
        { 300, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/300.png" },
        { 350, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/350.png" },
        { 400, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/400.png" },
        { 450, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/450.png" },
        { 500, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/500.png" },
        { 550, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/550.png" },
        { 600, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/600.png" },
        { 650, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/650.png" },
        { 700, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/700.png" }
    };

    private static string GetMilestoneDescription(int tradeCount)
    {
        return tradeCount switch
        {
            1 => "恭喜您完成第一次交易！\n**称号:** 新手训练家。",
            50 => "您已完成 50 次交易！\n**称号:** 初级训练家。",
            100 => "您已完成 100 次交易！\n**称号:** 宝可梦博士。",
            150 => "您已完成 150 次交易！\n**称号:** 宝可梦专家。",
            200 => "您已完成 200 次交易！\n**称号:** 宝可梦冠军。",
            250 => "您已完成 250 次交易！\n**称号:** 宝可梦英雄。",
            300 => "您已完成 300 次交易！\n**称号:** 宝可梦精英。",
            350 => "您已完成 350 次交易！\n**称号:** 宝可梦交易员。",
            400 => "您已完成 400 次交易！\n**称号:** 宝可梦贤者。",
            450 => "您已完成 450 次交易！\n**称号:** 宝可梦传奇。",
            500 => "您已完成 500 次交易！\n**称号:** 地区大师。",
            550 => "您已完成 550 次交易！\n**称号:** 交易大师。",
            600 => "您已完成 600 次交易！\n**称号:** 世界闻名。",
            650 => "您已完成 650 次交易！\n**称号:** 宝可梦大师。",
            700 => "您已完成 700 次交易！\n**称号:** 宝可梦之神。",
            _ => $"恭喜您完成 {tradeCount} 次交易！继续加油！"
        };
    }

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isHiddenTrade = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync("交易密码应为 00000000-99999999！").ConfigureAwait(false);
            return;
        }

        try
        {
            // Only send trade code for non-batch trades (batch container will handle its own)
            if (!isBatchTrade)
            {
                if (trade is PB7 && lgcode != null)
                {
                    var (thefile, lgcodeembed) = CreateLGLinkCodeSpriteEmbed(lgcode);
                    await trader.SendFileAsync(thefile, "您的交易密码是:", embed: lgcodeembed).ConfigureAwait(false);
                }
                else
                {
                    await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);
                }
            }

            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryEgg, lgcode, ignoreAutoOT, setEdited, isNonNative).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, bool ignoreAutoOT = false)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User, ignoreAutoOT: ignoreAutoOT);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName,
        RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade,
        int batchTradeNumber, int totalBatchTrades, bool isHiddenTrade, bool isMysteryEgg = false,
        List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        // Note: This method should only be called for individual trades now
        // Batch trades use AddBatchContainerToQueueAsync

        var user = trader;
        var userID = user.Id;
        var name = user.Username;
        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades,
            isMysteryEgg, lgcode: lgcode!);

        int uniqueTradeID = GenerateUniqueTradeID();

        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored,
            lgcode, batchTradeNumber, totalBatchTrades, isMysteryEgg, uniqueTradeID, ignoreAutoOT, setEdited);

        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.LinkTrade, name, uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var isSudo = sig == RequestSignificance.Owner;
        var added = Info.AddToTradeQueue(trade, userID, false, isSudo);

        // Start queue position updates for Discord notification
        if (added != QueueResultAdd.AlreadyInQueue && added != QueueResultAdd.NotAllowedItem && notifier is DiscordTradeNotifier<T> discordNotifier)
        {
            // IMPORTANT: Update the notifier's unique trade ID to match the one used in the queue
            // Otherwise the DM will check position with the wrong ID and return incorrect results
            discordNotifier.UpdateUniqueTradeID(uniqueTradeID);
            await discordNotifier.SendInitialQueueUpdate().ConfigureAwait(false);
        }

        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            await context.Channel.SendMessageAsync($"{trader.Mention} - 您已在队列中！").ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        if (added == QueueResultAdd.QueueFull)
        {
            var maxCount = SysCord<T>.Runner.Config.Queues.MaxQueueCount;
            var embed = new EmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("🚫 队列已满")
                .WithDescription($"队列当前已满 ({maxCount}/{maxCount})。请稍后在有空位时重试。")
                .WithFooter("交易完成后队列将会开放")
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        if (added == QueueResultAdd.NotAllowedItem)
        {
            var held = pk.HeldItem;
            var itemName = held > 0 ? PKHeX.Core.GameInfo.GetStrings("en").Item[held] : "(无)";
            await context.Channel.SendMessageAsync($"{trader.Mention} - 交易被阻止：携带道具 '{itemName}' 无法在 PLZA 中交易。").ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        var embedData = DetailsExtractor<T>.ExtractPokemonDetails(
            pk, trader, isMysteryEgg, type == PokeRoutineType.Clone, type == PokeRoutineType.Dump,
            type == PokeRoutineType.FixOT, type == PokeRoutineType.SeedCheck, false, 1, 1
        );

        try
        {
            (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(pk);

            embedData.EmbedImageUrl = isMysteryEgg ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/mysteryegg3.png" :
                                       type == PokeRoutineType.Dump ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/128x128/dumpball.png" :
                                       type == PokeRoutineType.Clone ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/clonepod.png" :
                                       type == PokeRoutineType.SeedCheck ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/specialrequest.png" :
                                       type == PokeRoutineType.FixOT ? "https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/128x128/rocketball.png" :
                                       embedImageUrl;

            embedData.HeldItemUrl = string.Empty;
            if (!string.IsNullOrWhiteSpace(embedData.HeldItem))
            {
                string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                embedData.HeldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
            }

            embedData.IsLocalFile = File.Exists(embedData.EmbedImageUrl);

            var position = Info.CheckPosition(userID, uniqueTradeID, type);
            var botct = Info.Hub.Bots.Count;
            var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;
            var etaMessage = $"预计: {baseEta:F1} 分钟后交易。";
            string footerText = $"当前位置: {(position.Position == -1 ? 1 : position.Position)}";

            string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails);
            if (!string.IsNullOrEmpty(userDetailsText))
            {
                footerText += $"\n{userDetailsText}";
            }
            footerText += $"\n{etaMessage}";

            var embedBuilder = new EmbedBuilder()
                .WithColor(embedColor)
                .WithImageUrl(embedData.IsLocalFile ? $"attachment://{Path.GetFileName(embedData.EmbedImageUrl)}" : embedData.EmbedImageUrl)
                .WithFooter(footerText)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(embedData.AuthorName)
                    .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                    .WithUrl("https://genpkm.com"));

            DetailsExtractor<T>.AddAdditionalText(embedBuilder);

            if (!isMysteryEgg && type != PokeRoutineType.Clone && type != PokeRoutineType.Dump && type != PokeRoutineType.FixOT && type != PokeRoutineType.SeedCheck)
            {
                DetailsExtractor<T>.AddNormalTradeFields(embedBuilder, embedData, trader.Mention, pk);
            }
            else
            {
                DetailsExtractor<T>.AddSpecialTradeFields(embedBuilder, isMysteryEgg, type == PokeRoutineType.SeedCheck, type == PokeRoutineType.Clone, type == PokeRoutineType.FixOT, trader.Mention);
            }

            // Check if the Pokemon is Non-Native and/or has a Home Tracker
            if (pk is IHomeTrack homeTrack)
            {
                if (homeTrack.HasTracker && isNonNative)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/exclamation.gif";
                    embedBuilder.AddField("**__注意__**: **此宝可梦为非原生且有 Home 追踪器。**", "*未应用自动OT。*");
                }
                else if (homeTrack.HasTracker)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/exclamation.gif";
                    embedBuilder.AddField("**__注意__**: **检测到 Home 追踪器。**", "*未应用自动OT。*");
                }
                else if (isNonNative)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/exclamation.gif";
                    embedBuilder.AddField("**__注意__**: **此宝可梦为非原生。**", "*无法进入 HOME 且未应用自动OT。*");
                }
            }
            else if (isNonNative)
            {
                embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/exclamation.gif";
                embedBuilder.AddField("**__注意__**: **此宝可梦为非原生。**", "*无法进入 HOME 且未应用自动OT。*");
            }

            DetailsExtractor<T>.AddThumbnails(embedBuilder, type == PokeRoutineType.Clone, type == PokeRoutineType.SeedCheck, embedData.HeldItemUrl);

            if (!isHiddenTrade && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
            {
                var embed = embedBuilder.Build();
                if (embed == null)
                {
                    Console.WriteLine("Error: Embed is null.");
                    await context.Channel.SendMessageAsync("准备交易详情时发生错误。");
                    return new TradeQueueResult(false);
                }

                if (embedData.IsLocalFile)
                {
                    await context.Channel.SendFileAsync(embedData.EmbedImageUrl, embed: embed);
                    await ScheduleFileDeletion(embedData.EmbedImageUrl, 0);
                }
                else
                {
                    await context.Channel.SendMessageAsync(embed: embed);
                }
            }
            else
            {
                var message = $"{trader.Mention} - Added to the LinkTrade queue. Current Position: {position.Position}. Receiving: {embedData.SpeciesName}.\n{etaMessage}";
                await context.Channel.SendMessageAsync(message);
            }
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex);
            return new TradeQueueResult(false);
        }

        if (SysCord<T>.Runner.Hub.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            int tradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            _ = SendMilestoneEmbed(tradeCount, context.Channel, trader);
        }

        return new TradeQueueResult(true);
    }

    public static async Task AddBatchContainerToQueueAsync(SocketCommandContext context, int code, string trainer, T firstTrade, List<T> allTrades, RequestSignificance sig, SocketUser trader, int totalBatchTrades)
    {
        var userID = trader.Id;
        var name = trader.Username;
        var trainer_info = new PokeTradeTrainerInfo(trainer, userID);
        var notifier = new DiscordTradeNotifier<T>(firstTrade, trainer_info, code, trader, 1, totalBatchTrades, false, lgcode: []);

        int uniqueTradeID = GenerateUniqueTradeID();

        var detail = new PokeTradeDetail<T>(firstTrade, trainer_info, notifier, PokeTradeType.Batch, code,
            sig == RequestSignificance.Favored, null, 1, totalBatchTrades, false, uniqueTradeID)
        {
            BatchTrades = allTrades
        };

        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.Batch, name, uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, false, sig == RequestSignificance.Owner);

        // Send trade code once
        await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);

        // Start queue position updates for Discord notification
        if (added != QueueResultAdd.AlreadyInQueue && added != QueueResultAdd.NotAllowedItem && notifier is DiscordTradeNotifier<T> discordNotifier)
        {
            // IMPORTANT: Update the notifier's unique trade ID to match the one used in the queue
            // Otherwise the DM will check position with the wrong ID and return incorrect results
            discordNotifier.UpdateUniqueTradeID(uniqueTradeID);
            await discordNotifier.SendInitialQueueUpdate().ConfigureAwait(false);
        }

        // Handle the display
        if (added == QueueResultAdd.AlreadyInQueue)
        {
            await context.Channel.SendMessageAsync($"{trader.Mention} - 您已在队列中！").ConfigureAwait(false);
            return;
        }

        if (added == QueueResultAdd.QueueFull)
        {
            var maxCount = SysCord<T>.Runner.Config.Queues.MaxQueueCount;
            var embed = new EmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("🚫 队列已满")
                .WithDescription($"队列当前已满 ({maxCount}/{maxCount})。请稍后在有空位时重试。")
                .WithFooter("交易完成后队列将会开放")
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        if (added == QueueResultAdd.NotAllowedItem)
        {
            var held = firstTrade.HeldItem;
            var itemName = held > 0 ? PKHeX.Core.GameInfo.GetStrings("en").Item[held] : "(无)";
            await context.Channel.SendMessageAsync($"{trader.Mention} - 交易被阻止：携带道具 '{itemName}' 无法在 PLZA 中交易。").ConfigureAwait(false);
            return;
        }

        var position = Info.CheckPosition(userID, uniqueTradeID, PokeRoutineType.Batch);
        var botct = Info.Hub.Bots.Count;
        var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;

        // Get user trade details for footer
        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }

        // Send initial batch summary message
        await context.Channel.SendMessageAsync($"{trader.Mention} - 已将包含 {totalBatchTrades} 只宝可梦的批量交易添加到队列！位置: {position.Position}。预计: {baseEta:F1} 分钟。").ConfigureAwait(false);

        // Create and send embeds for each Pokémon in the batch
        if (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
        {
            for (int i = 0; i < allTrades.Count; i++)
            {
                var pk = allTrades[i];
                var batchTradeNumber = i + 1;

                // Extract details for this Pokémon
                var embedData = DetailsExtractor<T>.ExtractPokemonDetails(
                    pk, trader, false, false, false, false, false, true, batchTradeNumber, totalBatchTrades
                );

                try
                {
                    // Prepare embed details
                    (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(pk);

                    embedData.EmbedImageUrl = embedImageUrl;
                    embedData.HeldItemUrl = string.Empty;
                    if (!string.IsNullOrWhiteSpace(embedData.HeldItem))
                    {
                        string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                        embedData.HeldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
                    }

                    embedData.IsLocalFile = File.Exists(embedData.EmbedImageUrl);

                    // Build footer text with batch info
                    string footerText = $"批量交易 {batchTradeNumber}/{totalBatchTrades}";
                    if (i == 0) // Only show position and ETA on first embed
                    {
                        footerText += $" | 位置: {position.Position}";
                        string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails);
                        if (!string.IsNullOrEmpty(userDetailsText))
                        {
                            footerText += $"\n{userDetailsText}";
                        }
                        footerText += $"\n预计: {baseEta:F1} 分钟完成批量交易";
                    }

                    // Create embed
                    var embedBuilder = new EmbedBuilder()
                        .WithColor(embedColor)
                        .WithImageUrl(embedData.IsLocalFile ? $"attachment://{Path.GetFileName(embedData.EmbedImageUrl)}" : embedData.EmbedImageUrl)
                        .WithFooter(footerText)
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName(embedData.AuthorName)
                            .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                            .WithUrl("https://genpkm.com"));

                    DetailsExtractor<T>.AddAdditionalText(embedBuilder);
                    DetailsExtractor<T>.AddNormalTradeFields(embedBuilder, embedData, trader.Mention, pk);

                    // Check for Non-Native and Home Tracker
                    if (pk is IHomeTrack homeTrack)
                    {
                        if (homeTrack.HasTracker)
                        {
                            embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/hexbyt3/sprites/main/exclamation.gif";
                            embedBuilder.AddField("**__注意__**: **检测到 Home 追踪器。**", "*未应用自动OT。*");
                        }
                    }

                    DetailsExtractor<T>.AddThumbnails(embedBuilder, false, false, embedData.HeldItemUrl);

                    var embed = embedBuilder.Build();

                    // Send embed
                    if (embedData.IsLocalFile)
                    {
                        await context.Channel.SendFileAsync(embedData.EmbedImageUrl, embed: embed);
                        await ScheduleFileDeletion(embedData.EmbedImageUrl, 0);
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync(embed: embed);
                    }

                    // Small delay between embeds to avoid rate limiting
                    if (i < allTrades.Count - 1)
                    {
                        await Task.Delay(500);
                    }
                }
                catch (HttpException ex)
                {
                    await HandleDiscordExceptionAsync(context, trader, ex);
                }
            }
        }

        // Send milestone embed if applicable
        if (SysCord<T>.Runner.Hub.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            int tradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            _ = SendMilestoneEmbed(tradeCount, context.Channel, trader);
        }
    }

    private static int GenerateUniqueTradeID()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomValue = Random.Shared.Next(1000);
        return (int)((timestamp % int.MaxValue) * 1000 + randomValue);
    }

    private static string GetImageFolderPath()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string imagesFolder = Path.Combine(baseDirectory, "Images");

        if (!Directory.Exists(imagesFolder))
        {
            Directory.CreateDirectory(imagesFolder);
        }

        return imagesFolder;
    }

    private static string SaveImageLocally(System.Drawing.Image image)
    {
        string imagesFolderPath = GetImageFolderPath();
        string filePath = Path.Combine(imagesFolderPath, $"image_{Guid.NewGuid()}.png");

#pragma warning disable CA1416 // Validate platform compatibility
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
#pragma warning restore CA1416 // Validate platform compatibility

        return filePath;
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetails(T pk)
    {
        string embedImageUrl;
        string speciesImageUrl;

        if (pk.IsEgg)
        {
            string eggImageUrl = GetEggTypeImageUrl(pk);
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, false, true, null);
            System.Drawing.Image combinedImage = await OverlaySpeciesOnEgg(eggImageUrl, speciesImageUrl);
            embedImageUrl = SaveImageLocally(combinedImage);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            speciesImageUrl = TradeExtensions<T>.PokeImg(pk, canGmax, false, SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.PreferredImageSize);
            embedImageUrl = speciesImageUrl;
        }

        var strings = GameInfo.GetStrings("en");
        string ballName = strings.balllist[pk.Ball];
        if (ballName.Contains("(LA)"))
        {
            ballName = "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower();
        }
        else
        {
            ballName = ballName.Replace(" ", "").ToLower();
        }

        string ballImgUrl = $"https://raw.githubusercontent.com/hexbyt3/sprites/main/AltBallImg/20x20/{ballName}.png";

        if (Uri.TryCreate(embedImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using var localImage = await Task.Run(() => System.Drawing.Image.FromFile(uri.LocalPath));
#pragma warning restore CA1416 // Validate platform compatibility
            using var ballImage = await LoadImageFromUrl(ballImgUrl);
            if (ballImage != null)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                using (var graphics = Graphics.FromImage(localImage))
                {
                    var ballPosition = new Point(localImage.Width - ballImage.Width, localImage.Height - ballImage.Height);
                    graphics.DrawImage(ballImage, ballPosition);
                }
#pragma warning restore CA1416 // Validate platform compatibility
                embedImageUrl = SaveImageLocally(localImage);
            }
        }
        else
        {
            (System.Drawing.Image? finalCombinedImage, bool ballImageLoaded) = await OverlayBallOnSpecies(speciesImageUrl, ballImgUrl);
            if (finalCombinedImage != null)
            {
                embedImageUrl = SaveImageLocally(finalCombinedImage);
            }
            else
            {
                // Fall back to species image if overlay failed
                embedImageUrl = speciesImageUrl;
            }

            if (!ballImageLoaded)
            {
                Console.WriteLine($"Ball image could not be loaded: {ballImgUrl}");
            }
        }

        (int R, int G, int B) = await GetDominantColorAsync(embedImageUrl);
        return (embedImageUrl, new DiscordColor(R, G, B));
    }

    private static async Task<(System.Drawing.Image?, bool)> OverlayBallOnSpecies(string speciesImageUrl, string ballImageUrl)
    {
        using var speciesImage = await LoadImageFromUrl(speciesImageUrl);
        if (speciesImage == null)
        {
            Console.WriteLine("Species image could not be loaded.");
            return (null, false);
        }

        var ballImage = await LoadImageFromUrl(ballImageUrl);
        if (ballImage == null)
        {
            Console.WriteLine($"Ball image could not be loaded: {ballImageUrl}");
#pragma warning disable CA1416 // Validate platform compatibility
            return ((System.Drawing.Image)speciesImage.Clone(), false);
#pragma warning restore CA1416 // Validate platform compatibility
        }

        using (ballImage)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using (var graphics = Graphics.FromImage(speciesImage))
            {
                var ballPosition = new Point(speciesImage.Width - ballImage.Width, speciesImage.Height - ballImage.Height);
                graphics.DrawImage(ballImage, ballPosition);
            }
#pragma warning restore CA1416 // Validate platform compatibility

#pragma warning disable CA1416 // Validate platform compatibility
            return ((System.Drawing.Image)speciesImage.Clone(), true);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }

    private static async Task<System.Drawing.Image> OverlaySpeciesOnEgg(string eggImageUrl, string speciesImageUrl)
    {
        System.Drawing.Image? eggImage = await LoadImageFromUrl(eggImageUrl);
        System.Drawing.Image? speciesImage = await LoadImageFromUrl(speciesImageUrl);
        
        if (eggImage == null || speciesImage == null)
        {
            throw new InvalidOperationException("Failed to load egg or species image.");
        }

#pragma warning disable CA1416 // Validate platform compatibility
        double scaleRatio = Math.Min((double)eggImage.Width / speciesImage.Width, (double)eggImage.Height / speciesImage.Height);
        Size newSize = new((int)(speciesImage.Width * scaleRatio), (int)(speciesImage.Height * scaleRatio));
        System.Drawing.Image resizedSpeciesImage = new Bitmap(speciesImage, newSize);

        using (Graphics g = Graphics.FromImage(eggImage))
        {
            int speciesX = (eggImage.Width - resizedSpeciesImage.Width) / 2;
            int speciesY = (eggImage.Height - resizedSpeciesImage.Height) / 2;
            g.DrawImage(resizedSpeciesImage, speciesX, speciesY, resizedSpeciesImage.Width, resizedSpeciesImage.Height);
        }

        speciesImage.Dispose();
        resizedSpeciesImage.Dispose();

        double scale = Math.Min(128.0 / eggImage.Width, 128.0 / eggImage.Height);
        int newWidth = (int)(eggImage.Width * scale);
        int newHeight = (int)(eggImage.Height * scale);

        Bitmap finalImage = new(128, 128);

        using (Graphics g = Graphics.FromImage(finalImage))
        {
            int x = (128 - newWidth) / 2;
            int y = (128 - newHeight) / 2;
            g.DrawImage(eggImage, x, y, newWidth, newHeight);
        }

        eggImage.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility
        return finalImage;
    }

    private static async Task<System.Drawing.Image?> LoadImageFromUrl(string url)
    {
        using HttpClient client = new();
        HttpResponseMessage response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to load image from {url}. Status code: {response.StatusCode}");
            return null;
        }

        Stream stream = await response.Content.ReadAsStreamAsync();
        if (stream == null || stream.Length == 0)
        {
            Console.WriteLine($"No data or empty stream received from {url}");
            return null;
        }

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return System.Drawing.Image.FromStream(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Failed to create image from stream. URL: {url}, Exception: {ex}");
            return null;
        }
    }

    private static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds)
    {
        await Task.Delay(delayInMilliseconds);
        DeleteFile(filePath);
    }

    private static void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }

    private static async Task SendMilestoneEmbed(int tradeCount, ISocketMessageChannel channel, SocketUser user)
    {
        if (MilestoneImages.TryGetValue(tradeCount, out string? imageUrl))
        {
            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username} 的里程碑勋章")
                .WithDescription(GetMilestoneDescription(tradeCount))
                .WithColor(new DiscordColor(255, 215, 0)) // Gold color
                .WithThumbnailUrl(imageUrl)
                .Build();

            await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
    }

    public static async Task<(int R, int G, int B)> GetDominantColorAsync(string imagePath)
    {
        try
        {
            Bitmap image = await LoadImageAsync(imagePath);

            var colorCount = new Dictionary<Color, int>();
#pragma warning disable CA1416 // Validate platform compatibility
            await Task.Run(() =>
            {
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var pixelColor = image.GetPixel(x, y);

                        if (pixelColor.A < 128 || pixelColor.GetBrightness() > 0.9) continue;

                        var brightnessFactor = (int)(pixelColor.GetBrightness() * 100);
                        var saturationFactor = (int)(pixelColor.GetSaturation() * 100);
                        var combinedFactor = brightnessFactor + saturationFactor;

                        var quantizedColor = Color.FromArgb(
                            pixelColor.R / 10 * 10,
                            pixelColor.G / 10 * 10,
                            pixelColor.B / 10 * 10
                        );

                        if (colorCount.ContainsKey(quantizedColor))
                        {
                            colorCount[quantizedColor] += combinedFactor;
                        }
                        else
                        {
                            colorCount[quantizedColor] = combinedFactor;
                        }
                    }
                }
            });

            image.Dispose();
#pragma warning restore CA1416 // Validate platform compatibility

            if (colorCount.Count == 0)
                return (255, 255, 255);

            var dominantColor = colorCount.Aggregate((a, b) => a.Value > b.Value ? a : b).Key;
            return (dominantColor.R, dominantColor.G, dominantColor.B);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing image from {imagePath}. Error: {ex.Message}");
            return (255, 255, 255);
        }
    }

    private static async Task<Bitmap> LoadImageAsync(string imagePath)
    {
        if (imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(imagePath);
            await using var stream = await response.Content.ReadAsStreamAsync();
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(stream);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        else
        {
#pragma warning disable CA1416 // Validate platform compatibility
            return new Bitmap(imagePath);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        message = "您必须授予我\"发送消息\"权限！";
                        Base.LogUtil.LogError("队列助手", message);
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> 您必须授予我\"管理消息\"权限！";
                    }
                }
                break;

            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    message = context.User == trader ? "您必须启用私信才能排队！" : "被提及的用户必须启用私信才能被排队！";
                }
                break;

            default:
                {
                    message = ex.DiscordCode != null ? $"Discord 错误 {(int)ex.DiscordCode}: {ex.Reason}" : $"Http 错误 {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    private static string GetEggTypeImageUrl(T pk)
    {
        var pi = pk.PersonalInfo;
        byte typeIndex = pi.Type1;

        string[] typeNames = [
            "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost",
            "Steel", "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon",
            "Dark", "Fairy"
        ];

        string typeName = (typeIndex >= 0 && typeIndex < typeNames.Length)
            ? typeNames[typeIndex]
            : "Normal";

        return $"https://raw.githubusercontent.com/hexbyt3/HomeImages/ebd562941ff77b1889a297ee50eacfa8cb3589de/128x128/Egg_{typeName}.png";
    }

    public static (string, Embed) CreateLGLinkCodeSpriteEmbed(List<Pictocodes> lgcode)
    {
        int codecount = 0;
        List<System.Drawing.Image> spritearray = [];
        foreach (Pictocodes cd in lgcode)
        {
            var showdown = new ShowdownSet(cd.ToString());
            var sav = BlankSaveFile.Get(EntityContext.Gen7b, "pip");
            PKM pk = sav.GetLegalFromSet(showdown).Created;
#pragma warning disable CA1416 // Validate platform compatibility
            System.Drawing.Image png = pk.Sprite();
            var destRect = new Rectangle(-40, -65, 137, 130);
            var destImage = new Bitmap(137, 130);

            destImage.SetResolution(png.HorizontalResolution, png.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(png, destRect, 0, 0, png.Width, png.Height, GraphicsUnit.Pixel);
            }
            png = destImage;
            spritearray.Add(png);
#pragma warning restore CA1416 // Validate platform compatibility
            codecount++;
        }

#pragma warning disable CA1416 // Validate platform compatibility
        int outputImageWidth = spritearray[0].Width + 20;
        int outputImageHeight = spritearray[0].Height - 65;

        Bitmap outputImage = new(outputImageWidth, outputImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(outputImage))
        {
            graphics.DrawImage(spritearray[0], new Rectangle(0, 0, spritearray[0].Width, spritearray[0].Height),
                new Rectangle(new Point(), spritearray[0].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[1], new Rectangle(50, 0, spritearray[1].Width, spritearray[1].Height),
                new Rectangle(new Point(), spritearray[1].Size), GraphicsUnit.Pixel);
            graphics.DrawImage(spritearray[2], new Rectangle(100, 0, spritearray[2].Width, spritearray[2].Height),
                new Rectangle(new Point(), spritearray[2].Size), GraphicsUnit.Pixel);
        }

        System.Drawing.Image finalembedpic = outputImage;
        var filename = $"{Directory.GetCurrentDirectory()}//finalcode.png";
        finalembedpic.Save(filename);
#pragma warning restore CA1416 // Validate platform compatibility

        filename = Path.GetFileName($"{Directory.GetCurrentDirectory()}//finalcode.png");
        Embed returnembed = new EmbedBuilder().WithTitle($"{lgcode[0]}, {lgcode[1]}, {lgcode[2]}").WithImageUrl($"attachment://{filename}").Build();
        return (filename, returnembed);
    }
}
