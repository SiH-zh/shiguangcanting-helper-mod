using System;
using TMPro;
using UnityEngine;

namespace DailyAssistant
{
    // 只管理本模组文字；不修改字体资产或游戏文字正在使用的共享材质。
    internal static class BodyTypography
    {
        internal static TMP_FontAsset SelectFont(TMP_FontAsset[] fonts)
        {
            // 当前游戏的普通中文正文资产；按固定名称优先级选取，不枚举文字控件。
            string[] names = { "alibaba SDF", "alibaba Dynamic SDF" };
            foreach (string name in names)
                foreach (TMP_FontAsset candidate in fonts)
                    if (candidate != null && String.Equals(candidate.name, name, StringComparison.Ordinal)
                        && candidate.material != null) return candidate;
            throw new InvalidOperationException("普通中文正文字体尚未加载，请稍后重新打开日常助手。");
        }

        internal static Material CreateMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null)
                throw new InvalidOperationException("普通中文正文字体材质尚未加载，请稍后重新打开日常助手。");

            // 从字体基础材质复制一份，不读取任意标题的fontSharedMaterial。
            var material = new Material(font.material);
            material.name = "DailyAssistant_正文独立材质";
            material.hideFlags = HideFlags.DontSave;
            try
            {
                string[] clearFloats = {
                    "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_WeightNormal",
                    "_UnderlayDilate", "_UnderlaySoftness", "_UnderlayOffsetX", "_UnderlayOffsetY",
                    "_GlowInner", "_GlowOuter", "_GlowOffset", "_Bevel", "_BumpFace", "_BumpOutline"
                };
                foreach (string property in clearFloats)
                    if (material.HasProperty(property)) material.SetFloat(property, 0f);
                // 白色字面保留文字控件自身的棕色、绿色、红色及按钮状态颜色。
                if (material.HasProperty("_FaceColor")) material.SetColor("_FaceColor", Color.white);
                string[] clearColors = { "_OutlineColor", "_UnderlayColor", "_GlowColor" };
                foreach (string property in clearColors)
                    if (material.HasProperty(property)) material.SetColor(property, Color.clear);
                string[] effects = { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "GLOW_ON", "BEVEL_ON" };
                foreach (string keyword in effects) material.DisableKeyword(keyword);
                // 保留原字体图集、距离场刻度及裁剪参数，按新的效果重新计算比例。
                ShaderUtilities.UpdateShaderRatios(material);
                return material;
            }
            catch
            {
                UnityEngine.Object.Destroy(material);
                throw;
            }
        }

        internal static void Apply(TMP_Text text, TMP_FontAsset font, Material ownedMaterial)
        {
            text.font = font;
            text.fontSharedMaterial = ownedMaterial;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.enableVertexGradient = false;
            // 不改text.color，也不改Button.colors；按钮仍可显式使用原来的Bold样式。
        }
    }
}
