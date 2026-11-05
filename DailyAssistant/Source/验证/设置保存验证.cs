using System;
using DailyAssistant;
using Newtonsoft.Json;

internal static class PreferenceTests
{
    private static int count;
    private static void Check(bool ok, string text)
    {
        if (!ok) throw new Exception("验证失败：" + text);
        count++;
    }
    public static int Main()
    {
        try
        {
            var initial = new AssistantSettings();
            Check(!initial.QuizHints && !initial.AutoHamster && !initial.SpeedEnabled, "初装全部关闭");
            initial.QuizHints = true; initial.AutoHamster = true; initial.SpeedEnabled = true;
            initial.WaiterSpeedUnits = 23; initial.CustomerSpeedUnits = 87;
            var restored = JsonConvert.DeserializeObject<AssistantSettings>(JsonConvert.SerializeObject(initial));
            SpeedValue.NormalizePreferences(restored);
            Check(restored.QuizHints, "答题开关保存并恢复");
            Check(restored.AutoHamster, "自动地鼠开关保存并恢复");
            Check(restored.SpeedEnabled, "速度开关保存并恢复");
            Check(restored.WaiterSpeedUnits == 23 && restored.CustomerSpeedUnits == 87, "两类倍率保存并恢复");
            restored.QuizHints = restored.AutoHamster = restored.SpeedEnabled = false;
            restored = JsonConvert.DeserializeObject<AssistantSettings>(JsonConvert.SerializeObject(restored));
            Check(!restored.QuizHints && !restored.AutoHamster && !restored.SpeedEnabled, "用户关闭后仍保存为关闭");
            var legacy = JsonConvert.DeserializeObject<AssistantSettings>("{\"QuizHints\":true,\"Schema\":1}");
            SpeedValue.NormalizePreferences(legacy);
            Check(legacy.QuizHints && !legacy.AutoHamster && !legacy.SpeedEnabled
                && legacy.WaiterSpeedUnits == 10 && legacy.CustomerSpeedUnits == 10, "兼容旧设置，已有开关保留而新功能默认关闭");
            Console.WriteLine("设置保存验证通过 " + count + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
