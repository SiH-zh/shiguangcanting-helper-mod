using System;
using System.Collections.Generic;
using DailyAssistant;

// 在隔离的引擎替身上编译真实BodyTypography.cs，验证选择和材质政策；不等同于实机渲染。
namespace UnityEngine
{
    public enum HideFlags { None, DontSave }
    public class Object
    {
        public string name;
        public bool Destroyed;
        public static void Destroy(Object value) { value.Destroyed = true; LastDestroyed = value; }
        public static Object LastDestroyed;
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha) { r = red; g = green; b = blue; a = alpha; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
    }
    public sealed class Material : Object
    {
        public HideFlags hideFlags;
        public int RatioUpdates;
        public Dictionary<string, object> Values = new Dictionary<string, object>();
        public HashSet<string> Keywords = new HashSet<string>();
        public Material() { }
        public Material(Material original) { Values = new Dictionary<string, object>(original.Values); Keywords = new HashSet<string>(original.Keywords); }
        public bool HasProperty(string property) { return Values.ContainsKey(property); }
        public void SetFloat(string property, float value) { Values[property] = value; }
        public void SetColor(string property, Color value) { Values[property] = value; }
        public void DisableKeyword(string keyword) { Keywords.Remove(keyword); }
    }
}
namespace TMPro
{
    public enum FontStyles { Normal = 0, Bold = 1 }
    public enum FontWeight { Regular = 400, Bold = 700 }
    public sealed class TMP_FontAsset : UnityEngine.Object { public UnityEngine.Material material; }
    public sealed class TMP_Text
    {
        public TMP_FontAsset font;
        public UnityEngine.Material fontSharedMaterial;
        public FontStyles fontStyle;
        public FontWeight fontWeight;
        public bool enableVertexGradient;
        public UnityEngine.Color color;
    }
    public static class ShaderUtilities
    {
        public static bool Fail;
        public static void UpdateShaderRatios(UnityEngine.Material material)
        { if (Fail) throw new InvalidOperationException("模拟引擎异常"); material.RatioUpdates++; }
    }
}
internal static class TypographyTests
{
    private static int count;
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception("验证失败：" + name); count++; }
    private static TMPro.TMP_FontAsset Font(string name)
    { return new TMPro.TMP_FontAsset { name = name, material = new UnityEngine.Material() }; }
    public static int Main()
    {
        try
        {
            var body = Font("alibaba SDF");
            var dynamicBody = Font("alibaba Dynamic SDF");
            var title = Font("zhanku-happy SDF");
            var latin = Font("Oswald Bold SDF");
            var candidates = new[] { body, dynamicBody, title, latin };
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++)
            for (int c = 0; c < 4; c++) for (int d = 0; d < 4; d++)
                if (a != b && a != c && a != d && b != c && b != d && c != d)
                    Check(Object.ReferenceEquals(BodyTypography.SelectFont(new[] { candidates[a], candidates[b], candidates[c], candidates[d] }), body), "24种资源顺序均选择固定正文");
            Check(Object.ReferenceEquals(BodyTypography.SelectFont(new[] { title, dynamicBody, latin }), dynamicBody), "仅使用已知中文动态后备");
            body.material = null;
            Check(Object.ReferenceEquals(BodyTypography.SelectFont(new[] { body, title, dynamicBody }), dynamicBody), "跳过未就绪正文资产");
            bool rejected = false;
            try { BodyTypography.SelectFont(new[] { title, latin, (TMPro.TMP_FontAsset)null }); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "无正文时拒绝标题或西文字体");
            body.material = new UnityEngine.Material();
            var original = body.material;
            string[] effects = { "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_WeightNormal", "_UnderlayDilate", "_UnderlaySoftness", "_UnderlayOffsetX", "_UnderlayOffsetY", "_GlowInner", "_GlowOuter", "_GlowOffset", "_Bevel", "_BumpFace", "_BumpOutline" };
            foreach (string p in effects) original.Values[p] = .95f;
            original.Values["_FaceColor"] = UnityEngine.Color.clear;
            foreach (string p in new[] { "_OutlineColor", "_UnderlayColor", "_GlowColor" }) original.Values[p] = UnityEngine.Color.white;
            foreach (string p in new[] { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "GLOW_ON", "BEVEL_ON" }) original.Keywords.Add(p);
            var atlas = new object();
            original.Values["_MainTex"] = atlas;
            original.Values["_GradientScale"] = 6f;
            original.Values["_Stencil"] = 3f;
            original.Values["_WeightBold"] = .75f;
            var copy = BodyTypography.CreateMaterial(body);
            Check(!Object.ReferenceEquals(copy, original), "生成独立材质");
            Check(Object.ReferenceEquals(body.material, original), "不替换游戏字体基础材质");
            foreach (string p in effects)
            {
                Check((float)copy.Values[p] == 0f, "清除正文效果" + p);
                Check((float)original.Values[p] == .95f, "原材质效果不变" + p);
            }
            Check(((UnityEngine.Color)copy.Values["_FaceColor"]).Equals(UnityEngine.Color.white), "白色字面保留顶点颜色");
            Check(((UnityEngine.Color)original.Values["_FaceColor"]).Equals(UnityEngine.Color.clear), "不改原字面颜色");
            foreach (string p in new[] { "_OutlineColor", "_UnderlayColor", "_GlowColor" })
                Check(((UnityEngine.Color)copy.Values[p]).Equals(UnityEngine.Color.clear), "清除附加色" + p);
            Check(copy.Keywords.Count == 0 && original.Keywords.Count == 5, "只关闭独立材质效果关键词");
            Check(Object.ReferenceEquals(copy.Values["_MainTex"], atlas), "保留原字体图集");
            Check((float)copy.Values["_GradientScale"] == 6f && (float)copy.Values["_Stencil"] == 3f, "保留距离场和裁剪参数");
            Check((float)copy.Values["_WeightBold"] == .75f, "保留按钮显式粗体字重");
            Check(copy.RatioUpdates == 1 && original.RatioUpdates == 0, "仅重算独立材质比例");
            foreach (var color in new[] { new UnityEngine.Color(.35f, .24f, .15f, 1), new UnityEngine.Color(.2f, .6f, .1f, 1), new UnityEngine.Color(.7f, .1f, .1f, .6f) })
            {
                var label = new TMPro.TMP_Text { color = color, fontStyle = TMPro.FontStyles.Bold, fontWeight = TMPro.FontWeight.Bold, enableVertexGradient = true };
                BodyTypography.Apply(label, body, copy);
                Check(label.color.Equals(color), "保留正文及提示的颜色和透明度");
                Check(label.fontStyle == TMPro.FontStyles.Normal && label.fontWeight == TMPro.FontWeight.Regular && !label.enableVertexGradient, "重置为普通正文字重");
                Check(Object.ReferenceEquals(label.fontSharedMaterial, copy), "所有助手文字引用自有材质");
            }
            var minimal = BodyTypography.CreateMaterial(Font("alibaba SDF"));
            Check(minimal != null, "兼容不存在的可选材质属性");
            TMPro.ShaderUtilities.Fail = true;
            rejected = false;
            try { BodyTypography.CreateMaterial(body); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && UnityEngine.Object.LastDestroyed != null && !original.Destroyed, "初始化失败释放自有材质");
            Console.WriteLine("字体初始化隔离验证通过 " + count + " 项断言；不代表实机渲染验证。");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
