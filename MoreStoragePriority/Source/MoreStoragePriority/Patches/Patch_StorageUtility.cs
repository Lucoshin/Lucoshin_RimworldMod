using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace MoreStoragePriority.Patches
{
    /// <summary>
    /// Hook StoreUtility.TryFindBestBetterStorageFor
    /// 这是小人寻找最佳存储位置的核心方法
    /// </summary>
    [HarmonyPatch(typeof(StoreUtility))]
    [HarmonyPatch(nameof(StoreUtility.TryFindBestBetterStorageFor))]
    public static class Patch_StoreUtility_TryFindBestBetterStorageFor
    {
        /// <summary>
        /// Postfix: 在找到最佳存储位置后，根据类别权重重新评估
        /// </summary>
        static void Postfix(
            Thing t,
            Pawn carrier,
            Map map,
            StoragePriority currentPriority,
            Faction faction,
            ref IntVec3 foundCell,
            ref IHaulDestination haulDestination,
            bool needAccurateResult)
        {
            try
            {
                if (!MoreStoragePriority.MSP_SessionSettings.Enabled)
                    return;
                // 如果没找到目标，或者物品无效，直接返回
                if (haulDestination == null || t?.def == null)
                    return;

                // 获取找到的存储设置
                StorageSettings foundSettings = GetStorageSettings(haulDestination);
                if (foundSettings == null)
                    return;

                // 检查是否有扩展设置
                ExtendedStorageSettings extendedSettings = StorageSettingsManager.GetExtendedSettings(foundSettings);
                if (extendedSettings == null || !extendedSettings.HasAnyCustomPriority())
                    return;

                // 重新评估：查找考虑类别权重后的最佳存储位置
                SlotGroup bestGroup;
                IHaulDestination betterDest = FindBestStorageWithMoreStoragePriority(
                    t, carrier, map, currentPriority, faction, needAccurateResult, out bestGroup
                );

                if (betterDest != null && betterDest != haulDestination)
                {
                    haulDestination = betterDest;

                    IntVec3 carrierPos = carrier?.Position ?? t.Position;
                    IntVec3 newCell = FindBestCellInGroup(bestGroup, t, carrier, faction, map, carrierPos);
                    if (newCell.IsValid)
                        foundCell = newCell;

                    if (MoreStoragePriorityMod.settings.enableDebugLog)
                    {
                        Log.Message($"[MoreStoragePriority] {t.Label} 重新选择存储位置（考虑类别权重）");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreStoragePriority] TryFindBestBetterStorageFor_Postfix 出错: {ex}");
            }
        }

        /// <summary>
        /// 查找考虑类别权重后的最佳存储位置
        /// </summary>
        private static IHaulDestination FindBestStorageWithMoreStoragePriority(
            Thing t,
            Pawn carrier,
            Map map,
            StoragePriority currentPriority,
            Faction faction,
            bool needAccurateResult,
            out SlotGroup bestGroup)
        {
            IHaulDestination bestDest = null;
            int bestPriority = int.MinValue;
            float bestDistSquared = float.MaxValue;
            bestGroup = null;

            IntVec3 carrierPos = carrier?.Position ?? t.Position;

            // 遍历所有可能的存储位置
            List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;

            foreach (SlotGroup group in allGroups)
            {
                if (!IsValidStorageFor(group, t, carrier, faction))
                    continue;

                StorageSettings settings = GetStorageSettings(group?.parent);
                if (settings == null)
                    continue;

                // 获取实际优先级（基础优先级 + 类别权重）
                int effectivePriority = GetEffectivePriority(settings, t);

                // 如果优先级不够高，跳过
                if (effectivePriority < ExtendedStorageSettings.ToListPriority(currentPriority))
                    continue;

                // 计算距离
                IntVec3 destPos = GetDestinationPosition(group);
                float distSquared = (carrierPos - destPos).LengthHorizontalSquared;

                // 比较优先级和距离
                bool isBetter = false;

                if (effectivePriority > bestPriority)
                {
                    isBetter = true;
                }
                else if (effectivePriority == bestPriority && distSquared < bestDistSquared)
                {
                    isBetter = true;
                }

                if (isBetter)
                {
                    bestDest = group?.parent as IHaulDestination;
                    bestPriority = effectivePriority;
                    bestDistSquared = distSquared;
                    bestGroup = group;
                }
            }

            return bestDest;
        }

        /// <summary>
        /// 检查存储位置是否有效
        /// </summary>
        private static bool IsValidStorageFor(SlotGroup group, Thing t, Pawn carrier, Faction faction)
        {
            if (group == null) return false;

            StorageSettings settings = GetStorageSettings(group.parent);
            if (settings == null) return false;

            // 检查物品是否被允许
            if (!settings.AllowedToAccept(t)) return false;

            // 检查是否有空间
            if (group == null) return false;

            // 其他检查...
            return true;
        }

        /// <summary>
        /// 获取实际优先级
        /// </summary>
        private static int GetEffectivePriority(StorageSettings settings, Thing t)
        {
            ExtendedStorageSettings extended = StorageSettingsManager.GetExtendedSettings(settings);

            if (extended != null && extended.HasAnyCustomPriority())
            {
                return extended.GetEffectivePriorityForThing(t);
            }
            else
            {
                return ExtendedStorageSettings.ToListPriority(settings.Priority);
            }
        }

        /// <summary>
        /// 获取存储设置
        /// </summary>
        private static StorageSettings GetStorageSettings(IHaulDestination dest)
        {
            if (dest is IStoreSettingsParent storeParent)
            {
                return storeParent.GetStoreSettings();
            }
            else if (dest is Zone_Stockpile stockpile)
            {
                return stockpile.settings;
            }
            return null;
        }

        /// <summary>
        /// 获取目标位置
        /// </summary>
        private static IntVec3 GetDestinationPosition(SlotGroup group)
        {
            if (group?.parent is Thing thing)
            {
                return thing.Position;
            }
            else if (group?.parent is Zone zone)
            {
                return zone.Cells.FirstOrDefault();
            }
            return IntVec3.Invalid;
        }

        private static IntVec3 FindBestCellInGroup(SlotGroup group, Thing t, Pawn carrier, Faction faction, Map map, IntVec3 carrierPos)
        {
            if (group == null) return IntVec3.Invalid;
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            var cells = group.CellsList;
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 c = cells[i];
                if (StoreUtility.IsGoodStoreCell(c, map, t, carrier, faction))
                {
                    float d = (carrierPos - c).LengthHorizontalSquared;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
            }
            return best;
        }
    }

 
}
