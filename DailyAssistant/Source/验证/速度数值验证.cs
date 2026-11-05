using System;
using DailyAssistant;

internal static class SpeedTests
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
            int units;
            for (int i = 10; i <= 100; i++)
            {
                string text = SpeedValue.Format(i);
                Check(SpeedValue.TryParse(text, out units) && units == i, "滑块全部91档与输入值一致：" + text);
            }
            foreach (string invalid in new[] { "", " ", "0", "0.9", "10.1", "11", "-1.0", "+2.0", "2.35", "1.01",
                "1.000000000000000000000000000001", "1e1", "NaN", "Infinity", "2,5", "2.0倍", "2.0x", "abc", "1.", ".5", "01.0", "1\n2" })
                Check(!SpeedValue.TryParse(invalid, out units), "拒绝无效数值：" + invalid);
            Check(SpeedValue.TryParse(" 2.3 ", out units) && units == 23, "允许外部空格并规范化");
            Check(SpeedValue.TryParse("2", out units) && units == 20, "整数按2.0处理");
            Check(SpeedValue.TryParse("2.30", out units) && units == 23, "等值尾零不改变0.1步长");
            Check(SpeedValue.TryParse("10.0", out units) && units == 100, "接受最大值10.0");
            Check(SpeedValue.TryParse("1.0", out units) && units == 10, "接受最小值1.0");
            Check(SpeedValue.Format(10) == "1.0" && SpeedValue.Format(100) == "10.0", "界面固定显示一位小数");
            Check(Math.Abs(SpeedValue.Multiply(1.5f, 23) - 3.45f) < .0001f, "按原版速度乘2.3倍率");
            Check(SpeedValue.Multiply(1.5f, 10) == 1.5f, "1.0倍率等于原版");
            Check(SpeedValue.Multiply(1.5f, 100) == 15f, "10.0倍率准确");
            Check(SpeedValue.Multiply(1.5f, 0) == 1.5f && SpeedValue.Multiply(1.5f, 101) == 1.5f, "损坏数据不放大移动速度");
            var settings = new AssistantSettings { QuizHints = true, AutoHamster = true, SpeedEnabled = true, WaiterSpeedUnits = 23, CustomerSpeedUnits = 87 };
            SpeedValue.NormalizePreferences(settings);
            Check(settings.QuizHints && settings.AutoHamster && settings.SpeedEnabled, "保留以前开启的功能状态");
            Check(settings.WaiterSpeedUnits == 23 && settings.CustomerSpeedUnits == 87, "关闭状态仍可记住两类倍率偏好");
            settings.WaiterSpeedUnits = 0; settings.CustomerSpeedUnits = 101;
            SpeedValue.NormalizePreferences(settings);
            Check(settings.WaiterSpeedUnits == 10 && settings.CustomerSpeedUnits == 10, "无效偏好恢复为1.0倍率");
            var initial = new AssistantSettings();
            SpeedValue.NormalizePreferences(initial);
            Check(!initial.QuizHints && !initial.AutoHamster && !initial.SpeedEnabled, "只有初次安装时全部默认关闭");
            Console.WriteLine("速度数值验证通过 " + count + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
