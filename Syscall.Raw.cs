using System;

namespace OsbKT
{
    // Test-only escape hatch: register-level syscall invoke for test programs
    // (TPtr) that must exercise the gate with arbitrary AH/AL and arguments.
    // NOT part of the normal API surface — production code (the shell, XWIN,
    // utilities) must use the typed wrappers in Syscall.cs. Kept in a separate
    // partial so the omission is visible and so the shell cannot reach it by
    // accident.
    public static unsafe partial class Syscall
    {
        public static int Raw(int ah, int al, ulong arg1, ulong arg2, ulong arg3, ulong arg4)
        {
            return (int)Invoke(ah, (ulong)al, arg1, arg2, arg3, arg4);
        }
    }
}