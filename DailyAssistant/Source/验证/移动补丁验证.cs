using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// 只读取原版方法的指令并调用补丁转换器，不启动游戏、不执行角色移动、不写原版程序集。
internal static class MovementAudit
{
    public static int Main()
    {
        try
        {
            var original = AccessTools.Method(typeof(NpcMoveController), "Update");
            var input = PatchProcessor.GetOriginalInstructions(original, (ILGenerator)null);
            var patchType = Assembly.LoadFrom("DailyAssistant.dll").GetType("DailyAssistant.MovementPatch", true);
            var transpiler = patchType.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic);
            var output = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { input })).ToList();
            if (output.Count != input.Count + 3) throw new Exception("替换数量不符合预期");
            var scaleCalls = output.Where(x => x.opcode == OpCodes.Call && x.operand is MethodInfo
                && ((MethodInfo)x.operand).DeclaringType == patchType && ((MethodInfo)x.operand).Name == "Scale").ToList();
            if (scaleCalls.Count != 1) throw new Exception("本帧倍率入口并非恰好一处");
            if (output.Count(x => x.opcode == OpCodes.Stfld) != input.Count(x => x.opcode == OpCodes.Stfld))
                throw new Exception("新增了原版字段写入");
            for (int i = 0; i < input.Count; i++)
                if (!output.Any(x => ReferenceEquals(x, input[i]))) throw new Exception("原版指令被移除");
            bool rejected = false;
            try { ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { new List<CodeInstruction>() })).ToList(); }
            catch { rejected = true; }
            if (!rejected) throw new Exception("无法识别的移动结构未被拒绝");
            Console.WriteLine("移动补丁验证通过：只增加1处倍率计算，保留全部原版指令和字段写入，拒绝不兼容结构。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
