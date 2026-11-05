using System;
using DailyAssistant;

internal static class ScenarioTests
{
    private static int count;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("验证失败：" + name);
        count++;
        Console.WriteLine("通过：" + name);
    }
    public static int Main()
    {
        try
        {
            var day = DailyRecord.Begin("存档甲", "20261105", 0);
            long expected = 0;
            for (int i = 1; i <= 20; i++)
            {
                long reward = i % 5 + 1;
                expected += reward;
                Check(day.Record(i, reward, i == 11 ? 7 : -1, false), "正常结算第" + i + "只");
            }
            Check(day.RecordedCount == 20 && day.Teeth == expected, "20次混合板牙按实际值累计");
            Check(day.Masks.Count == 1 && day.Masks[0] == 7, "第11次面具记录不丢失");
            Check(!day.Record(20, 999, 8, false) && day.Teeth == expected, "重复回调不重复累计");
            Check(!day.HistoryIncomplete, "从首只开始统计覆盖完整当日");
            var partial = DailyRecord.Begin("存档甲", "20261105", 8);
            partial.Record(9, 3, -1, false);
            Check(partial.HistoryIncomplete && partial.RecordedCount == 1 && partial.Teeth == 3, "启用前8次不虚构收益");
            partial.Record(11, 7, -1, true);
            Check(partial.Teeth == 10 && partial.FullCollectionRewards == 1 && partial.Masks.Count == 0, "集齐补偿包含在原版板牙中且不当作新面具");
            partial.Observe(4);
            Check(partial.Teeth == 0 && partial.RecordedCount == 0 && partial.LastCompleted == 4, "读档回退清除不再对应的旧收益");
            Check(partial.Record(5, 2, -1, false) && partial.Teeth == 2, "回退后可继续记录");
            var nextDay = DailyRecord.Begin("存档甲", "20261106", 0);
            Check(nextDay.Teeth == 0 && !nextDay.HistoryIncomplete, "跨日统计独立");
            Check(OperationGuard.IsCurrent("甲", "今天", "甲", "今天", true, true), "同存档同日允许操作");
            Check(!OperationGuard.IsCurrent("甲", "今天", "乙", "今天", true, true), "切换存档阻止继续");
            Check(!OperationGuard.IsCurrent("甲", "今天", "甲", "明天", true, true), "跨日阻止继续");
            Check(!OperationGuard.IsCurrent("甲", "今天", "甲", "今天", false, true), "拜访其他餐厅阻止继续");
            Check(!OperationGuard.IsCurrent("甲", "今天", "甲", "今天", true, false), "加载或退出阻止继续");
            Check(OperationGuard.CanSubmit(3, 3, 2), "已答3题只接续剩余题目");
            Check(!OperationGuard.CanSubmit(3, 4, 1), "进度被外部改变时不重复提交旧题");
            Check(!OperationGuard.CanSubmit(5, 5, 0), "今日答完后不再提交");
            Console.WriteLine("共通过 " + count + " 项场景断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
