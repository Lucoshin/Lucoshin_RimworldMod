using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace MoreStoragePriority.Patches
{
    /// <summary>
    /// Hook ThingFilterUI 来在每个类别右侧绘制优先级框
    /// </summary>
    [HarmonyPatch(typeof(ThingFilterUI))]
    public static class Patch_ThingFilterUI
    {
        // 当前正在编辑的存储设置
        public static StorageSettings currentStorageSettings;
        public static ExtendedStorageSettings currentExtendedSettings;

        // 用于追踪绘制的行
        private static float lastY = 0f;
        public static ThingDef lastThingDef = null;

        /// <summary>
        /// Hook DoThingFilterConfigWindow - 在窗口打开时获取存储设置
        /// </summary>
        [HarmonyPatch("DoThingFilterConfigWindow")]
        [HarmonyPrefix]
        static void DoThingFilterConfigWindow_Prefix(ThingFilter filter)
        {
            try
            {
                currentStorageSettings = FindStorageSettings(filter);

                if (currentStorageSettings != null)
                {
                    currentExtendedSettings = StorageSettingsManager.GetExtendedSettings(currentStorageSettings);
                }
                else
                {
                    currentExtendedSettings = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreStoragePriority] DoThingFilterConfigWindow_Prefix 出错: {ex}");
            }
        }

        [HarmonyPatch("DoThingFilterConfigWindow")]
        [HarmonyPostfix]
        static void DoThingFilterConfigWindow_Postfix()
        {
            currentStorageSettings = null;
            currentExtendedSettings = null;
        }

        /// <summary>
        /// Hook DrawHitPointsFilterConfig - 在绘制完 HP 过滤后绘制优先级说明
        /// </summary>
        [HarmonyPatch("DrawHitPointsFilterConfig")]
        [HarmonyPostfix]
        static void DrawHitPointsFilterConfig_Postfix(ref float y, float width)
        {
            if (currentExtendedSettings == null) return;

            // 在顶部添加说明文字
            Rect infoRect = new Rect(0f, y, width, 60f);

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;

            string infoText = "类别优先级：右侧数字框可调整优先级\n";
            infoText += "左键+1 | 右键-1 | 中键重置 | Shift快捷键";

            if (currentExtendedSettings.HasAnyCustomPriority())
            {
                infoText += $"\n已自定义 {currentExtendedSettings.GetCustomPriorityCount()} 个类别";
            }

            Widgets.Label(infoRect, infoText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            y += 60f;
        }

        /// <summary>
        /// 寻找 ThingFilter 对应的 StorageSettings
        /// </summary>
        private static StorageSettings FindStorageSettings(ThingFilter filter)
        {
            if (filter == null) return null;

            if (Current.Game?.Maps == null) return null;

            foreach (Map map in Current.Game.Maps)
            {
                if (map?.listerBuildings?.allBuildingsColonist == null) continue;

                // 检查存储建筑
                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is IStoreSettingsParent storeParent)
                    {
                        StorageSettings settings = storeParent.GetStoreSettings();
                        if (settings?.filter == filter)
                        {
                            return settings;
                        }
                    }
                }

                // 检查存储区
                if (map.zoneManager?.AllZones != null)
                {
                    foreach (Zone zone in map.zoneManager.AllZones)
                    {
                        if (zone is Zone_Stockpile stockpile)
                        {
                            if (stockpile.settings?.filter == filter)
                            {
                                return stockpile.settings;
                            }
                        }
                    }
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(Listing_TreeThingFilter))]
    [HarmonyPatch("DoCategory")]
    public static class Patch_Listing_TreeThingFilter_DoCategory
    {
        static float headerY;
        static void Prefix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node)
        {
            headerY = __instance.CurHeight;
        }
        static void Postfix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node)
        {
            if (!MSP_SessionSettings.Enabled) return;
            if (Patch_ThingFilterUI.currentExtendedSettings == null) return;
            if (node?.catDef == null) return;
            try
            {
                Rect lineRect = new Rect(
                    0f,
                    headerY,
                    __instance.ColumnWidth,
                    __instance.lineHeight
                );
                UI_PriorityBox.DrawCategoryPriorityBox(lineRect, node.catDef, Patch_ThingFilterUI.currentExtendedSettings);
            }
            catch (System.Exception ex)
            {
                if (MoreStoragePriorityMod.settings.enableDebugLog)
                {
                    Log.Warning($"[MoreStoragePriority] DoCategory_Postfix 出错: {ex}");
                }
            }
        }
    }
 

    /// <summary>
    /// Hook Listing_TreeThingFilter.DoThingDef - 绘制单个 ThingDef
    /// </summary>
    [HarmonyPatch(typeof(Listing_TreeThingFilter))]
    [HarmonyPatch("DoThingDef")]
    public static class Patch_Listing_TreeThingFilter_DoThingDef
    {
        static void Prefix(Listing_TreeThingFilter __instance, ThingDef tDef, int nestLevel)
        {
            // 记录当前绘制的 ThingDef
            Patch_ThingFilterUI.lastThingDef = tDef;
        }

        static void Postfix(Listing_TreeThingFilter __instance, ThingDef tDef, int nestLevel)
        {
            if (!MSP_SessionSettings.Enabled) return;
            if (Patch_ThingFilterUI.currentExtendedSettings == null) return;
            if (tDef == null) return;

            try
            {
                // 获取当前的绘制位置
                // Listing_TreeThingFilter 继承自 Listing_Tree
                // curY 字段保存当前的 Y 坐标
                float curY = __instance.CurHeight;

                // 计算行的矩形
                Rect lineRect = new Rect(
                    0f,
                    curY - __instance.lineHeight,
                    __instance.ColumnWidth,
                    __instance.lineHeight
                );

                // 绘制优先级框
                UI_PriorityBox.DrawPriorityBox(lineRect, tDef, Patch_ThingFilterUI.currentExtendedSettings);
            }
            catch (Exception ex)
            {
                if (MoreStoragePriorityMod.settings.enableDebugLog)
                {
                    Log.Warning($"[MoreStoragePriority] DoThingDef_Postfix 出错: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Hook Listing_TreeThingFilter.DoSpecialFilter - 处理特殊过滤器
    /// </summary>
    [HarmonyPatch(typeof(Listing_TreeThingFilter))]
    [HarmonyPatch("DoSpecialFilter")]
    public static class Patch_Listing_TreeThingFilter_DoSpecialFilter
    {
        static void Postfix(Listing_TreeThingFilter __instance, SpecialThingFilterDef sfDef, int nestLevel)
        {
            // 特殊过滤器（如"允许腐烂"）也可以添加优先级
            // 暂时不处理
        }
    }
}
