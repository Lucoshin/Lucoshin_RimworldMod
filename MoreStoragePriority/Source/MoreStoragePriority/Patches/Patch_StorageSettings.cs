using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.Sound;
using System.Reflection;

namespace MoreStoragePriority.Patches
{
    /// <summary>
    /// Hook StorageSettings 的保存/加载
    /// </summary>
    [HarmonyPatch(typeof(StorageSettings))]
    [HarmonyPatch(nameof(StorageSettings.ExposeData))]
    public static class Patch_StorageSettings_ExposeData
    {
        static void Postfix(StorageSettings __instance)
        {
            // 在保存/加载 StorageSettings 时，同时处理扩展设置
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                ExtendedStorageSettings extended = StorageSettingsManager.GetExtendedSettings(__instance);

                if (extended != null)
                {
                    extended.ExposeData();
                }
            }
        }
    }

    /// <summary>
    /// 在 ITab_Storage 顶部绘制开关按钮（本局游戏生效）
    /// </summary>
    [HarmonyPatch(typeof(ITab_Storage))]
    [HarmonyPatch("FillTab")]
    public static class Patch_ITab_Storage_FillTab
    {
        static void Postfix(ITab_Storage __instance)
        {
            try
            {
                Vector2 tabSize = (Vector2)HarmonyLib.AccessTools.Field(typeof(InspectTabBase), "size").GetValue(__instance);
                string label = MSP_SessionSettings.Enabled ? "类别权重: 开" : "类别权重: 关";
                var oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                float calcWidth = Text.CalcSize(label).x + 16f;
                Text.Font = oldFont;
                float btnWidth = Mathf.Clamp(calcWidth, 72f, 110f);
                Text.Font = GameFont.Small;
                float priorityBtnWidth = Text.CalcSize("优先级: 普通").x + 24f;
                float baseHeight = Text.CalcSize("优先级: 普通").y;
                Text.Font = oldFont;
                float btnHeight = Mathf.Clamp(Mathf.Round(baseHeight) + 6f, 22f, 26f);
                float leftCandidate = 10f + priorityBtnWidth + 8f;
                float rightSafe = Mathf.Clamp(tabSize.x * 0.02f, 28f, 56f);
                float rightCandidate = tabSize.x - btnWidth - rightSafe;
                float topY = 8f;
                float secondY = topY + btnHeight + 6f;
                float finalX = Mathf.Max(10f, rightCandidate);
                float finalY = rightCandidate <= leftCandidate + 4f ? secondY : topY;
                Rect btnRect = new Rect(finalX, finalY, btnWidth, btnHeight);
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(btnRect, label))
                {
                    MSP_SessionSettings.Toggle();
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                Text.Font = oldFont;
                TooltipHandler.TipRegion(btnRect, "启用/禁用更多优先级（仅当前存档）");
            }
            catch (System.Exception ex)
            {
                if (MoreStoragePriorityMod.settings.enableDebugLog)
                {
                    Log.Warning($"[MoreStoragePriority] 绘制开关按钮失败: {ex}");
                }
            }
        }
    }
 

    /// <summary>
    /// Hook Building_Storage 销毁时清理数据
    /// </summary>
    [HarmonyPatch(typeof(Building_Storage))]
    [HarmonyPatch(nameof(Building_Storage.Destroy))]
    public static class Patch_Building_Storage_Destroy
    {
        static void Prefix(Building_Storage __instance)
        {
            if (__instance?.settings != null)
            {
                StorageSettingsManager.RemoveExtendedSettings(__instance.settings);
            }
        }
    }

    /// <summary>
    /// Hook ThingFilter.SetDisallowAll - 原版“全部清除”同步清理权重
    /// </summary>
    [HarmonyPatch(typeof(ThingFilter))]
    [HarmonyPatch(nameof(ThingFilter.SetDisallowAll))]
    public static class Patch_ThingFilter_SetDisallowAll
    {
        static void Postfix()
        {
            if (Patch_ThingFilterUI.currentExtendedSettings != null)
            {
                Patch_ThingFilterUI.currentExtendedSettings.ResetAllPriorities();
            }
        }
    }

    /// <summary>
    /// Hook StorageSettings.CopyFrom - 同步复制扩展权重
    /// </summary>
    [HarmonyPatch(typeof(StorageSettings))]
    [HarmonyPatch(nameof(StorageSettings.CopyFrom))]
    public static class Patch_StorageSettings_CopyFrom
    {
        static void Postfix(StorageSettings __instance, StorageSettings other)
        {
            if (!MSP_SessionSettings.Enabled) return;
            ExtendedStorageSettings target = StorageSettingsManager.GetExtendedSettings(__instance);
            ExtendedStorageSettings source = StorageSettingsManager.GetExtendedSettings(other);
            if (target != null && source != null)
            {
                source.CopyTo(target);
            }
        }
    }
 
}
