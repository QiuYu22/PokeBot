using PKHeX.Core;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public static class NonTradableItemsPLZA
    {
        // Names must match PKHeX's item names in the respective language (case-insensitive compare below)
        private static readonly HashSet<string> BlockedItemNamesEn = new(StringComparer.OrdinalIgnoreCase)
        {
            "Abomasite", // 暴雪王进化石
            "Absolite", // 阿勃梭鲁进化石
            "Aerodactylite", // 化石翼龙进化石
            "Aggronite", // 波士可多拉进化石
            "Alakazite", // 胡地进化石
            "Altarianite", // 七夕青鸟进化石
            "Ampharosite", // 电龙进化石
            "Audinite", // 差不多娃娃进化石
            "Autographed Plush", // 亲笔签名玩偶
            "Banettite", // 勾魂眼进化石
            "Barbaracite", // 龟足巨铠进化石
            "Beedrillite", // 大针蜂进化石
            "Blastoisinite", // 水箭龟进化石
            "Blazikenite", // 火焰鸡进化石
            "Blue Canari Plush Lv. 1", // 蓝色水黄鹂玩偶 Lv. 1
            "Blue Canari Plush Lv. 2", // 蓝色水黄鹂玩偶 Lv. 2
            "Blue Canari Plush Lv. 3", // 蓝色水黄鹂玩偶 Lv. 3
            "Cameruptite", // 喷火驼进化石
            "Chandelurite", // 水晶灯火灵进化石
            "Charizardite X", // 喷火龙进化石 X
            "Charizardite Y", // 喷火龙进化石 Y
            "Cherished Ring", // 珍贵戒指
            "Chesnaughtite", // 布里卡隆进化石
            "Clefablite", // 皮可西进化石
            "Colorful Screw", // 彩色螺钉
            "Delphoxite", // 妖火红狐进化石
            "Diancite", // 蒂安希进化石
            "Dragalgite", // 毒藻龙进化石
            "Dragoninite", // 快龙进化石
            "Drampanite", // 老翁龙进化石
            "Eelektrossite", // 麻麻鳗鱼王进化石
            "Elevator Key", // 电梯钥匙
            "Emboarite", // 炎武王进化石
            "Excadrite", // 挖掘兔进化石
            "Falinksite", // 列阵兵进化石
            "Feraligite", // 大力鳄进化石
            "Floettite", // 花叶蒂进化石
            "Froslassite", // 雪妖女进化石
            "Galladite", // 艾路雷朵进化石
            "Garchompite", // 烈咬陆鲨进化石
            "Gardevoirite", // 沙奈朵进化石
            "Gengarite", // 耿鬼进化石
            "Glalitite", // 冰鬼护进化石
            "Gold Canari Plush Lv. 1", // 金色水黄鹂玩偶 Lv. 1
            "Gold Canari Plush Lv. 2", // 金色水黄鹂玩偶 Lv. 2
            "Gold Canari Plush Lv. 3", // 金色水黄鹂玩偶 Lv. 3
            "Green Canari Plush Lv. 1", // 绿色水黄鹂玩偶 Lv. 1
            "Green Canari Plush Lv. 2", // 绿色水黄鹂玩偶 Lv. 2
            "Green Canari Plush Lv. 3", // 绿色水黄鹂玩偶 Lv. 3
            "Greninjite", // 甲贺忍蛙进化石
            "Gyaradosite", // 暴鲤龙进化石
            "Hawluchanite", // 摔角鹰人进化石
            "Heracronite", // 赫拉克罗斯进化石
            "Houndoominite", // 黑鲁加进化石
            "Kangaskhanite", // 袋兽进化石
            "Key to Room 202", // 客房202钥匙
            "Lab Key Card A", // 研究所钥匙卡A
            "Lab Key Card B", // 研究所钥匙卡B
            "Lab Key Card C", // 研究所钥匙卡C
            "Latiasite", // 拉帝亚斯进化石
            "Latiosite", // 拉帝欧斯进化石
            "Lida's Things", // 莉达的物品
            "Lopunnite", // 长耳兔进化石
            "Lucarionite", // 路卡利欧进化石
            "Malamarite", // 乌贼王进化石
            "Manectite", // 雷电兽进化石
            "Mawilite", // 大嘴娃进化石
            "Medichamite", // 恰雷姆进化石
            "Mega Ring", // 超级环
            "Mega Shard", // 超级碎片
            "Meganiumite", // 大竺葵进化石
            "Metagrossite", // 巨金怪进化石
            "Mewtwonite X", // 超梦进化石 X
            "Mewtwonite Y", // 超梦进化石 Y
            "Pebble", // 小石头
            "Pidgeotite", // 大比鸟进化石
            "Pink Canari Plush Lv. 1", // 粉色水黄鹂玩偶 Lv. 1
            "Pink Canari Plush Lv. 2", // 粉色水黄鹂玩偶 Lv. 2
            "Pink Canari Plush Lv. 3", // 粉色水黄鹂玩偶 Lv. 3
            "Pinsirite", // 凯罗斯进化石
            "Pyroarite", // 火炎狮进化石
            "Raichunite X", // 雷丘进化石 X
            "Raichunite Y", // 雷丘进化石 Y
            "Red Canari Plush Lv. 1", // 红色水黄鹂玩偶 Lv. 1
            "Red Canari Plush Lv. 2", // 红色水黄鹂玩偶 Lv. 2
            "Red Canari Plush Lv. 3", // 红色水黄鹂玩偶 Lv. 3
            "Revitalizing Twig", // 复苏小枝
            "Sablenite", // 勾魂眼进化石
            "Salamencite", // 暴飞龙进化石
            "Sceptilite", // 蜥蜴王进化石
            "Scizorite", // 巨钳螳螂进化石
            "Scolipite", // 蜈蚣王进化石
            "Scraftinite", // 师公进化石
            "Sharpedonite", // 巨牙鲨进化石
            "Shiny Charm", // 闪耀护符
            "Skarmorite", // 盔甲鸟进化石
            "Slowbronite", // 呆壳兽进化石
            "Starminite", // 宝星鸟进化石
            "Steelixite", // 大钢蛇进化石
            "Super Lumiose Galette", // 超级密阿雷可丽饼
            "Swampertite", // 巨沼怪进化石
            "Tasty Trash", // 可口垃圾
            "Tyranitarite", // 班吉拉进化石
            "Venusaurite", // 妙蛙花进化石
            "Victreebelite", // 大食花进化石
            "Zygarde Cube", // 基格尔德多面体
            "Zygardite" // 基格尔德进化石
        };

        private static readonly HashSet<string> BlockedItemNamesZh = new(StringComparer.OrdinalIgnoreCase)
        {
            "暴雪王进化石",
            "阿勃梭鲁进化石",
            "化石翼龙进化石",
            "波士可多拉进化石",
            "胡地进化石",
            "七夕青鸟进化石",
            "电龙进化石",
            "差不多娃娃进化石",
            "亲笔签名玩偶",
            "勾魂眼进化石",
            "龟足巨铠进化石",
            "大针蜂进化石",
            "水箭龟进化石",
            "火焰鸡进化石",
            "蓝色水黄鹂玩偶 Lv. 1",
            "蓝色水黄鹂玩偶 Lv. 2",
            "蓝色水黄鹂玩偶 Lv. 3",
            "喷火驼进化石",
            "水晶灯火灵进化石",
            "喷火龙进化石 X",
            "喷火龙进化石 Y",
            "珍贵戒指",
            "布里卡隆进化石",
            "皮可西进化石",
            "彩色螺钉",
            "妖火红狐进化石",
            "蒂安希进化石",
            "毒藻龙进化石",
            "快龙进化石",
            "老翁龙进化石",
            "麻麻鳗鱼王进化石",
            "电梯钥匙",
            "炎武王进化石",
            "挖掘兔进化石",
            "列阵兵进化石",
            "大力鳄进化石",
            "花叶蒂进化石",
            "雪妖女进化石",
            "艾路雷朵进化石",
            "烈咬陆鲨进化石",
            "沙奈朵进化石",
            "耿鬼进化石",
            "冰鬼护进化石",
            "金色水黄鹂玩偶 Lv. 1",
            "金色水黄鹂玩偶 Lv. 2",
            "金色水黄鹂玩偶 Lv. 3",
            "绿色水黄鹂玩偶 Lv. 1",
            "绿色水黄鹂玩偶 Lv. 2",
            "绿色水黄鹂玩偶 Lv. 3",
            "甲贺忍蛙进化石",
            "暴鲤龙进化石",
            "摔角鹰人进化石",
            "赫拉克罗斯进化石",
            "黑鲁加进化石",
            "袋兽进化石",
            "客房202钥匙",
            "研究所钥匙卡A",
            "研究所钥匙卡B",
            "研究所钥匙卡C",
            "拉帝亚斯进化石",
            "拉帝欧斯进化石",
            "莉达的物品",
            "长耳兔进化石",
            "路卡利欧进化石",
            "乌贼王进化石",
            "雷电兽进化石",
            "大嘴娃进化石",
            "恰雷姆进化石",
            "超级环",
            "超级碎片",
            "大竺葵进化石",
            "巨金怪进化石",
            "超梦进化石 X",
            "超梦进化石 Y",
            "小石头",
            "大比鸟进化石",
            "粉色水黄鹂玩偶 Lv. 1",
            "粉色水黄鹂玩偶 Lv. 2",
            "粉色水黄鹂玩偶 Lv. 3",
            "凯罗斯进化石",
            "火炎狮进化石",
            "雷丘进化石 X",
            "雷丘进化石 Y",
            "红色水黄鹂玩偶 Lv. 1",
            "红色水黄鹂玩偶 Lv. 2",
            "红色水黄鹂玩偶 Lv. 3",
            "复苏小枝",
            "勾魂眼进化石",
            "暴飞龙进化石",
            "蜥蜴王进化石",
            "巨钳螳螂进化石",
            "蜈蚣王进化石",
            "师公进化石",
            "巨牙鲨进化石",
            "闪耀护符",
            "盔甲鸟进化石",
            "呆壳兽进化石",
            "宝星鸟进化石",
            "大钢蛇进化石",
            "超级密阿雷可丽饼",
            "巨沼怪进化石",
            "可口垃圾",
            "班吉拉进化石",
            "妙蛙花进化石",
            "大食花进化石",
            "基格尔德多面体",
            "基格尔德进化石"
        };

        public static bool IsBlocked(PKM pkm)
        {
            var held = pkm.HeldItem;
            if (held <= 0)
                return false;

            var namesEn = GameInfo.GetStrings("en");
            if (held >= 0 && held < namesEn.Item.Count)
            {
                var itemNameEn = namesEn.Item[held];
                if (BlockedItemNamesEn.Contains(itemNameEn))
                    return true;
            }

            var namesZh = GameInfo.GetStrings("zh-Hans");
            if (held >= 0 && held < namesZh.Item.Count)
            {
                var itemNameZh = namesZh.Item[held];
                return BlockedItemNamesZh.Contains(itemNameZh);
            }

            return false;
        }

        public static bool IsPLZAMode<TPoke>(PokeTradeHub<TPoke> hub) where TPoke : PKM, new()
        {
            // Detect PLZA based on the generic type (used by hub runner)
            return typeof(TPoke) == typeof(PA9);
        }
    }
}
