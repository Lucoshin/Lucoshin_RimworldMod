using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoreStoragePriority
{
    /// <summary>
    /// 全局管理器 - 管理所有存储设置的扩展数据
    /// </summary>
    public static class StorageSettingsManager
    {
        // 存储设置 → 扩展设置的映射
        private static Dictionary<StorageSettings, ExtendedStorageSettings> extendedSettings =
            new Dictionary<StorageSettings, ExtendedStorageSettings>();

        /// <summary>
        /// 获取或创建扩展设置
        /// </summary>
        public static ExtendedStorageSettings GetExtendedSettings(StorageSettings baseSettings)
        {
            if (baseSettings == null)
            {
                Log.Warning("[MoreStoragePriority] 尝试获取 null StorageSettings 的扩展设置");
                return null;
            }

            if (!extendedSettings.TryGetValue(baseSettings, out ExtendedStorageSettings extended))
            {
                extended = new ExtendedStorageSettings(baseSettings);
                extendedSettings[baseSettings] = extended;

                if (MoreStoragePriorityMod.settings.enableDebugLog)
                {
                    Log.Message($"[MoreStoragePriority] 创建新的扩展设置，当前总数: {extendedSettings.Count}");
                }
            }

            return extended;
        }

        /// <summary>
        /// 移除扩展设置（当存储区被删除时）
        /// </summary>
        public static void RemoveExtendedSettings(StorageSettings baseSettings)
        {
            if (baseSettings == null) return;

            if (extendedSettings.Remove(baseSettings))
            {
                if (MoreStoragePriorityMod.settings.enableDebugLog)
                {
                    Log.Message($"[MoreStoragePriority] 移除扩展设置，剩余: {extendedSettings.Count}");
                }
            }
        }

        /// <summary>
        /// 清理所有扩展设置
        /// </summary>
        public static void Clear()
        {
            extendedSettings.Clear();
            Log.Message("[MoreStoragePriority] 已清理所有扩展设置");
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static string GetStatistics()
        {
            int totalSettings = extendedSettings.Count;
            int settingsWithCustomPriorities = 0;
            int totalCustomPriorities = 0;

            foreach (var extended in extendedSettings.Values)
            {
                if (extended.HasAnyCustomPriority())
                {
                    settingsWithCustomPriorities++;
                    totalCustomPriorities += extended.GetCustomPriorityCount();
                }
            }

            return $"扩展设置总数: {totalSettings}\n" +
                   $"使用自定义权重的存储区: {settingsWithCustomPriorities}\n" +
                   $"自定义权重总数: {totalCustomPriorities}";
        }

        /// <summary>
        /// 保存所有扩展设置（在游戏保存时调用）
        /// </summary>
        public static void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 保存时：创建可序列化的数据列表
                List<StorageSettings> keys = new List<StorageSettings>(extendedSettings.Keys);
                List<ExtendedStorageSettings> values = new List<ExtendedStorageSettings>(extendedSettings.Values);

                // 只保存有自定义权重的设置
                for (int i = keys.Count - 1; i >= 0; i--)
                {
                    if (!values[i].HasAnyCustomPriority())
                    {
                        keys.RemoveAt(i);
                        values.RemoveAt(i);
                    }
                }

                Scribe_Collections.Look(ref keys, "storageSettingsKeys", LookMode.Reference);
                Scribe_Collections.Look(ref values, "extendedSettingsValues", LookMode.Deep);

                Log.Message($"[MoreStoragePriority] 保存了 {keys?.Count ?? 0} 个扩展设置");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 加载时：重建字典
                List<StorageSettings> keys = null;
                List<ExtendedStorageSettings> values = null;

                Scribe_Collections.Look(ref keys, "storageSettingsKeys", LookMode.Reference);
                Scribe_Collections.Look(ref values, "extendedSettingsValues", LookMode.Deep);

                if (keys != null && values != null)
                {
                    extendedSettings.Clear();
                    for (int i = 0; i < keys.Count && i < values.Count; i++)
                    {
                        if (keys[i] != null && values[i] != null)
                        {
                            values[i].baseSettings = keys[i];
                            extendedSettings[keys[i]] = values[i];
                        }
                    }

                    Log.Message($"[MoreStoragePriority] 加载了 {extendedSettings.Count} 个扩展设置");
                }
            }
        }
    }
}