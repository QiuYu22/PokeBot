using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System;

namespace SysBot.Pokemon.Helpers;

public static class LanguageHelper
{
    public static byte GetFinalLanguage(string content, ShowdownSet? set, byte configLanguage, Func<string, byte> detectLanguageFunc)
    {
        // 检查 Showdown 数据中是否显式指定语言
        var lines = content.Split('\n', StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Language:", StringComparison.OrdinalIgnoreCase))
            {
                var languageValue = line["Language:".Length..].Trim();

                // 尝试解析为 LanguageID 枚举
                if (Enum.TryParse<LanguageID>(languageValue, true, out var langId))
                {
                    return (byte)langId;
                }

                // 支持常见语言名称
                var explicitLang = languageValue.ToLower() switch
                {
                    "japanese" or "jpn" or "日本語" => (byte)LanguageID.Japanese,
                    "english" or "eng" => (byte)LanguageID.English,
                    "french" or "fre" or "fra" => (byte)LanguageID.French,
                    "italian" or "ita" => (byte)LanguageID.Italian,
                    "german" or "ger" or "deu" => (byte)LanguageID.German,
                    "spanish" or "spa" or "esp" => (byte)LanguageID.Spanish,
                    "spanish-latam" or "spanishl" or "es-419" or "latam" => (byte)LanguageID.SpanishL,
                    "korean" or "kor" or "한국어" => (byte)LanguageID.Korean,
                    "chinese" or "chs" or "中文" => (byte)LanguageID.ChineseS,
                    "cht" => (byte)LanguageID.ChineseT,
                    _ => 0
                };

                if (explicitLang != 0)
                {
                    return (byte)explicitLang;
                }
            }
        }

        // 未找到显式语言时，通过检测函数获取
        byte detectedLanguage = detectLanguageFunc(content);

        // 若未检测到语言（0），使用配置语言
        if (detectedLanguage == 0)
        {
            return configLanguage;
        }

        return detectedLanguage;
    }

    public static ITrainerInfo GetTrainerInfoWithLanguage<T>(LanguageID language) where T : PKM, new()
    {
        return typeof(T) switch
        {
            Type t when t == typeof(PK8) => TrainerSettings.GetSavedTrainerData(8, GameVersion.SWSH, lang: language),
            Type t when t == typeof(PB8) => TrainerSettings.GetSavedTrainerData(8, GameVersion.BDSP, lang: language),
            Type t when t == typeof(PA8) => TrainerSettings.GetSavedTrainerData(8, GameVersion.PLA, lang: language),
            Type t when t == typeof(PK9) => TrainerSettings.GetSavedTrainerData(9, GameVersion.SV, lang: language),
            Type t when t == typeof(PA9) => TrainerSettings.GetSavedTrainerData(9, GameVersion.ZA, lang: language),
            Type t when t == typeof(PB7) => TrainerSettings.GetSavedTrainerData(7, GameVersion.GE, lang: language),
            _ => throw new ArgumentException("未识别的训练家类型，无法获取对应模板", typeof(T).Name)
        };
    }
}
