#nullable enable

using HarmonyLib;
using System;
using System.Reflection.Emit;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Creates the smallest correct local-variable instruction for the installed
    /// Harmony version and reads the local identity from supported instruction
    /// encodings. ONI's bundled Harmony predates these convenience operations.
    /// </summary>
    internal static class HarmonyCodeInstructionFactory
    {
        internal static CodeInstruction LoadLocal(
            int localIndex,
            bool loadAddress = false)
        {
            if (localIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localIndex));
            }

            if (loadAddress)
            {
                return localIndex < 256
                    ? new CodeInstruction(
                        OpCodes.Ldloca_S,
                        Convert.ToByte(localIndex))
                    : new CodeInstruction(OpCodes.Ldloca, localIndex);
            }

            switch (localIndex)
            {
                case 0:
                    return new CodeInstruction(OpCodes.Ldloc_0);
                case 1:
                    return new CodeInstruction(OpCodes.Ldloc_1);
                case 2:
                    return new CodeInstruction(OpCodes.Ldloc_2);
                case 3:
                    return new CodeInstruction(OpCodes.Ldloc_3);
                default:
                    return localIndex < 256
                        ? new CodeInstruction(
                            OpCodes.Ldloc_S,
                            Convert.ToByte(localIndex))
                        : new CodeInstruction(OpCodes.Ldloc, localIndex);
            }
        }

        internal static CodeInstruction StoreLocal(int localIndex)
        {
            if (localIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localIndex));
            }

            switch (localIndex)
            {
                case 0:
                    return new CodeInstruction(OpCodes.Stloc_0);
                case 1:
                    return new CodeInstruction(OpCodes.Stloc_1);
                case 2:
                    return new CodeInstruction(OpCodes.Stloc_2);
                case 3:
                    return new CodeInstruction(OpCodes.Stloc_3);
                default:
                    return localIndex < 256
                        ? new CodeInstruction(
                            OpCodes.Stloc_S,
                            Convert.ToByte(localIndex))
                        : new CodeInstruction(OpCodes.Stloc, localIndex);
            }
        }

        internal static int LocalIndex(this CodeInstruction instruction)
        {
            if (instruction == null)
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            if (instruction.opcode == OpCodes.Ldloc_0 ||
                instruction.opcode == OpCodes.Stloc_0)
            {
                return 0;
            }

            if (instruction.opcode == OpCodes.Ldloc_1 ||
                instruction.opcode == OpCodes.Stloc_1)
            {
                return 1;
            }

            if (instruction.opcode == OpCodes.Ldloc_2 ||
                instruction.opcode == OpCodes.Stloc_2)
            {
                return 2;
            }

            if (instruction.opcode == OpCodes.Ldloc_3 ||
                instruction.opcode == OpCodes.Stloc_3)
            {
                return 3;
            }

            if (instruction.opcode == OpCodes.Ldloc_S ||
                instruction.opcode == OpCodes.Ldloc ||
                instruction.opcode == OpCodes.Stloc_S ||
                instruction.opcode == OpCodes.Stloc ||
                instruction.opcode == OpCodes.Ldloca_S ||
                instruction.opcode == OpCodes.Ldloca)
            {
                return ConvertLocalOperandToIndex(instruction.operand);
            }

            throw new ArgumentException(
                "The Harmony instruction is not a supported local-variable load, " +
                "store, or address load.",
                nameof(instruction));
        }

        private static int ConvertLocalOperandToIndex(object? operand)
        {
            if (operand is LocalBuilder localBuilder)
            {
                return localBuilder.LocalIndex;
            }

            if (operand == null)
            {
                throw new ArgumentException(
                    "A local-variable instruction requires an operand.",
                    nameof(operand));
            }

            return Convert.ToInt32(operand);
        }
    }
}
