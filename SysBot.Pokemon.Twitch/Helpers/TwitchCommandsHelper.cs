using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;
using SysBot.Pokemon;
using SysBot.Pokemon.Helpers;
using System;

namespace SysBot.Pokemon.Twitch
{
    public static class TwitchCommandsHelper<T> where T : PKM, new()
    {
        // Helper functions for commands
        public static bool AddToWaitingList(string setstring, string display, string username, ulong mUserId, bool sub, out string msg)
        {
            if (!TwitchBot<T>.Info.GetCanQueue())
            {
                msg = "抱歉，目前不接受加入队列的请求！";
                return false;
            }

            setstring = ShowdownTranslator<T>.TranslateIfChinese(setstring);
            var set = ShowdownUtil.ConvertToShowdown(setstring);
            if (set == null)
            {
                msg = $"跳过交易，@{username}：未为该宝可梦提供昵称。";
                return false;
            }
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (template.Species < 1)
            {
                msg = $"跳过交易，@{username}：请阅读命令参数应当如何填写。";
                return false;
            }

            if (set.InvalidLines.Count != 0)
            {
                msg = $"跳过交易，@{username}：无法解析 Showdown 配置：\n{string.Join("\n", set.InvalidLines)}";
                return false;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();

                // Check if this is an egg request
                bool isEgg = set.Nickname.Equals("egg", StringComparison.CurrentCultureIgnoreCase) && Breeding.CanHatchAsEgg(set.Species);

                PKM pkm;
                string result;
                if (isEgg)
                {
                    // Use ALM's GenerateEgg method for eggs
                    pkm = sav.GenerateEgg(template, out var eggResult);
                    result = eggResult.ToString();
                    if (eggResult != LegalizationResult.Regenerated)
                    {
                        msg = $"跳过交易，@{username}：生成蛋失败。";
                        return false;
                    }
                }
                else
                {
                    // Use normal generation for non-eggs
                    pkm = sav.GetLegal(template, out result);
                }

                var nickname = pkm.Nickname.ToLower();

                if (pkm.Species == 132 && (nickname.Contains("atk") || nickname.Contains("spa") || nickname.Contains("spe") || nickname.Contains("6iv")))
                    TradeExtensions<T>.DittoTrade(pkm);

                if (!pkm.CanBeTraded())
                {
                    msg = $"跳过交易，@{username}：所提供的宝可梦内容被禁止交易！";
                    return false;
                }

                if (pkm is T pk)
                {
                    var valid = new LegalityAnalysis(pkm).Valid;
                    if (valid)
                    {
                        var tq = new TwitchQueue<T>(pk, new PokeTradeTrainerInfo(display, mUserId), username, sub);
                        TwitchBot<T>.QueuePool.RemoveAll(z => z.UserName == username); // remove old requests if any
                        TwitchBot<T>.QueuePool.Add(tq);
                        msg = $"@{username} - 已加入等待列表。请私信我你的交易密码！若长时间未响应，你的请求将会被移除！";
                        return true;
                    }
                }

                var reason = result == "超时" ? "生成配招耗时过长。" : "无法生成合法的宝可梦。";
                msg = $"跳过交易，@{username}：{reason}";
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TwitchCommandsHelper<T>));
                msg = $"跳过交易，@{username}：发生了意外问题。";
            }
            return false;
        }

        public static string ClearTrade(string user)
        {
            var result = TwitchBot<T>.Info.ClearTrade(user);
            return GetClearTradeMessage(result);
        }

        public static string ClearTrade(ulong userID)
        {
            var result = TwitchBot<T>.Info.ClearTrade(userID);
            return GetClearTradeMessage(result);
        }

        public static string GetCode(ulong parse)
        {
            var detail = TwitchBot<T>.Info.GetDetail(parse);
            return detail == null
                ? "抱歉，你当前不在队列中。"
                : $"你的交易密码是 {detail.Trade.Code:0000 0000}";
        }

        private static string GetClearTradeMessage(QueueResultRemove result)
        {
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "看起来你正在交易处理中！未从队列移除。",
                QueueResultRemove.CurrentlyProcessingRemoved => "看起来你正在交易处理中！已从队列移除。",
                QueueResultRemove.Removed => "已将你从队列中移除。",
                _ => "抱歉，你当前不在队列中。",
            };
        }
    }
}
