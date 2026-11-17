using System;
using System.Runtime.InteropServices;

namespace OsbKT
{
    public static unsafe class Syscall
    {
        // Syscall numbers (AH)
        private const int WriteNum       = 0x01; // write(buf in RDI, len in RSI)
        private const int PrintStringNum = 0x09; // legacy '$'-terminated print (not used by osbkt)
        private const int GetKeyScancodeNum = 0x52; // From SyscallGfx.KEY
        private const int GetPidNum      = 0x30;
        private const int ExitNum        = 0x4C; // AL = exit code
        private const int SpawnNum       = 0x80; // AL = built-in program id
        private const int WaitNum        = 0x81; // RSI = pid
        private const int MemInfoNum     = 0x10;
        private const int GetAbiNum      = 0x82; // kernel ABI version (osbkt/docs/ABI.md)
        private const int TicksNum       = 0x54; // PIT tick count (SyscallGfx)
        private const int SleepNum       = 0x55; // sleep(ticks) (SyscallGfx)

        // Register-based gate (osbkt/crt0.S): RAX = AH<<8 | AL, then
        // RDI, RSI, RDX, RCX arguments; result in RAX.
        [System.Runtime.InteropServices.DllImport("osbnative")]
        private static extern ulong osb_syscall(ulong rax, ulong arg1, ulong arg2, ulong arg3, ulong arg4);

        private static ulong Invoke(int ah, ulong al = 0,
                                    ulong arg1 = 0, // RDI
                                    ulong arg2 = 0, // RSI
                                    ulong arg3 = 0, // RDX
                                    ulong arg4 = 0) // RCX
        {
            return osb_syscall(((ulong)ah << 8) | al, arg1, arg2, arg3, arg4);
        }

        // ---------- Filesystem helpers (existing syscalls) ----------

        public static int GetCwd(byte* buf, int cap)
        {
            return (int)Invoke(0x22, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        public static int GetArgs(byte* buf, int cap)
        {
            return (int)Invoke(0x23, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        public static bool ResolvePath(string path, out uint ino, out ushort mode, out ushort uid, out ulong size)
        {
            ino = 0; mode = 0; uid = 0; size = 0;
            if (path == null) return false;
            fixed (char* p = path)
            {
                byte* outBuf = stackalloc byte[16];
                if (Invoke(0x20, arg1: (ulong)p, arg2: (ulong)path.Length, arg3: (ulong)outBuf) != 1)
                    return false;
                ino = (uint)(outBuf[0] | (outBuf[1] << 8) | (outBuf[2] << 16) | (outBuf[3] << 24));
                mode = (ushort)(outBuf[4] | (outBuf[5] << 8));
                uid = (ushort)(outBuf[6] | (outBuf[7] << 8));
                size = 0;
                for (int k = 0; k < 8; k++) size |= ((ulong)outBuf[8 + k]) << (k * 8);
                return true;
            }
        }

        public static bool DirNext(uint dirIno, int index, byte* outBuf, int cap)
        {
            return Invoke(0x21, arg1: dirIno, arg2: (ulong)index, arg3: (ulong)outBuf, arg4: (ulong)cap) == 1;
        }

        // ---------- PUBLIC API ----------

        // Text output via AH=09h PRINT ('$'-terminated).
        private static void WriteStringAsBytes(string s, byte* dst)
        {
            fixed (char* p = s)
            {
                int i = 0;
                while (i < s.Length && i < 255) { dst[i] = (byte)p[i]; i++; }
                dst[i] = (byte)'$';
            }
        }

        public static void PrintString(string s)
        {
            var buf = stackalloc byte[256];
            WriteStringAsBytes(s, buf);
            Invoke(PrintStringNum, arg2: (ulong)buf);
        }

        public static int GetKeyScancode()
        {
            return (int)Invoke(GetKeyScancodeNum);
        }

        public static uint GetPid()
        {
            return (uint)Invoke(GetPidNum);
        }

        public static void Exit(int exitCode)
        {
            // AH = 4Ch, AL = exitCode (F1: was passing the code in RSI)
            Invoke(ExitNum, al: (ulong)(byte)exitCode);
            // Should not return.
        }

        public static int Spawn(byte programId)
        {
            // AH = 80h, AL = program id (F1: was passing the id in RSI)
            return (int)Invoke(SpawnNum, al: programId);
        }

        public static int Wait(int pid)
        {
            return (int)Invoke(WaitNum, arg2: (ulong)pid);
        }

        public static void MemInfo(out ulong totalKB, out ulong usedKB, out ulong freeKB)
        {
            // Kernel expects RDI pointing to buffer of 3 ulong (total, used, free)
            var buffer = stackalloc ulong[3];
            Invoke(MemInfoNum, arg1: (ulong)buffer);
            totalKB = buffer[0];
            usedKB  = buffer[1];
            freeKB  = buffer[2];
        }

        // ----- ABI version check (AH=82h) -----
        // The ABI version this osbkt is written against (docs/ABI.md). Call
        // GetABI() once at startup and refuse to run on a mismatch.
        public const ushort AbiVersion = 1;

        public static ushort GetABI()
        {
            return (ushort)Invoke(GetAbiNum);
        }

        // ----- time (SyscallGfx 54h/55h) -----

        public static ulong Ticks()
        {
            return Invoke(TicksNum);
        }

        public static void Sleep(ulong ticks)
        {
            Invoke(SleepNum, arg1: ticks);
        }

        // ----- single char -----

        public static void PutChar(char c)
        {
            char b = c;
            Invoke(WriteNum, arg1: (ulong)(&b), arg2: 1UL);
        }
    }
}