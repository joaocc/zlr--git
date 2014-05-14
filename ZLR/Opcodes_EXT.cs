using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ZLR.VM
{
    partial class Opcode
    {
#pragma warning disable 0169
        [Opcode(OpCount.Zero, 181, false, true, false, MaxVersion = 3)]
        [Opcode(OpCount.Zero, 181, true, MinVersion = 4, MaxVersion = 4)]
        [Opcode(OpCount.Ext, 0, true, MinVersion = 5)]
        private void op_save(ILGenerator il)
        {
            MethodInfo impl;

            switch (zm.ZVersion)
            {
                case 1:
                case 2:
                case 3:
                    // branching version
                    impl = typeof (ZMachine).GetMethod("SaveQuetzal",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    il.Emit(OpCodes.Ldarg_0);
                    // pass the address of this instruction's branch offset, which is always the 2nd instruction byte because this is 0OP
                    il.Emit(OpCodes.Ldc_I4, this.PC + 1);
                    il.Emit(OpCodes.Call, impl);
                    Branch(il, OpCodes.Brtrue, OpCodes.Brfalse);
                    break;

                default:
                    // storing version
                    // in V4, argc is always 0 since this is a 0OP instruction
                    if (argc == 0)
                    {
                        impl = typeof (ZMachine).GetMethod("SaveQuetzal",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        il.Emit(OpCodes.Ldarg_0);
                        // pass the address of this instruction's result storage, which is always the last instruction byte
                        il.Emit(OpCodes.Ldc_I4, this.PC + this.ZCodeLength - 1);
                        il.Emit(OpCodes.Call, impl);
                        StoreResult(il);
                    }
                    else
                    {
                        impl = typeof (ZMachine).GetMethod("SaveAuxiliary",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        il.Emit(OpCodes.Ldarg_0);
                        LoadOperand(il, 0);
                        LoadOperand(il, 1);
                        LoadOperand(il, 2);
                        il.Emit(OpCodes.Call, impl);
                        StoreResult(il);
                    }
                    break;
            }
        }

        [Opcode(OpCount.Zero, 182, false, true, false, Terminates = true, MaxVersion = 3)]
        [Opcode(OpCount.Zero, 182, true, Terminates = true, MinVersion = 4, MaxVersion = 4)]
        [Opcode(OpCount.Ext, 1, true, Terminates = true, MinVersion = 5)]
        private void op_restore(ILGenerator il)
        {
            MethodInfo impl;

            switch (zm.ZVersion)
            {
                case 1:
                case 2:
                case 3:
                    // branching version
                    impl = typeof(ZMachine).GetMethod("RestoreQuetzal",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    int failurePC = PC + ZCodeLength;
                    if (!branchIfTrue)
                        failurePC += branchOffset - 2;

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, failurePC);
                    il.Emit(OpCodes.Call, impl);
                    il.Emit(OpCodes.Pop);
                    compiling = false;
                    break;

                default:
                    // storing version
                    // in V4, argc is always 0 since this is a 0OP instruction
                    if (argc == 0)
                    {
                        impl = typeof(ZMachine).GetMethod("RestoreQuetzal",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4, PC + ZCodeLength);
                        il.Emit(OpCodes.Call, impl);
                        StoreResult(il);

                        compiling = false;
                    }
                    else
                    {
                        impl = typeof(ZMachine).GetMethod("RestoreAuxiliary",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        il.Emit(OpCodes.Ldarg_0);
                        LoadOperand(il, 0);
                        LoadOperand(il, 1);
                        LoadOperand(il, 2);
                        il.Emit(OpCodes.Call, impl);
                        StoreResult(il);
                    }
                    break;
            }
        }

        [Opcode(OpCount.Ext, 2, true, MinVersion = 5)]
        private void op_log_shift(ILGenerator il)
        {
            if (operandTypes[1] == OperandType.Variable)
            {
                MethodInfo impl = typeof(ZMachine).GetMethod("LogShiftImpl", BindingFlags.NonPublic | BindingFlags.Static);

                LoadOperand(il, 0);
                LoadOperand(il, 1);
                il.Emit(OpCodes.Call, impl);
            }
            else if (operandValues[1] < 0)
            {
                // shift right
                int value = -operandValues[1];
                LoadOperand(il, 0);
                il.Emit(OpCodes.Conv_U2);
                il.Emit(OpCodes.Ldc_I4, value);
                il.Emit(OpCodes.Shr_Un);
            }
            else
            {
                // shift left
                LoadOperand(il, 0);
                il.Emit(OpCodes.Ldc_I4, (int)operandValues[1]);
                il.Emit(OpCodes.Shl);
            }
            StoreResult(il);
        }

        [Opcode(OpCount.Ext, 3, true)]
        private void op_art_shift(ILGenerator il)
        {
            if (operandTypes[1] == OperandType.Variable)
            {
                MethodInfo impl = typeof(ZMachine).GetMethod("ArtShiftImpl", BindingFlags.NonPublic | BindingFlags.Static);

                LoadOperand(il, 0);
                LoadOperand(il, 1);
                il.Emit(OpCodes.Call, impl);
            }
            else if (operandValues[1] < 0)
            {
                // shift right
                int value = -operandValues[1];
                LoadOperand(il, 0);
                il.Emit(OpCodes.Conv_I2);
                il.Emit(OpCodes.Ldc_I4, value);
                il.Emit(OpCodes.Shr);
            }
            else
            {
                // shift left
                LoadOperand(il, 0);
                il.Emit(OpCodes.Ldc_I4, (int)operandValues[1]);
                il.Emit(OpCodes.Shl);
            }
            StoreResult(il);
        }

        [Opcode(OpCount.Ext, 4, true, MinVersion = 5)]
        private void op_set_font(ILGenerator il)
        {
            FieldInfo ioFI = typeof(ZMachine).GetField("io", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo setFontMI = typeof(IZMachineIO).GetMethod("SetFont");

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, ioFI);
            LoadOperand(il, 0);
            il.Emit(OpCodes.Call, setFontMI);
            StoreResult(il);
        }

        // EXT:5 to EXT:8 are only in V6

        [Opcode(OpCount.Ext, 9, true, MinVersion = 5)]
        private void op_save_undo(ILGenerator il)
        {
            MethodInfo impl = typeof(ZMachine).GetMethod("SaveUndo", BindingFlags.NonPublic | BindingFlags.Instance);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, resultStorage);
            il.Emit(OpCodes.Ldc_I4, this.PC + this.ZCodeLength);
            il.Emit(OpCodes.Call, impl);
        }

        [Opcode(OpCount.Ext, 10, true, Terminates = true, MinVersion = 5)]
        private void op_restore_undo(ILGenerator il)
        {
            MethodInfo impl = typeof(ZMachine).GetMethod("RestoreUndo", BindingFlags.NonPublic | BindingFlags.Instance);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, resultStorage);
            il.Emit(OpCodes.Ldc_I4, PC + ZCodeLength);
            il.Emit(OpCodes.Call, impl);

            compiling = false;
        }

        [Opcode(OpCount.Ext, 11, MinVersion = 5)]
        private void op_print_unicode(ILGenerator il)
        {
            MethodInfo impl = typeof(ZMachine).GetMethod("PrintUnicode", BindingFlags.NonPublic | BindingFlags.Instance);

            il.Emit(OpCodes.Ldarg_0);
            LoadOperand(il, 0);
            il.Emit(OpCodes.Call, impl);
        }

        [Opcode(OpCount.Ext, 12, true, MinVersion = 5)]
        private void op_check_unicode(ILGenerator il)
        {
            FieldInfo ioFI = typeof(ZMachine).GetField("io", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo checkUnicodeMI = typeof(IZMachineIO).GetMethod("CheckUnicode");

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, ioFI);
            LoadOperand(il, 0);
            il.Emit(OpCodes.Call, checkUnicodeMI);
            StoreResult(il);
        }
#pragma warning restore 0169
    }
}
