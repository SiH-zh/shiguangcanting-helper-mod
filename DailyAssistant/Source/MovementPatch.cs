using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace DailyAssistant
{
    [HarmonyPatch(typeof(NpcMoveController), "Update")]
    internal static class MovementPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo speed = AccessTools.Field(typeof(NpcMoveController), "m_fMoveSpeed");
            FieldInfo npc = AccessTools.Field(typeof(NpcMoveController), "m_Npc");
            MethodInfo scale = AccessTools.Method(typeof(MovementPatch), "Scale");
            if (speed == null || npc == null || scale == null)
                throw new InvalidOperationException("移动速度接口已变化，请更新日常助手。");
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, speed))
                {
                    // 只替换本帧使用的局部速度值，保留原版存储的基础速度。
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, npc);
                    yield return new CodeInstruction(OpCodes.Call, scale);
                    replaced++;
                }
            }
            if (replaced != 1) throw new InvalidOperationException("移动计算结构与1.2不兼容，未启用速度补丁。");
        }

        private static float Scale(float original, Npc npc)
        {
            var owner = ModBehaviour.Current;
            if (owner == null || !owner.SpeedEnabled) return original;
            try { return owner.ScaleMovement(original, npc); }
            catch (Exception ex) { owner.DisableSpeedAfterError(ex); return original; }
        }
    }
}
