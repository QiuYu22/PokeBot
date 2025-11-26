using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord
{
    /// <summary>
    /// Provides functionality for listing and requesting Pokémon wondercard events via Discord commands.
    /// Users can interact with the system in multiple ways:
    ///
    /// 1. Listing Events:
    ///    - Users can list events from a specified generation or game. Optionally, users can filter this list by specifying a Pokémon species name.
    ///    - Command format: .srp {generationOrGame} [speciesName] [pageX]
    ///    - Example: .srp gen9 Mew page2
    ///      This command lists the second page of events for the 'gen9' dataset, specifically filtering for events related to 'Mew'.
    ///
    /// 2. Requesting Specific Events:
    ///    - Users can request a specific event to be processed by providing an event index number.
    ///    - Command format: .srp {generationOrGame} {eventIndex}
    ///    - Example: .srp gen9 26
    ///      This command requests the processing of the event at index 26 within the 'gen9' dataset.
    ///
    /// 3. Pagination:
    ///    - Users can navigate through pages of event listings by specifying the page number after the generation/game and optionally the species.
    ///    - Command format: .srp {generationOrGame} [speciesName] pageX
    ///    - Example: .srp gen9 page3
    ///      This command lists the third page of events for the 'gen9' dataset.
    ///
    /// This module ensures that user inputs are properly validated for the specific commands to manage event data effectively, adjusting listings or processing requests based on user interactions.
    /// </summary>
    public class SpecialRequestModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private const int itemsPerPage = 25;

        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        [Command("specialrequestpokemon")]
        [Alias("srp")]
        [Summary("列出指定世代或游戏的可用神秘卡活动，或在提供编号时请求特定活动。")]
        public async Task ListSpecialEventsAsync(string generationOrGame, [Remainder] string args = "")
        {
            var botPrefix = SysCord<T>.Runner.Config.Discord.CommandPrefix;
            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && int.TryParse(parts[0], out int index))
            {
                await SpecialEventRequestAsync(generationOrGame, index.ToString()).ConfigureAwait(false);
                return;
            }

            int page = 1;
            string speciesName = "";

            foreach (string part in parts)
            {
                if (part.StartsWith("page", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(4), out int pageNumber))
                {
                    page = pageNumber;
                    continue;
                }
                speciesName = part;
            }

            var eventData = GetEventData(generationOrGame);
            if (eventData == null)
            {
                await ReplyAsync($"无效的世代或游戏: {generationOrGame}").ConfigureAwait(false);
                return;
            }

            var allEvents = GetFilteredEvents(eventData, speciesName);
            if (!allEvents.Any())
            {
                await ReplyAsync($"未找到 {generationOrGame} 符合指定筛选条件的活动配布。").ConfigureAwait(false);
                return;
            }

            var pageCount = (int)Math.Ceiling((double)allEvents.Count() / itemsPerPage);
            page = Math.Clamp(page, 1, pageCount);
            var embed = BuildEventListEmbed(generationOrGame, allEvents, page, pageCount, botPrefix);
            await SendEventListAsync(embed).ConfigureAwait(false);
            await CleanupMessagesAsync().ConfigureAwait(false);
        }

        [Command("specialrequestpokemon")]
        [Alias("srp")]
        [Summary("从指定世代下载神秘卡活动附件并添加到交易队列。")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task SpecialEventRequestAsync(string generationOrGame, [Remainder] string args = "")
        {
            if (!int.TryParse(args, out int index))
            {
                await ReplyAsync("无效的活动索引。请提供有效的活动编号。").ConfigureAwait(false);
                return;
            }

            var userID = Context.User.Id;
            if (Info.IsUserInQueue(userID))
            {
                await ReplyAsync("您在队列中已有一个交易。请等待处理完成。").ConfigureAwait(false);
                return;
            }

            try
            {
                var eventData = GetEventData(generationOrGame);
                if (eventData == null)
                {
                    await ReplyAsync($"无效的世代或游戏: {generationOrGame}").ConfigureAwait(false);
                    return;
                }

                var entityEvents = eventData.Where(gift => gift.IsEntity && !gift.IsItem).ToArray();
                if (index < 1 || index > entityEvents.Length)
                {
                    await ReplyAsync($"无效的活动索引。请使用 `{SysCord<T>.Runner.Config.Discord.CommandPrefix}srp {generationOrGame}` 命令中的有效活动编号。").ConfigureAwait(false);
                    return;
                }

                var selectedEvent = entityEvents[index - 1];
                var pk = ConvertEventToPKM(selectedEvent);
                if (pk == null)
                {
                    await ReplyAsync("提供的神秘礼物数据与此模块不兼容！").ConfigureAwait(false);
                    return;
                }

                var code = Info.GetRandomTradeCode(userID);
                var lgcode = Info.GetRandomLGTradeCode();
                var sig = Context.User.GetFavor();

                await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User, lgcode: lgcode).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"发生错误: {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                await CleanupUserMessageAsync().ConfigureAwait(false);
            }
        }

        public static MysteryGift[]? GetEventData(string generationOrGame)
        {
            return generationOrGame.ToLowerInvariant() switch
            {
                "4" or "gen4" => EncounterEvent.MGDB_G4,
                "5" or "gen5" => EncounterEvent.MGDB_G5,
                "6" or "gen6" => EncounterEvent.MGDB_G6,
                "7" or "gen7" => EncounterEvent.MGDB_G7,
                "gg" or "lgpe" => EncounterEvent.MGDB_G7GG,
                "swsh" => EncounterEvent.MGDB_G8,
                "pla" or "la" => EncounterEvent.MGDB_G8A,
                "bdsp" => EncounterEvent.MGDB_G8B,
                "9" or "gen9" => EncounterEvent.MGDB_G9,
                "plza" or "9a" or "gen9a" => EncounterEvent.MGDB_G9A,
                _ => null,
            };
        }

        private static IEnumerable<(int Index, string EventInfo)> GetFilteredEvents(MysteryGift[] eventData, string speciesName = "")
        {
            return eventData
                .Select((gift, index) =>
                {
                    if (!gift.IsEntity || gift.IsItem)
                        return (Index: -1, EventInfo: string.Empty);

                    string species = GameInfo.Strings.Species[gift.Species];
                    if (!string.IsNullOrWhiteSpace(speciesName) && !species.Equals(speciesName, StringComparison.OrdinalIgnoreCase))
                        return (Index: -1, EventInfo: string.Empty);

                    string levelInfo = $"{gift.Level}";
                    string formName = ShowdownParsing.GetStringFromForm(gift.Form, GameInfo.Strings, gift.Species, gift.Context);
                    formName = !string.IsNullOrEmpty(formName) ? $"-{formName}" : "";
                    string trainerName = gift.OriginalTrainerName;

                    string eventDetails = $"{gift.CardHeader} - {species}{formName} | Lvl.{levelInfo} | OT: {trainerName}";

                    return (Index: index + 1, EventInfo: eventDetails);
                })
                .Where(x => x.Index != -1);
        }

        private static EmbedBuilder BuildEventListEmbed(string generationOrGame, IEnumerable<(int Index, string EventInfo)> allEvents, int page, int pageCount, string botPrefix)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"可用活动配布 - {generationOrGame.ToUpperInvariant()}")
                .WithDescription($"第 {page} 页，共 {pageCount} 页")
                .WithColor(DiscordColor.Blue);

            foreach (var item in allEvents.Skip((page - 1) * itemsPerPage).Take(itemsPerPage))
            {
                embed.AddField($"{item.Index}. {item.EventInfo}", $"使用 `{botPrefix}srp {generationOrGame} {item.Index}` 请求此活动配布。");
            }

            return embed;
        }

        private async Task SendEventListAsync(EmbedBuilder embed)
        {
            if (Context.User is not IUser user)
            {
                await ReplyAsync("**错误**: 无法发送私信。请检查您的**服务器隐私设置**。");
                return;
            }

            try
            {
                var dmChannel = await user.CreateDMChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed.Build());
                await ReplyAsync($"{Context.User.Mention}，我已向您发送了活动配布列表的私信。");
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
            {
                await ReplyAsync($"{Context.User.Mention}，我无法向您发送私信。请检查您的**服务器隐私设置**。");
            }
        }

        private async Task CleanupMessagesAsync()
        {
            await Task.Delay(10_000).ConfigureAwait(false);
            await CleanupUserMessageAsync().ConfigureAwait(false);
        }

        private async Task CleanupUserMessageAsync()
        {
            if (Context.Message is IUserMessage userMessage)
                await userMessage.DeleteAsync().ConfigureAwait(false);
        }

        public static T? ConvertEventToPKM(MysteryGift selectedEvent, byte? requestedLanguage = null, string? metDate = null)
        {
            // Create a SimpleTrainerInfo instance with just version and language
            var trainer = new SimpleTrainerInfo(selectedEvent.Version)
            {
                Language = requestedLanguage ?? (byte)LanguageID.English,
            };

            // Let the original implementation handle everything including special cases
            PKM? pkm = selectedEvent.ConvertToPKM(trainer, EncounterCriteria.Unrestricted);

            if (pkm is null)
                return null;

            // DO NOT apply custom met date for Mystery Gift eggs
            // PKHeX already handles dates correctly for Mystery Gifts including special cases like:
            // - BDSP eggs where MetLocation=65535 requires date fields to be 0
            // - SV eggs where MetLocation=0 requires date fields to be 0
            // Only apply custom dates for non-Mystery Gift scenarios
            // Note: For now, we skip date setting for all eggs to avoid conflicts
            if (!string.IsNullOrEmpty(metDate) && !pkm.IsEgg)
            {
                bool dateParseSuccess = false;

                // Try to parse YYYYMMDD format first (expected from PKHeX)
                if (metDate.Length == 8 && DateTime.TryParseExact(metDate, "yyyyMMdd", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    dateParseSuccess = true;
                }
                // Fallback to general DateTime parsing for other formats
                else if (DateTime.TryParse(metDate, out parsedDate))
                {
                    dateParseSuccess = true;
                }
                
                if (dateParseSuccess)
                {
                    var dateOnly = new DateOnly(parsedDate.Year, parsedDate.Month, parsedDate.Day);
                    
                    // Set MetDate for non-eggs
                    if (pkm is PK9 pk9)
                    {
                        pk9.MetDate = dateOnly;
                    }
                    else if (pkm is PK8 pk8)
                    {
                        pk8.MetDate = dateOnly;
                    }
                    else if (pkm is PA8 pa8)
                    {
                        pa8.MetDate = dateOnly;
                    }
                    else if (pkm is PB8 pb8)
                    {
                        pb8.MetDate = dateOnly;
                    }
                    else
                    {
                        // For older PKM formats, use individual day/month/year properties
                        pkm.MetDay = (byte)parsedDate.Day;
                        pkm.MetMonth = (byte)parsedDate.Month;
                        pkm.MetYear = (byte)(parsedDate.Year - 2000); // PKHeX stores only the last two digits of the year
                    }
                }
            }

            // Convert to the correct type if necessary
            if (pkm is T pk)
                return pk;

            return EntityConverter.ConvertToType(pkm, typeof(T), out _) as T;
        }

        [Command("geteventpokemon")]
        [Alias("gep")]
        [Summary("将请求的活动下载为 pk 文件并发送给用户。可选择指定语言。")]
        public async Task GetEventPokemonAsync(string generationOrGame, int eventIndex, byte? language = null)
        {
            try
            {
                var eventData = GetEventData(generationOrGame);
                if (eventData == null)
                {
                    await ReplyAsync($"无效的世代或游戏: {generationOrGame}").ConfigureAwait(false);
                    return;
                }

                var entityEvents = eventData.Where(gift => gift.IsEntity && !gift.IsItem).ToArray();
                if (eventIndex < 1 || eventIndex > entityEvents.Length)
                {
                    await ReplyAsync($"无效的活动索引。请使用来自 `{SysCord<T>.Runner.Config.Discord.CommandPrefix}gep {generationOrGame}` 命令的有效活动编号。").ConfigureAwait(false);
                    return;
                }

                var selectedEvent = entityEvents[eventIndex - 1];
                var pk = ConvertEventToPKM(selectedEvent);
                if (pk == null)
                {
                    await ReplyAsync("提供的神秘礼物数据与此模块不兼容！").ConfigureAwait(false);
                    return;
                }

                // If language is provided, set pk.Language
                if (language.HasValue)
                {
                    pk.Language = language.Value;
                }

                try
                {
                    await Context.User.SendPKMAsync(pk);
                    await ReplyAsync($"{Context.User.Mention}，我已通过私信向您发送了 PK 文件。");
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    await ReplyAsync($"{Context.User.Mention}，我无法向您发送私信。请检查您的**服务器隐私设置**。");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"发生错误: {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                await CleanupUserMessageAsync().ConfigureAwait(false);
            }
        }

        private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, PokeTradeType tradeType = PokeTradeType.Specific, bool ignoreAutoOT = false, bool isHiddenTrade = false)
        {
            lgcode ??= Helpers<T>.GenerateRandomPictocodes(3);
        var la = new LegalityAnalysis(pk);
        if (!la.Valid)
        {
            string responseMessage = pk.IsEgg ? "此蛋的 Showdown 配置无效。请检查您的信息后重试。" :
                $"{typeof(T).Name} 附件不合法，无法进行交易！\n\n{la.Report()}\n";
            var reply = await ReplyAsync(responseMessage).ConfigureAwait(false);
            await Task.Delay(6000);
            await reply.DeleteAsync().ConfigureAwait(false);
            return;
        }
            if (!la.Valid && la.Results.Any(m => m.Identifier is CheckIdentifier.Memory))
            {
                var clone = (T)pk.Clone();

                clone.HandlingTrainerName = pk.OriginalTrainerName;
                clone.HandlingTrainerGender = pk.OriginalTrainerGender;

                if (clone is PK8 or PA8 or PB8 or PK9)
                    ((dynamic)clone).HandlingTrainerLanguage = (byte)pk.Language;

                clone.CurrentHandler = 1;

                la = new LegalityAnalysis(clone);

                if (la.Valid) pk = clone;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, tradeType, usr, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryEgg, lgcode: lgcode, ignoreAutoOT).ConfigureAwait(false);
        }
    }
}
