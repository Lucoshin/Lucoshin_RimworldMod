using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Verse;

namespace MoreStoragePriority
{
    /// <summary>
    /// Mod 入口类
    /// </summary>
    public class MoreStoragePriorityMod : Mod
    {
        public static MoreStoragePrioritySettings settings;

        public MoreStoragePriorityMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<MoreStoragePrioritySettings>();

            Log.Message("[MoreStoragePriority] 类别优先级 Mod 加载成功");

            var harmony = new Harmony("com.yourname.MoreStoragePriority");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("[MoreStoragePriority] Harmony 补丁应用完成");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("类别优先级 Mod");
            listingStandard.Gap();

            listingStandard.CheckboxLabeled(
                "启用调试日志",
                ref settings.enableDebugLog,
                "显示详细的调试信息"
            );

            listingStandard.Gap();
            listingStandard.Label("使用说明：");
            listingStandard.Label("• 在存储区设置中，每个物品类别右侧会显示数字框");
            listingStandard.Label("• 左键点击：提高优先级（+1）");
            listingStandard.Label("• 右键点击：降低优先级（-1）");
            listingStandard.Label("• 中键点击：重置为默认（0）");

            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "类别优先级 (Category Priority)";
        }
    }

    /// <summary>
    /// Mod 设置
    /// </summary>
    public class MoreStoragePrioritySettings : ModSettings
    {
        public bool enableDebugLog = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableDebugLog, "enableDebugLog", false);
        }
    }
}
