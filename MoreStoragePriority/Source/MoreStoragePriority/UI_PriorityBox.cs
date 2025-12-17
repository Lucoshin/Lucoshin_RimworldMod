using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MoreStoragePriority
{
    /// <summary>
    /// 优先级框 UI 绘制工具
    /// </summary>
    public static class UI_PriorityBox
    {
        private const float BOX_WIDTH = 40f;
        private const float BOX_HEIGHT = 24f;
        private const float LEFT_SAFE_WIDTH = 28f;
        private const float RIGHT_ANCHOR_FACTOR = 0.62f;
        private const float RIGHT_SAFE_WIDTH = 28f;

        /// <summary>
        /// 绘制优先级框
        /// </summary>
        public static void DrawPriorityBox(Rect rect, ThingDef def, ExtendedStorageSettings extendedSettings)
        {
            if (def == null || extendedSettings == null) return;

            int priority = extendedSettings.GetPriority(def);

            // 计算位置（居中偏右一点）
            float anchorX = rect.x + rect.width * RIGHT_ANCHOR_FACTOR;
            float midY = rect.y + (rect.height - BOX_HEIGHT) * 0.5f;
            Rect boxRect = new Rect(anchorX - BOX_WIDTH * 0.5f, midY, BOX_WIDTH, BOX_HEIGHT);

            // 绘制文字
            DrawText(boxRect, priority);

            // 处理交互
            Rect clickRect = rect;
            clickRect.xMin += LEFT_SAFE_WIDTH; // 避开左侧原版勾选区域
            clickRect.xMax -= RIGHT_SAFE_WIDTH; // 避开右侧允许/禁用区域
            HandleInteraction(clickRect, def, priority, extendedSettings);
        }

        public static void DrawCategoryPriorityBox(Rect rect, ThingCategoryDef catDef, ExtendedStorageSettings extendedSettings)
        {
            if (catDef == null || extendedSettings == null) return;

            int priority = extendedSettings.GetCategoryPriority(catDef);

            float anchorX = rect.x + rect.width * RIGHT_ANCHOR_FACTOR;
            float midY = rect.y + (rect.height - BOX_HEIGHT) * 0.5f;
            Rect boxRect = new Rect(anchorX - BOX_WIDTH * 0.5f, midY, BOX_WIDTH, BOX_HEIGHT);
            DrawText(boxRect, priority);

            Rect clickRect = rect;
            clickRect.xMin += LEFT_SAFE_WIDTH;
            clickRect.xMax -= RIGHT_SAFE_WIDTH;
            if (!Mouse.IsOver(clickRect)) return;
            Widgets.DrawHighlight(clickRect);
            TooltipHandler.TipRegion(boxRect, extendedSettings.GetTooltipTextForCategory(catDef));

            if (Widgets.ButtonInvisible(clickRect))
            {
                Event current = Event.current;
                if (current.button == 0)
                {
                    extendedSettings.AdjustCategoryPriority(catDef, +1);
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
                else if (current.button == 1)
                {
                    extendedSettings.AdjustCategoryPriority(catDef, -1);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }
                else if (current.button == 2)
                {
                    extendedSettings.ResetCategoryPriority(catDef);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
        }

        /// <summary>
        /// 绘制背景
        /// </summary>
        private static void DrawBackground(Rect rect, int priority)
        {
            Color bgColor = GetPriorityColor(priority);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect); // 边框
        }

        /// <summary>
        /// 绘制文字
        /// </summary>
        private static void DrawText(Rect rect, int priority)
        {
            string text = GetPriorityText(priority);
            Color textColor = GetTextColor(priority);

            GUI.color = textColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        /// <summary>
        /// 处理交互
        /// </summary>
        private static void HandleInteraction(Rect rect, ThingDef def, int priority, ExtendedStorageSettings extendedSettings)
        {
            if (!Mouse.IsOver(rect)) return;

            // 高亮显示
            Widgets.DrawHighlight(rect);

            // 显示 Tooltip
            TooltipHandler.TipRegion(rect, extendedSettings.GetTooltipText(def));

            // 处理点击
            if (Widgets.ButtonInvisible(rect))
            {
                HandleClick(def, priority, extendedSettings);
            }
        }

        /// <summary>
        /// 处理点击事件
        /// </summary>
        private static void HandleClick(ThingDef def, int priority, ExtendedStorageSettings extendedSettings)
        {
            Event current = Event.current;

            if (current.button == 0) // 左键
            {
                extendedSettings.AdjustPriority(def, +1);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            else if (current.button == 1) // 右键
            {
                extendedSettings.AdjustPriority(def, -1);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
            else if (current.button == 2) // 中键
            {
                // 中键：重置
                extendedSettings.ResetPriority(def);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// 获取优先级颜色（背景色）
        /// </summary>
        private static Color GetPriorityColor(int priority)
        {
            // -3 到 +3 的渐变色
            switch (priority)
            {
                case -3: return new Color(0.8f, 0.2f, 0.2f, 0.5f); // 深红
                case -2: return new Color(0.9f, 0.4f, 0.4f, 0.5f); // 红
                case -1: return new Color(1f, 0.7f, 0.7f, 0.5f);   // 浅红
                case 0: return new Color(0.4f, 0.4f, 0.4f, 0.3f); // 灰
                case 1: return new Color(0.7f, 1f, 0.7f, 0.5f);   // 浅绿
                case 2: return new Color(0.4f, 0.9f, 0.4f, 0.5f); // 绿
                case 3: return new Color(0.2f, 0.8f, 0.2f, 0.5f); // 深绿
                default: return new Color(0.4f, 0.4f, 0.4f, 0.3f); // 默认灰色
            }
        }

        /// <summary>
        /// 获取文字颜色
        /// </summary>
        private static Color GetTextColor(int priority)
        {
            return Color.white;
        }

        /// <summary>
        /// 获取优先级文本
        /// </summary>
        private static string GetPriorityText(int priority)
        {
            if (priority == 0)
                return ""; // 默认不显示
            else if (priority > 0)
                return $"+{priority}";
            else
                return priority.ToString();
        }

        /// <summary>
        /// 计算优先级框的矩形区域
        /// </summary>
        public static Rect GetPriorityBoxRect(Rect lineRect)
        {
            return new Rect(
                lineRect.xMax - BOX_WIDTH - 4f,
                lineRect.y,
                BOX_WIDTH,
                BOX_HEIGHT
            );
        }
    }
}
