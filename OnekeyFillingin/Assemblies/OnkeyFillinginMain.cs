using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace OneKeyFillingin
{
    // Mod 初始化类
    public class OneKeyMod : Mod
    {
        public static OneKeyMod Instance;
        public OneKeySettings Settings;
        public OneKeyMod(ModContentPack content) : base(content)
        {
            // Log.Message("[OneKey] OneKeyFillingin (一键装填) 加载成功");
            var harmony = new Harmony("com.yourname.onekeyfillingin");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Instance = this;
            Settings = GetSettings<OneKeySettings>();
            PresetClipboard.LoadFromSettings(Settings);
        }
    }

    public class OneKeySettings : ModSettings
    {
        public List<PresetRecord> Presets = new List<PresetRecord>();
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Presets, "Presets", LookMode.Deep);
        }
    }

    public class PresetItemDTO : IExposable
    {
        public string defName;
        public int count;
        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "def");
            Scribe_Values.Look(ref count, "count");
        }
    }

    public class PresetRecord : IExposable
    {
        public string name;
        public List<PresetItemDTO> items = new List<PresetItemDTO>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref items, "items", LookMode.Deep);
        }
    }

    public static class OneKeyIcons
    {
        private static Texture2D main;
        private static Texture2D dropdown;
        private static Texture2D Load(string path)
        {
            var t = ContentFinder<Texture2D>.Get(path, false);
            return t ?? TexCommand.GatherSpotActive;
        }
        public static Texture2D Main => main ?? (main = Load("icon_main"));
        public static Texture2D Dropdown => dropdown ?? (dropdown = Load("icon_dropdown"));
    }

    // ================== 数据存储：剪贴板（优化版）==================
    public static class LoadoutClipboard
    {
        public struct SavedItem
        {
            public ThingDef def;
            public int count;
        }

        public struct LoadoutKey
        {
            public int mapID;
            public int groupID;

            public LoadoutKey(int mapID, int groupID)
            {
                this.mapID = mapID;
                this.groupID = groupID;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is LoadoutKey)) return false;
                LoadoutKey other = (LoadoutKey)obj;
                return this.mapID == other.mapID && this.groupID == other.groupID;
            }

            public override int GetHashCode()
            {
                return mapID.GetHashCode() ^ groupID.GetHashCode();
            }

            public override string ToString()
            {
                return $"Map{mapID}_Group{groupID}";
            }
        }

        // 主存储：按 (mapID, groupID) 存储
        private static Dictionary<LoadoutKey, List<SavedItem>> loadoutsByGroup =
            new Dictionary<LoadoutKey, List<SavedItem>>();

        // 后备存储：全局最近使用的方案
        private static List<SavedItem> lastGlobalLoadout = new List<SavedItem>();
        private static DateTime lastGlobalLoadoutTime = DateTime.MinValue;

        // 方案使用时间记录（用于清理旧数据）
        private static Dictionary<LoadoutKey, DateTime> loadoutLastUsed =
            new Dictionary<LoadoutKey, DateTime>();

        /// <summary>
        /// 保存指定地图和组的装载方案
        /// </summary>
        public static void SaveLoadout(Map map, int groupID, List<SavedItem> items)
        {
            if (map == null || items == null) return;

            LoadoutKey key = new LoadoutKey(map.uniqueID, groupID);

            // 保存到主存储
            if (loadoutsByGroup.ContainsKey(key))
            {
                loadoutsByGroup[key] = new List<SavedItem>(items);
            }
            else
            {
                loadoutsByGroup.Add(key, new List<SavedItem>(items));
            }

            // 更新使用时间
            loadoutLastUsed[key] = DateTime.Now;

            // 同时保存到全局后备
            lastGlobalLoadout = new List<SavedItem>(items);
            lastGlobalLoadoutTime = DateTime.Now;

            // Log.Message($"[OneKey] 已保存方案到 {key}（{items.Count} 项）");

            // 清理超过 30 天未使用的旧方案（防止内存泄漏）
            CleanOldLoadouts();
        }

        /// <summary>
        /// 获取指定地图和组的装载方案
        /// </summary>
        public static List<SavedItem> GetLoadout(Map map, int groupID)
        {
            if (map == null) return new List<SavedItem>();

            LoadoutKey key = new LoadoutKey(map.uniqueID, groupID);

            // 1. 优先返回精确匹配的方案
            if (loadoutsByGroup.ContainsKey(key))
            {
                loadoutLastUsed[key] = DateTime.Now; // 更新使用时间
                return loadoutsByGroup[key];
            }

            // 2. 尝试在同一地图找相似的方案（groupID 可能变化了）
            var samMapLoadouts = loadoutsByGroup
                .Where(kvp => kvp.Key.mapID == map.uniqueID)
                .OrderByDescending(kvp => loadoutLastUsed.ContainsKey(kvp.Key) ? loadoutLastUsed[kvp.Key] : DateTime.MinValue)
                .ToList();

            if (samMapLoadouts.Any())
            {
                // Log.Message($"[OneKey] {key} 无方案，使用同地图最近的方案: {samMapLoadouts.First().Key}");
                return samMapLoadouts.First().Value;
            }

            // 3. 返回全局最近使用的方案（后备方案）
            if (lastGlobalLoadout.Count > 0)
            {
                // Log.Message($"[OneKey] {key} 无方案，使用全局最近方案（{lastGlobalLoadoutTime}）");
                return lastGlobalLoadout;
            }

            return new List<SavedItem>();
        }

        /// <summary>
        /// 检查指定地图和组是否有保存的方案
        /// </summary>
        public static bool HasData(Map map, int groupID)
        {
            if (map == null) return false;

            LoadoutKey key = new LoadoutKey(map.uniqueID, groupID);

            // 有精确匹配的方案
            if (loadoutsByGroup.ContainsKey(key) && loadoutsByGroup[key].Count > 0)
                return true;

            // 或者同地图有其他方案
            if (loadoutsByGroup.Any(kvp => kvp.Key.mapID == map.uniqueID && kvp.Value.Count > 0))
                return true;

            // 或者有全局后备方案
            return lastGlobalLoadout.Count > 0;
        }

        /// <summary>
        /// 清空指定地图和组的方案
        /// </summary>
        public static void ClearLoadout(Map map, int groupID)
        {
            if (map == null) return;

            LoadoutKey key = new LoadoutKey(map.uniqueID, groupID);

            if (loadoutsByGroup.ContainsKey(key))
            {
                loadoutsByGroup.Remove(key);
            }

            if (loadoutLastUsed.ContainsKey(key))
            {
                loadoutLastUsed.Remove(key);
            }
        }

        /// <summary>
        /// 清理超过 30 天未使用的旧方案
        /// </summary>
        private static void CleanOldLoadouts()
        {
            try
            {
                DateTime threshold = DateTime.Now.AddDays(-30);
                List<LoadoutKey> toRemove = new List<LoadoutKey>();

                foreach (var kvp in loadoutLastUsed)
                {
                    if (kvp.Value < threshold)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in toRemove)
                {
                    loadoutsByGroup.Remove(key);
                    loadoutLastUsed.Remove(key);
                    // Log.Message($"[OneKey] 清理旧方案: {key}");
                }

                if (toRemove.Count > 0)
                {
                    // Log.Message($"[OneKey] 已清理 {toRemove.Count} 个超过 30 天未使用的方案");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[OneKey] 清理旧方案时出错: {ex}");
            }
        }

        /// <summary>
        /// 获取方案来源描述（用于 UI 显示）
        /// </summary>
        public static string GetLoadoutSourceDescription(Map map, int groupID)
        {
            if (map == null) return "无方案";

            LoadoutKey key = new LoadoutKey(map.uniqueID, groupID);

            // 精确匹配
            if (loadoutsByGroup.ContainsKey(key))
            {
                DateTime time = loadoutLastUsed.ContainsKey(key) ? loadoutLastUsed[key] : DateTime.MinValue;
                return $"此组的上次方案 ({GetTimeDescription(time)})";
            }

            // 同地图其他组
            var sameMapLoadouts = loadoutsByGroup
                .Where(kvp => kvp.Key.mapID == map.uniqueID)
                .ToList();

            if (sameMapLoadouts.Any())
            {
                var mostRecent = sameMapLoadouts
                    .OrderByDescending(kvp => loadoutLastUsed.ContainsKey(kvp.Key) ? loadoutLastUsed[kvp.Key] : DateTime.MinValue)
                    .First();

                DateTime time = loadoutLastUsed.ContainsKey(mostRecent.Key) ? loadoutLastUsed[mostRecent.Key] : DateTime.MinValue;
                return $"同地图其他组的方案 ({GetTimeDescription(time)})";
            }

            // 全局后备
            if (lastGlobalLoadout.Count > 0)
            {
                return $"全局最近方案 ({GetTimeDescription(lastGlobalLoadoutTime)})";
            }

            return "无方案";
        }

        /// <summary>
        /// 将时间转换为友好描述
        /// </summary>
        private static string GetTimeDescription(DateTime time)
        {
            if (time == DateTime.MinValue) return "未知时间";

            TimeSpan span = DateTime.Now - time;

            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} 天前";

            return time.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 获取所有保存的方案统计信息（用于调试）
        /// </summary>
        public static string GetStatistics()
        {
            return $"共 {loadoutsByGroup.Count} 个方案，全局后备方案: {(lastGlobalLoadout.Count > 0 ? "有" : "无")}";
        }
    }

    public static class PresetClipboard
    {
        private static Dictionary<string, List<PresetItemDTO>> rawPresets =
            new Dictionary<string, List<PresetItemDTO>>();
        private static Dictionary<string, List<LoadoutClipboard.SavedItem>> presets =
            new Dictionary<string, List<LoadoutClipboard.SavedItem>>();
        private static Dictionary<string, DateTime> presetTimes =
            new Dictionary<string, DateTime>();
        public static void LoadFromSettings(OneKeySettings settings)
        {
            rawPresets.Clear();
            presets.Clear();
            presetTimes.Clear();
            if (settings == null || settings.Presets == null) return;
            foreach (var rec in settings.Presets)
            {
                rawPresets[rec.name] = rec.items != null ? new List<PresetItemDTO>(rec.items) : new List<PresetItemDTO>();
                presetTimes[rec.name] = DateTime.Now;
            }
        }

        private static void SaveSettings()
        {
            if (OneKeyMod.Instance == null || OneKeyMod.Instance.Settings == null) return;
            var list = new List<PresetRecord>();
            foreach (var kv in rawPresets)
            {
                var rec = new PresetRecord { name = kv.Key };
                rec.items = new List<PresetItemDTO>(kv.Value ?? new List<PresetItemDTO>());
                list.Add(rec);
            }
            OneKeyMod.Instance.Settings.Presets = list;
            OneKeyMod.Instance.WriteSettings();
        }

        public static bool HasAny() => (rawPresets.Count > 0) || (presets.Count > 0);

        public static List<string> GetPresetNames()
        {
            return presetTimes
                .OrderByDescending(k => presetTimes[k.Key])
                .Select(k => k.Key)
                .ToList();
        }

        private static void EnsureResolved(string name)
        {
            if (presets.ContainsKey(name)) return;
            var resolved = new List<LoadoutClipboard.SavedItem>();
            if (rawPresets.ContainsKey(name))
            {
                foreach (var dto in rawPresets[name])
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(dto.defName);
                    if (def != null)
                    {
                        resolved.Add(new LoadoutClipboard.SavedItem { def = def, count = dto.count });
                    }
                }
            }
            presets[name] = resolved;
        }

        public static void SavePreset(string name, List<LoadoutClipboard.SavedItem> items)
        {
            if (string.IsNullOrEmpty(name) || items == null) return;
            presets[name] = new List<LoadoutClipboard.SavedItem>(items);
            rawPresets[name] = items.Select(i => new PresetItemDTO { defName = i.def.defName, count = i.count }).ToList();
            presetTimes[name] = DateTime.Now;
            // Log.Message($"[OneKey] 已保存预设: {name}（{items.Count} 项）");
            SaveSettings();
        }

        public static List<LoadoutClipboard.SavedItem> GetPreset(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<LoadoutClipboard.SavedItem>();
            if (rawPresets.ContainsKey(name))
            {
                EnsureResolved(name);
                presetTimes[name] = DateTime.Now;
                return new List<LoadoutClipboard.SavedItem>(presets[name]);
            }
            return new List<LoadoutClipboard.SavedItem>();
        }

        public static void DeletePreset(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (rawPresets.ContainsKey(name)) rawPresets.Remove(name);
            if (presets.ContainsKey(name)) presets.Remove(name);
            if (presetTimes.ContainsKey(name)) presetTimes.Remove(name);
            // Log.Message($"[OneKey] 已删除预设: {name}");
            SaveSettings();
        }
    }

    // ================== 核心逻辑工具 ==================
    public static class SmartLoadUtility
    {
        public static void LoadFromItems(CompTransporter clickedTransporter, List<LoadoutClipboard.SavedItem> items, string sourceDesc)
        {
            if (clickedTransporter == null || clickedTransporter.Map == null) return;
            Map map = clickedTransporter.Map;
            int groupID = clickedTransporter.groupID;

            List<CompTransporter> selectedTransporters = GetSelectedTransporters(map, groupID);
            if (selectedTransporters.Count == 0)
            {
                selectedTransporters.Add(clickedTransporter);
                // Log.Message("[OneKey] 未检测到多选，只装载当前点击的运输舱");
            }
            else
            {
                // Log.Message($"[OneKey] 检测到用户选中了 {selectedTransporters.Count} 个运输舱");
            }

            foreach (CompTransporter comp in selectedTransporters) comp.CancelLoad();

            Dialog_LoadTransporters dialog = new Dialog_LoadTransporters(map, selectedTransporters);
            try
            {
                var postOpenMethod = typeof(Dialog_LoadTransporters).GetMethod("PostOpen");
                if (postOpenMethod != null) postOpenMethod.Invoke(dialog, null);
            }
            catch (Exception ex)
            {
                Log.Error($"[OneKey] 调用 PostOpen 失败: {ex}");
                return;
            }

            List<TransferableOneWay> allTransferables = null;
            try
            {
                allTransferables = Traverse.Create(dialog)
                    .Field("transferables")
                    .GetValue<List<TransferableOneWay>>();
            }
            catch (Exception ex)
            {
                Log.Error($"[OneKey] 无法获取 transferables 字段: {ex}");
            }

            if (allTransferables == null || allTransferables.Count == 0)
            {
                Log.Error("[OneKey] transferables 列表为空或获取失败");
                dialog.Close(false);
                return;
            }

            bool anyLoaded = false;
            List<string> missing = new List<string>();

            foreach (var saved in items)
            {
                TransferableOneWay match = allTransferables.FirstOrDefault(t => t.ThingDef == saved.def);
                if (match == null)
                {
                    missing.Add($"{saved.def.label} x{saved.count}");
                    continue;
                }
                int available = match.MaxCount;
                int take = Mathf.Min(saved.count, available);
                if (take > 0)
                {
                    match.AdjustTo(take);
                    anyLoaded = true;
                }
                if (available < saved.count)
                {
                    missing.Add($"{saved.def.label} 缺 {saved.count - available}");
                }
            }

            if (anyLoaded)
            {
                bool accepted = false;
                try
                {
                    accepted = Traverse.Create(dialog)
                        .Method("TryAccept")
                        .GetValue<bool>();
                }
                catch (Exception ex)
                {
                    Log.Error($"[OneKey] 调用 TryAccept 失败: {ex}");
                }

                if (accepted)
                {
                    if (missing.Count > 0)
                    {
                        Messages.Message(
                            $"已装载 {sourceDesc}，但资源不足：" + string.Join(", ", missing.Take(3)),
                            MessageTypeDefOf.CautionInput,
                            false
                        );
                    }
                    else
                    {
                        Messages.Message(
                            $"已成功装载 {sourceDesc}。",
                            MessageTypeDefOf.PositiveEvent,
                            false
                        );
                    }
                }
                else
                {
                    Messages.Message("装载任务因重量或容量限制失败。", MessageTypeDefOf.RejectInput, false);
                }
            }
            else
            {
                Messages.Message("地图上没有可用资源满足方案清单。", MessageTypeDefOf.RejectInput, false);
            }

            dialog.Close(false);
        }

        /// <summary>
        /// 装载上次方案
        /// </summary>
        public static void LoadPrevious(CompTransporter clickedTransporter)
        {
            if (clickedTransporter == null || clickedTransporter.Map == null)
                return;

            Map map = clickedTransporter.Map;
            int groupID = clickedTransporter.groupID;

            if (!LoadoutClipboard.HasData(map, groupID))
            {
                Messages.Message("无可用的装载方案。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 获取用户实际选中的运输舱
            List<CompTransporter> selectedTransporters = GetSelectedTransporters(map, groupID);

            // 如果没有找到选中的运输舱，则只使用点击的这一个
            if (selectedTransporters.Count == 0)
            {
                selectedTransporters.Add(clickedTransporter);
                // Log.Message("[OneKey] 未检测到多选，只装载当前点击的运输舱");
            }
            else
            {
                // Log.Message($"[OneKey] 检测到用户选中了 {selectedTransporters.Count} 个运输舱");
            }

            // 取消旧任务
            foreach (CompTransporter comp in selectedTransporters)
            {
                comp.CancelLoad();
            }

            // 实例化 Dialog_LoadTransporters
            Dialog_LoadTransporters dialog = new Dialog_LoadTransporters(map, selectedTransporters);

            // 强制初始化 transferables（调用 PostOpen）
            try
            {
                var postOpenMethod = typeof(Dialog_LoadTransporters).GetMethod("PostOpen");
                if (postOpenMethod != null)
                {
                    postOpenMethod.Invoke(dialog, null);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[OneKey] 调用 PostOpen 失败: {ex}");
                return;
            }

            // 获取 transferables 字段
            List<TransferableOneWay> allTransferables = null;

            try
            {
                allTransferables = Traverse.Create(dialog)
                    .Field("transferables")
                    .GetValue<List<TransferableOneWay>>();
            }
            catch (Exception ex)
            {
                Log.Error($"[OneKey] 无法获取 transferables 字段: {ex}");
            }

            if (allTransferables == null || allTransferables.Count == 0)
            {
                Log.Error("[OneKey] transferables 列表为空或获取失败");
                dialog.Close(false);
                return;
            }

            // Log.Message($"[OneKey] 成功获取 {allTransferables.Count} 个可传输物品");

            List<LoadoutClipboard.SavedItem> savedLoadout = LoadoutClipboard.GetLoadout(map, groupID);
            string sourceDesc = LoadoutClipboard.GetLoadoutSourceDescription(map, groupID);

            bool anyLoadedPlaceholder = true;
            if (!anyLoadedPlaceholder)
            {
                dialog.Close(false);
                return;
            }
            dialog.Close(false);
            LoadFromItems(clickedTransporter, savedLoadout, sourceDesc);
        }

        /// <summary>
        /// 获取用户当前选中的指定组的运输舱
        /// </summary>
        private static List<CompTransporter> GetSelectedTransporters(Map map, int groupID)
        {
            List<CompTransporter> result = new List<CompTransporter>();

            try
            {
                var selectedObjects = Find.Selector.SelectedObjects;

                if (selectedObjects != null && selectedObjects.Count > 0)
                {
                    foreach (object obj in selectedObjects)
                    {
                        Thing thing = obj as Thing;
                        if (thing != null)
                        {
                            CompTransporter comp = thing.TryGetComp<CompTransporter>();
                            // 只获取同组的运输舱
                            if (comp != null && comp.Map == map && comp.groupID == groupID)
                            {
                                result.Add(comp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[OneKey] 获取选中对象失败: {ex}");
            }

            return result;
        }
    }

    // ================== Harmony 补丁：Hook TryAccept 提取清单 ==================
    [HarmonyPatch]
    public static class Patch_TryAccept_SaveLoadout
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Dialog_LoadTransporters), "TryAccept");
        }

        static void Prefix(Dialog_LoadTransporters __instance)
        {
            // 获取 transferables 字段
            List<TransferableOneWay> allTransferables = null;

            try
            {
                allTransferables = Traverse.Create(__instance)
                    .Field("transferables")
                    .GetValue<List<TransferableOneWay>>();
            }
            catch (Exception ex)
            {
                Log.Warning($"[OneKey] Patch_TryAccept 获取 transferables 失败: {ex}");
                return;
            }

            if (allTransferables == null || allTransferables.Count == 0)
            {
                return;
            }

            // 获取运输舱组
            List<CompTransporter> transporters = null;
            try
            {
                transporters = Traverse.Create(__instance)
                    .Field("transporters")
                    .GetValue<List<CompTransporter>>();
            }
            catch (Exception ex)
            {
                Log.Warning($"[OneKey] Patch_TryAccept 获取 transporters 失败: {ex}");
                return;
            }

            if (transporters == null || transporters.Count == 0)
            {
                return;
            }

            // 获取地图
            Map map = null;
            try
            {
                map = Traverse.Create(__instance)
                    .Field("map")
                    .GetValue<Map>();
            }
            catch (Exception ex)
            {
                Log.Warning($"[OneKey] Patch_TryAccept 获取 map 失败: {ex}");
                return;
            }

            if (map == null)
            {
                return;
            }

            // 使用第一个运输舱的 groupID
            int groupID = transporters[0].groupID;

            // 保存数据
            List<LoadoutClipboard.SavedItem> items = new List<LoadoutClipboard.SavedItem>();

            foreach (var tr in allTransferables)
            {
                if (tr.CountToTransfer > 0)
                {
                    items.Add(new LoadoutClipboard.SavedItem
                    {
                        def = tr.ThingDef,
                        count = tr.CountToTransfer
                    });
                }
            }

            LoadoutClipboard.SaveLoadout(map, groupID, items);
        }
    }

    // ================== Harmony 补丁：添加按钮 ==================
    [HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.CompGetGizmosExtra))]
    public static class Patch_Gizmo
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, CompTransporter __instance)
        {
            foreach (Gizmo g in values)
                yield return g;

            if (__instance.LoadingInProgressOrReadyToLaunch)
                yield break;

            Command_Action cmd = new Command_Action
            {
                defaultLabel = "一键装载清单",
                defaultDesc = BuildDescription(__instance),
                icon = OneKeyIcons.Main,
                action = () => SmartLoadUtility.LoadPrevious(__instance)
            };

            Map map = __instance.Map;
            int groupID = __instance.groupID;

            if (!LoadoutClipboard.HasData(map, groupID))
            {
                cmd.Disable("暂无可用的装载方案，请先手动装载一次。");
            }

            yield return cmd;
            
            Command_Action dropdown = new Command_Action
            {
                defaultLabel = "预设清单▼",
                defaultDesc = "管理与应用预设",
                icon = OneKeyIcons.Dropdown,
                action = () =>
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    
                    opts.Add(new FloatMenuOption("保存为预设...", () =>
                    {
                        var current = LoadoutClipboard.GetLoadout(map, groupID);
                        if (current == null || current.Count == 0)
                        {
                            Messages.Message("暂无可保存的方案。", MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        string initial = BuildDefaultPresetName(__instance, current);
                        var dlg = new Dialog_PresetName(initial, (string name) =>
                        {
                            PresetClipboard.SavePreset(name, current);
                            // Messages.Message($"已保存为预设：{name}", MessageTypeDefOf.PositiveEvent, false);
                        });
                        Find.WindowStack.Add(dlg);
                    }));
                    
                    var header = new FloatMenuOption("⭐ 方案", null);
                    header.Disabled = true;
                    opts.Add(header);
                    
                    foreach (var name in PresetClipboard.GetPresetNames())
                    {
                        string presetName = name;
                        var applyOpt = new FloatMenuOption($"应用：{presetName}", () =>
                        {
                            var items = PresetClipboard.GetPreset(presetName);
                            if (items == null || items.Count == 0)
                            {
                                Messages.Message("预设为空或不存在。", MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            SmartLoadUtility.LoadFromItems(__instance, items, $"预设「{presetName}」");
                        });
                        applyOpt.tooltip = BuildPresetTooltip(PresetClipboard.GetPreset(presetName));
                        opts.Add(applyOpt);
                        var delOpt = new FloatMenuOption($"删除：{presetName}", () =>
                        {
                            PresetClipboard.DeletePreset(presetName);
                            // Messages.Message($"已删除预设：{presetName}", MessageTypeDefOf.NeutralEvent, false);
                        });
                        delOpt.tooltip = "删除此预设";
                        opts.Add(delOpt);
                    }
                    
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            };
            
            yield return dropdown;
        }

        static string BuildDescription(CompTransporter transporter)
        {
            Map map = transporter.Map;
            int groupID = transporter.groupID;

            if (!LoadoutClipboard.HasData(map, groupID))
                return "暂无可用的装载方案";

            string text = "";

            // 检测当前选中的同组运输舱数量
            int selectedCount = 0;
            try
            {
                var selectedObjects = Find.Selector.SelectedObjects;
                if (selectedObjects != null)
                {
                    foreach (object obj in selectedObjects)
                    {
                        Thing thing = obj as Thing;
                        if (thing != null)
                        {
                            var comp = thing.TryGetComp<CompTransporter>();
                            if (comp != null && comp.groupID == groupID)
                            {
                                selectedCount++;
                            }
                        }
                    }
                }
            }
            catch { }

            // 显示提示信息
            if (selectedCount > 1)
            {
                text = $"[当前选中 {selectedCount} 个运输舱，将装载到所有选中的舱]\n\n";
            }
            else
            {
                text = "[仅装载到当前运输舱]\n提示：Shift+点击可选择多个运输舱\n\n";
            }

            // 显示方案来源
            string sourceDesc = LoadoutClipboard.GetLoadoutSourceDescription(map, groupID);
            text += $"方案来源：{sourceDesc}\n\n";

            text += "将自动填充以下物品：\n";

            List<LoadoutClipboard.SavedItem> savedLoadout = LoadoutClipboard.GetLoadout(map, groupID);
            int shown = 0;

            foreach (var item in savedLoadout)
            {
                if (shown++ >= 6)
                {
                    text += $"...还有 {savedLoadout.Count - 6} 项";
                    break;
                }
                text += $"- {item.def.label} x{item.count}\n";
            }

            return text.TrimEnd('\n');
        }
        
        static string BuildDefaultPresetName(CompTransporter transporter, List<LoadoutClipboard.SavedItem> items)
        {
            Map map = transporter.Map;
            int groupID = transporter.groupID;
            int selectedCount = 0;
            try
            {
                var selectedObjects = Find.Selector.SelectedObjects;
                if (selectedObjects != null)
                {
                    foreach (object obj in selectedObjects)
                    {
                        Thing thing = obj as Thing;
                        if (thing != null)
                        {
                            var comp = thing.TryGetComp<CompTransporter>();
                            if (comp != null && comp.groupID == groupID && comp.Map == map)
                            {
                                selectedCount++;
                            }
                        }
                    }
                }
            }
            catch { }
            string typeLabel = GetTransportTypeLabel(transporter);
            string prefix = selectedCount > 1 ? $"[{typeLabel}×{selectedCount}]" : $"[{typeLabel}]";
            var top = items.Take(3).Select(i => $"{i.def.label}x{i.count}");
            string summary = string.Join(" ", top);
            if (string.IsNullOrEmpty(summary)) summary = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            return $"{prefix}:{summary}";
        }
        
        static string GetTransportTypeLabel(CompTransporter transporter)
        {
            try
            {
                if (transporter?.parent != null)
                {
                    var shuttle = transporter.parent.TryGetComp<CompShuttle>();
                    if (shuttle != null) return "穿梭机";
                }
            }
            catch { }
            return "运输舱";
        }
        
        static string BuildPresetTooltip(List<LoadoutClipboard.SavedItem> items)
        {
            if (items == null || items.Count == 0) return "空预设";
            int shown = 0;
            string tip = "物品清单：\n";
            foreach (var it in items)
            {
                if (shown++ >= 10)
                {
                    tip += $"...还有 {items.Count - 10} 项";
                    break;
                }
                tip += $"- {it.def.label} x{it.count}\n";
            }
            return tip.TrimEnd('\n');
        }
    }

    public class Dialog_PresetName : Window
    {
        private string buffer;
        private Action<string> onAccept;
        public override Vector2 InitialSize => new Vector2(420f, 160f);
        public Dialog_PresetName(string initial, Action<string> onAccept)
        {
            this.buffer = initial ?? "";
            this.onAccept = onAccept;
            closeOnAccept = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            draggable = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Widgets.Label(new Rect(0f, y, inRect.width, 30f), "输入预设名称");
            y += 36f;
            buffer = Widgets.TextField(new Rect(0f, y, inRect.width, 30f), buffer);
            y += 50f;
            float btnWidth = 120f;
            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth*2 - 10f, y, btnWidth, 32f), "取消"))
            {
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width - btnWidth - 5f, y, btnWidth, 32f), "确定"))
            {
                string name = buffer.Trim();
                if (!name.NullOrEmpty())
                {
                    onAccept?.Invoke(name);
                }
                Close();
            }
        }
    }
}
