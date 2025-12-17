using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreStoragePriority
{
    /// <summary>
    /// 扩展的存储设置 - 为每个物品类别添加优先级权重
    /// </summary>
    public class ExtendedStorageSettings : IExposable
    {
        // 关联的原版存储设置
        public StorageSettings baseSettings;

        // 类别权重映射（ThingDef defName → 权重）
        private Dictionary<string, int> categoryPriorities = new Dictionary<string, int>();
        private Dictionary<string, int> thingCategoryPriorities = new Dictionary<string, int>();

        // 权重范围限制
        public const int MIN_PRIORITY = -3;
        public const int MAX_PRIORITY = +3;
        public const int DEFAULT_PRIORITY = 0;

        public ExtendedStorageSettings(StorageSettings baseSettings)
        {
            this.baseSettings = baseSettings;
        }

        /// <summary>
        /// 调整权重
        /// </summary>
        public void AdjustPriority(ThingDef def, int delta)
        {
            if (def == null) return;

            int current = GetPriority(def);
            int newValue = Mathf.Clamp(current + delta, MIN_PRIORITY, MAX_PRIORITY);

            SetPriority(def, newValue);
        }

        public void AdjustCategoryPriority(ThingCategoryDef catDef, int delta)
        {
            if (catDef == null) return;
            int current = GetCategoryPriority(catDef);
            int newValue = Mathf.Clamp(current + delta, MIN_PRIORITY, MAX_PRIORITY);
            SetCategoryPriority(catDef, newValue);
        }

        /// <summary>
        /// 设置权重
        /// </summary>
        public void SetPriority(ThingDef def, int value)
        {
            if (def == null) return;

            value = Mathf.Clamp(value, MIN_PRIORITY, MAX_PRIORITY);

            if (value == DEFAULT_PRIORITY)
            {
                categoryPriorities.Remove(def.defName);
            }
            else
            {
                categoryPriorities[def.defName] = value;
            }

            if (MoreStoragePriorityMod.settings.enableDebugLog)
            {
                Log.Message($"[MoreStoragePriority] {def.label} 权重设置为: {value}");
            }
        }

        public void SetCategoryPriority(ThingCategoryDef catDef, int value)
        {
            if (catDef == null) return;
            value = Mathf.Clamp(value, MIN_PRIORITY, MAX_PRIORITY);
            if (value == DEFAULT_PRIORITY)
            {
                thingCategoryPriorities.Remove(catDef.defName);
            }
            else
            {
                thingCategoryPriorities[catDef.defName] = value;
            }

            if (MoreStoragePriorityMod.settings.enableDebugLog)
            {
                Log.Message($"[MoreStoragePriority] {catDef.label} 类别权重设置为: {value}");
            }
        }

        /// <summary>
        /// 获取权重
        /// </summary>
        public int GetPriority(ThingDef def)
        {
            if (def == null) return DEFAULT_PRIORITY;
            return categoryPriorities.TryGetValue(def.defName, out int value) ? value : DEFAULT_PRIORITY;
        }

        public int GetCategoryPriority(ThingCategoryDef catDef)
        {
            if (catDef == null) return DEFAULT_PRIORITY;
            return thingCategoryPriorities.TryGetValue(catDef.defName, out int value) ? value : DEFAULT_PRIORITY;
        }

        /// <summary>
        /// 重置权重
        /// </summary>
        public void ResetPriority(ThingDef def)
        {
            if (def == null) return;
            categoryPriorities.Remove(def.defName);

            if (MoreStoragePriorityMod.settings.enableDebugLog)
            {
                Log.Message($"[MoreStoragePriority] {def.label} 权重已重置");
            }
        }

        public void ResetCategoryPriority(ThingCategoryDef catDef)
        {
            if (catDef == null) return;
            thingCategoryPriorities.Remove(catDef.defName);

            if (MoreStoragePriorityMod.settings.enableDebugLog)
            {
                Log.Message($"[MoreStoragePriority] {catDef.label} 类别权重已重置");
            }
        }

        /// <summary>
        /// 重置所有权重
        /// </summary>
        public void ResetAllPriorities()
        {
            categoryPriorities.Clear();
            thingCategoryPriorities.Clear();
            Log.Message("[MoreStoragePriority] 已重置所有类别权重");
        }

        /// <summary>
        /// 获取基础优先级
        /// </summary>
        public int GetBasePriority()
        {
            if (baseSettings == null)
                return 5; // 默认"重要"优先级

            return ToListPriority(baseSettings.Priority);
        }

        /// <summary>
        /// 获取实际优先级（基础优先级 + 类别权重）
        /// </summary>
        public int GetEffectivePriority(ThingDef def)
        {
            int basePriority = GetBasePriority();
            int weightThing = GetPriority(def);
            int weight = weightThing;

            if (weightThing == DEFAULT_PRIORITY && def != null && def.thingCategories != null)
            {
                int sum = 0;
                for (int i = 0; i < def.thingCategories.Count; i++)
                {
                    ThingCategoryDef cat = def.thingCategories[i];
                    sum += GetCategoryPriority(cat);
                }
                weight = Mathf.Clamp(sum, MIN_PRIORITY, MAX_PRIORITY);
            }

            int effective = basePriority + weight;

            if (MoreStoragePriorityMod.settings.enableDebugLog)
            {
                Log.Message($"[MoreStoragePriority] {def?.label ?? "null"} 实际优先级: {basePriority} + {weight} = {effective}");
            }

            return effective;
        }

        public static int ToListPriority(StoragePriority priority)
        {
            switch (priority)
            {
                case StoragePriority.Unstored: return 0;
                case StoragePriority.Low: return 1;
                case StoragePriority.Normal: return 2;
                case StoragePriority.Preferred: return 3;
                case StoragePriority.Important: return 4;
                case StoragePriority.Critical: return 5;
                default: return 0;
            }
        }

        /// <summary>
        /// 获取实际优先级（用于物品实例）
        /// </summary>
        public int GetEffectivePriorityForThing(Thing thing)
        {
            if (thing?.def == null) return GetBasePriority();
            return GetEffectivePriority(thing.def);
        }

        /// <summary>
        /// 检查是否有任何自定义权重
        /// </summary>
        public bool HasAnyCustomPriority()
        {
            return categoryPriorities.Count > 0 || thingCategoryPriorities.Count > 0;
        }

        /// <summary>
        /// 获取所有自定义权重的数量
        /// </summary>
        public int GetCustomPriorityCount()
        {
            return categoryPriorities.Count + thingCategoryPriorities.Count;
        }

        /// <summary>
        /// 复制所有权重到另一个存储设置
        /// </summary>
        public void CopyTo(ExtendedStorageSettings other)
        {
            if (other == null) return;

            other.categoryPriorities.Clear();
            foreach (var kvp in categoryPriorities)
            {
                other.categoryPriorities[kvp.Key] = kvp.Value;
            }

            other.thingCategoryPriorities.Clear();
            foreach (var kvp in thingCategoryPriorities)
            {
                other.thingCategoryPriorities[kvp.Key] = kvp.Value;
            }

            Log.Message($"[MoreStoragePriority] 已复制 {categoryPriorities.Count} 个权重设置");
        }

        /// <summary>
        /// 保存/加载
        /// </summary>
        public void ExposeData()
        {
            Scribe_Collections.Look(ref categoryPriorities, "categoryPriorities", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref thingCategoryPriorities, "thingCategoryPriorities", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (categoryPriorities == null)
                {
                    categoryPriorities = new Dictionary<string, int>();
                }
                if (thingCategoryPriorities == null)
                {
                    thingCategoryPriorities = new Dictionary<string, int>();
                }

                // 清理无效的 defName（来自已卸载的 Mod）
                List<string> invalidKeys = new List<string>();
                foreach (var key in categoryPriorities.Keys)
                {
                    if (DefDatabase<ThingDef>.GetNamedSilentFail(key) == null)
                    {
                        invalidKeys.Add(key);
                    }
                }

                foreach (var key in invalidKeys)
                {
                    categoryPriorities.Remove(key);
                    Log.Warning($"[MoreStoragePriority] 移除无效的类别权重: {key}");
                }

                List<string> invalidCatKeys = new List<string>();
                foreach (var key in thingCategoryPriorities.Keys)
                {
                    if (DefDatabase<ThingCategoryDef>.GetNamedSilentFail(key) == null)
                    {
                        invalidCatKeys.Add(key);
                    }
                }
                foreach (var key in invalidCatKeys)
                {
                    thingCategoryPriorities.Remove(key);
                    Log.Warning($"[MoreStoragePriority] 移除无效的物品类别权重: {key}");
                }
            }
        }

        /// <summary>
        /// 获取 Tooltip 文本
        /// </summary>
        public string GetTooltipText(ThingDef def)
        {
            int weight = GetPriority(def);
            int basePriority = GetBasePriority();
            int effective = GetEffectivePriority(def);

            string baseText = $"{def.label}\n\n";
            baseText += $"当前权重: {weight:+0;-#;0}\n";
            baseText += $"基础优先级: {basePriority}\n";
            baseText += $"实际优先级: {effective}\n\n";
            baseText += "左键: +1 | 右键: -1 | 中键: 重置";

            return baseText;
        }

        public string GetTooltipTextForCategory(ThingCategoryDef catDef)
        {
            int weight = GetCategoryPriority(catDef);
            int basePriority = GetBasePriority();
            string name = catDef?.label ?? "类别";
            int effectiveSample = basePriority + weight;
            string baseText = $"{name}\n\n";
            baseText += $"当前类别权重: {weight:+0;-#;0}\n";
            baseText += $"基础优先级: {basePriority}\n";
            baseText += $"示例实际优先级: {effectiveSample}\n\n";
            baseText += "左键: +1 | 右键: -1 | 中键: 重置";
            return baseText;
        }
    }
}
