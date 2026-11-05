using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DailyAssistant
{
    // 只保存模组统计，不写游戏的奖励、次数或题库。
    [Serializable]
    public sealed class AssistantSettings
    {
        public int Schema = 1;
        public bool QuizHints;
        public bool AutoHamster;
        public bool SpeedEnabled;
        public int WaiterSpeedUnits = 10;
        public int CustomerSpeedUnits = 10;
        public List<DailyRecord> Records = new List<DailyRecord>();
    }

    public static class SpeedValue
    {
        public const int Minimum = 10;
        public const int Maximum = 100;
        private static readonly Regex Number = new Regex(@"^(?:[1-9]|10)(?:\.[0-9]0*)?$", RegexOptions.CultureInvariant);

        public static bool IsValid(int units) { return units >= Minimum && units <= Maximum; }
        public static string Format(int units) { return (units / 10m).ToString("F1", CultureInfo.InvariantCulture); }

        public static bool TryParse(string text, out int units)
        {
            units = Minimum;
            if (text == null) return false;
            text = text.Trim();
            if (text.Length > 32 || !Number.IsMatch(text)) return false;
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
                || value < 1m || value > 10m || decimal.Truncate(value * 10m) != value * 10m) return false;
            units = (int)(value * 10m);
            return IsValid(units);
        }

        public static float Multiply(float original, int units)
        {
            return IsValid(units) ? original * (units / 10f) : original;
        }

        public static void NormalizePreferences(AssistantSettings settings)
        {
            // 初装的布尔值默认关闭；已有开关状态原样保留，只修正损坏的倍率。
            if (!IsValid(settings.WaiterSpeedUnits)) settings.WaiterSpeedUnits = Minimum;
            if (!IsValid(settings.CustomerSpeedUnits)) settings.CustomerSpeedUnits = Minimum;
        }
    }

    [Serializable]
    public sealed class DailyRecord
    {
        public string SaveKey;
        public string Date;
        public int StartingCompleted;
        public int LastCompleted;
        public int RecordedCount;
        public long Teeth;
        public int FullCollectionRewards;
        public bool HistoryIncomplete;
        public List<int> Masks = new List<int>();

        public static DailyRecord Begin(string save, string date, int completed)
        {
            return new DailyRecord { SaveKey = save, Date = date,
                StartingCompleted = completed, LastCompleted = completed,
                HistoryIncomplete = completed > 0 };
        }

        // 读档回退后，旧统计已无法与当前存档对应，重新开始记录。
        public void Observe(int completed)
        {
            if (completed >= LastCompleted) return;
            StartingCompleted = completed;
            LastCompleted = completed;
            RecordedCount = 0;
            Teeth = 0;
            FullCollectionRewards = 0;
            Masks.Clear();
            HistoryIncomplete = completed > 0;
        }

        public bool Record(int completed, long teeth, int maskId, bool fullCollection)
        {
            if (completed <= LastCompleted || teeth < 0) return false;
            if (completed != LastCompleted + 1) HistoryIncomplete = true;
            LastCompleted = completed;
            RecordedCount++;
            Teeth = checked(Teeth + teeth);
            if (maskId >= 0) Masks.Add(maskId);
            if (fullCollection) FullCollectionRewards++;
            return true;
        }
    }

    public static class OperationGuard
    {
        public static bool IsCurrent(string expectedSave, string expectedDate,
            string currentSave, string currentDate, bool isOwner, bool ready)
        {
            return ready && isOwner && expectedSave == currentSave && expectedDate == currentDate;
        }

        public static bool CanSubmit(int expectedIndex, int actualIndex, int remaining)
        {
            return expectedIndex == actualIndex && remaining > 0;
        }
    }

    // 只控制模组何时发起点击，不拦截玩家手动点击，也不改变原版存活时间。
    public sealed class AppearanceDelay
    {
        public const double Seconds = 2.0;
        private object subject;
        private double appearedAt;

        public void Reset(object current, double now)
        {
            subject = current;
            appearedAt = now;
        }

        public void Observe(object current, double now)
        {
            if (!ReferenceEquals(subject, current) || now < appearedAt) Reset(current, now);
        }

        public bool CanHit(object current, double now)
        {
            return ElapsedFor(current, now) >= Seconds;
        }

        public double ElapsedFor(object current, double now)
        {
            return current != null && ReferenceEquals(subject, current) ? now - appearedAt : double.NegativeInfinity;
        }
    }
}
