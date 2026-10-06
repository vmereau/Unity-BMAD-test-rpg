using System.Collections.Generic;

namespace Game.Editor.QuestExplorer
{
    public enum StepOpKind { Remove, MoveUp, MoveDown }

    public struct StepOp
    {
        public StepOpKind Kind;
        public int Index;

        public StepOp(StepOpKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }
    }

    /// <summary>
    /// Old step index → new step index (null = removed) for a structural step edit. Used to keep
    /// step-state <c>QuestFact</c>s (<c>_questState = stepIndex + 3</c>) pointing at the same step.
    /// </summary>
    public static class QuestStepRemap
    {
        public static Dictionary<int, int?> Compute(int stepCount, StepOp op)
        {
            var map = new Dictionary<int, int?>();
            for (int i = 0; i < stepCount; i++) map[i] = i;
            if (op.Index < 0 || op.Index >= stepCount) return map;

            switch (op.Kind)
            {
                case StepOpKind.Remove:
                    map[op.Index] = null;
                    for (int i = op.Index + 1; i < stepCount; i++) map[i] = i - 1;
                    break;
                case StepOpKind.MoveUp:
                    if (op.Index == 0) break;
                    map[op.Index] = op.Index - 1;
                    map[op.Index - 1] = op.Index;
                    break;
                case StepOpKind.MoveDown:
                    if (op.Index == stepCount - 1) break;
                    map[op.Index] = op.Index + 1;
                    map[op.Index + 1] = op.Index;
                    break;
            }
            return map;
        }
    }
}
