using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// 在隔离程序中运行原游戏的结算方法；界面、存盘、账户和每日刷新等宿主副作用已被替换。
// 本文件不编入模组，不在真实存档或游戏进程中运行。
internal static class NativeRulesTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("失败：" + message);
        assertions++; Console.WriteLine("通过：" + message);
    }
    private static void Set(Type type, string name, object value)
    { type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value); }

    public static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        try
        {
            string settings = args[0];
            GameRule.nHamsterDailyQuota = 20;
            GameRule.nHamsterMaskRewardSuccessCount = 11;
            GameRule.nHamsterMaskFullBonusTeeth = 5;
            GameRule.nHamsterMaxConsecutive = 2;
            var defs = new List<HamsterConfig.TypeDef>();
            foreach (string line in File.ReadAllLines(Path.Combine(settings,"Hamster.tab")).Skip(2))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] c = line.Split('\t');
                var d = (HamsterConfig.TypeDef)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HamsterConfig.TypeDef));
                typeof(HamsterConfig.TypeDef).GetField("eType").SetValue(d,(EHamsterType)int.Parse(c[0]));
                typeof(HamsterConfig.TypeDef).GetField("nClicksToKill").SetValue(d,int.Parse(c[2]));
                typeof(HamsterConfig.TypeDef).GetField("nBaseBoardTeeth").SetValue(d,long.Parse(c[3]));
                typeof(HamsterConfig.TypeDef).GetField("nBagCount").SetValue(d,int.Parse(c[4]));
                defs.Add(d);
            }
            Set(typeof(HamsterConfig),"m_TypeDefs",defs);
            Set(typeof(HamsterConfig),"m_nAppearanceBagSize",defs.Sum(x=>x.nBagCount));
            Set(typeof(HamsterConfig),"m_bLoaded",true);
            Player.m_szPlayerId="隔离验证";
            var save = new HamsterSaveData();
            save.nContractRevision=6; save.szSequenceDate="20261105";
            save.daySequence=new List<int>();
            for(int i=0;i<20;i++) save.daySequence.Add(i%5);
            Set(typeof(HamsterManager),"m_saveData",save);
            Set(typeof(HamsterManager),"IsolatedTestMask",7);
            long total=0; int masks=0;
            for(int i=0;i<20;i++)
            {
                EHamsterType type;
                Check(HamsterManager.TryPeekNextAppearanceType(out type),"原版读取第"+(i+1)+"只类型");
                int before=save.nAppearanceIndex;
                Check(HamsterManager.CommitAppearance(type)&&save.nAppearanceIndex==before+1,"出现顺序只提交一次");
                var r=HamsterManager.SettleSuccess(type);
                total+=defs[(int)type].nBaseBoardTeeth;
                if(r.nUnlockedMaskId>=0)masks++;
                Check(r.bSuccess&&save.nBoardTeeth==total&&save.nCurrentSuccessIndex==i+1,"原版按类型结算板牙及每日进度");
                if(i==10)Check(r.nUnlockedMaskId==7&&save.bDailyMaskRewardGranted,"原版第11次触发面具");
            }
            Check(masks==1,"面具每日只触发一次");
            long old=save.nBoardTeeth;
            Check(!HamsterManager.SettleSuccess((EHamsterType)0).bSuccess&&old==save.nBoardTeeth,"原版额度满后拒绝追加奖励");
            save.nCurrentSuccessIndex=10;save.bDailyMaskRewardGranted=false;
            Set(typeof(HamsterManager),"IsolatedTestMask",-1);
            var bonus=HamsterManager.SettleSuccess((EHamsterType)0);
            Check(bonus.bSuccess&&bonus.bFullCollection&&bonus.nBoardTeethGained==defs[0].nBaseBoardTeeth+5,"原版面具集齐追加5板牙");

            var questions=new List<QuizConstData>();
            foreach(string line in File.ReadAllLines(Path.Combine(settings,"Quiz.tab")).Skip(2))
            {
                if(string.IsNullOrWhiteSpace(line))continue;
                string[] c=line.Split('\t');if(c[1]!="1")continue;
                questions.Add(new QuizConstData{nQuizID=ContentId.FromOfficial(int.Parse(c[0])),bEnable=true,
                    szQuestion=c[2],szOptionA=c[3],szOptionB=c[4],szOptionC=c[5],szOptionD=c[6],nCorrectIndex=int.Parse(c[7])});
            }
            Set(typeof(QuizManager),"m_enabledList",questions);
            Set(typeof(QuizManager),"m_quizDict",questions.ToDictionary(x=>x.nQuizID));
            GameRule.nQuizCountPerDay=5;GameRule.nQuizRewardGold=200;GameRule.nQuizRewardExp=50;
            TimeHelper.SetTimeProvider(delegate {return new DateTime(2026,11,5,12,0,0);});
            Set(typeof(QuizManager),"m_saveData",new QuizSaveData());
            var today=QuizManager.GetTodayQuizList();
            Check(today.Count==5,"原版从252题中生成今日5题");
            Player.m_nGold=0;Player.m_nExp=0;
            for(int i=0;i<3;i++)QuizManager.SubmitAnswer(today[i],today[i].nCorrectIndex);
            var remaining=QuizManager.GetTodayQuizList();
            Check(remaining.Count==2&&remaining[0].nQuizID==today[3].nQuizID,"原版待答列表只返回剩余2题");
            foreach(var q in remaining)Check(QuizManager.SubmitAnswer(q,q.nCorrectIndex),"原版接受剩余题正确答案");
            Check(QuizManager.GetTodayQuizList().Count==0&&QuizManager.GetTodayCorrectCount()==5,"原版进度正确推进至5题答完");
            Check(Player.m_nGold==1000&&Player.m_nExp==250,"5道原版奖励共1000金币及250经验");
            TimeHelper.SetTimeProvider(delegate {return new DateTime(2026,11,6,0,0,0);});
            Check(QuizManager.GetTodayQuizList().Count==5&&QuizManager.GetTodayAnsweredCount()==0,"原版跨日刷新待答列表与次数");
            Console.WriteLine("原版规则隔离验证共通过 "+assertions+" 项断言。");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
