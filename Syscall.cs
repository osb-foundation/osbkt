using System;
using System.Runtime.InteropServices;

namespace OsbKT
{
    public static unsafe partial class Syscall
    {
        // Syscall numbers (AH)
        private const int WriteNum       = 0x01; // write(buf in RDI, len in RSI)
        private const int PrintStringNum = 0x09; // legacy '$'-terminated print (not used by osbkt)
        private const int GetKeyScancodeNum = 0x52; // From SyscallGfx.KEY
        private const int GetPidNum      = 0x30;
        private const int ExitNum        = 0x4C; // AL = exit code
        private const int SpawnNum       = 0x80; // AL = built-in program id
        private const int SpawnPathNum   = 0x6A; // RDI = SpawnReq* (final v2 form)
        private const int GetCharNum     = 0x6E; // cooked console byte (serial+PS/2)
        private const int WaitNum        = 0x81; // RSI = pid
        private const int MemInfoNum     = 0x10;
        private const int GetAbiNum      = 0x82; // kernel ABI version (osbkt/docs/ABI.md)
        private const int GetEnvNum      = 0x63; // getenv(name,len,buf,cap); len 0 lists all
        private const int SetEnvNum      = 0x64; // setenv(name,len,val,valLen) -> 1/0
        private const int TicksNum       = 0x54; // PIT tick count (SyscallGfx)
        private const int SleepNum       = 0x55; // sleep(ticks) (SyscallGfx)
        private const int MemGrowNum     = 0x11; // MEMGROW (11h): grant memory in 2 MiB steps
        private const int LoginNum       = 0x65; // LOGIN(name,pass) session (1c)
        private const int LogoutNum      = 0x66; // LOGOUT()
        private const int SudoNum        = 0x67; // SUDO() elevate caller
        private const int EndSudoNum     = 0x68; // END_SUDO() restore caller
        private const int WhoAmINum      = 0x69; // WHOAMI(buf,cap) -> name
        private const int ClearNum       = 0x6F; // CLEAR (console clear)
        private const int SetColorNum    = 0x70; // SETCOLOR(fg AL, bg AH)
        private const int GetColorNum    = 0x71; // GETCOLOR(out fg, out bg)
        private const int DateTimeNum    = 0x72; // DATETIME(out 8B)
        private const int ChmodNum       = 0x73; // CHMOD(path,len,mode) -> int
        private const int ChownNum       = 0x74; // CHOWN(path,len,uid) -> int
        private const int UserOpNum      = 0x75; // USEROP(sub,...) -> int

        // Terminal API (7Ah-7Fh, K2)
        private const int ConsInfoNum    = 0x7A; // CONS_INFO: out u16[2] (cols, rows) at RDI
        private const int ConsCursorNum  = 0x7B; // CONS_CURSOR: AL=0 get,1 set,2 hide,3 show
        private const int ConsWriteAtNum = 0x7C; // CONS_WRITE_AT: RDI=row,RSI=col,RDX=buf,RCX=len
        private const int ConsKeyNum     = 0x7E; // CONS_KEY: AL=0 GETKEY,1 READKEY,2 KEYAVAILABLE
        private const int ConsFrameNum   = 0x7F; // CONS_FRAME: AL=0 BEGIN,1 END,2 FLUSH

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

        // Raw syscall invoke: test-only (TPtr). Moved to Syscall.Raw.cs so the
        // normal API surface never exposes register-level control; see that file.

        // ---------- Filesystem helpers (existing syscalls) ----------

        public static int GetCwd(byte* buf, int cap)
        {
            return (int)Invoke(0x22, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        public static int GetArgs(byte* buf, int cap)
        {
            return (int)Invoke(0x23, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        public static int ChDir(byte* path, int len)
        {
            return (int)Invoke(0x24, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int ChDir(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x24, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        public static int Open(byte* path, int len)
        {
            return (int)Invoke(0x2A, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int Open(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x2A, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        public static int Read(int handle, byte* buf, int count)
        {
            return (int)Invoke(0x2B, arg1: (ulong)handle, arg2: (ulong)buf, arg3: (ulong)count);
        }

        public static void Close(int handle)
        {
            Invoke(0x2C, arg1: (ulong)handle);
        }

        // 0x76 CREATEFILE(path, len) -> handle (create/truncate for writing)
        public static int CreateFile(byte* path, int len)
        {
            return (int)Invoke(0x76, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int CreateFile(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x76, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        // 0x77 WRITEFILE(handle, data, dataLen) -> bytes written
        public static int WriteFile(int handle, byte* data, int count)
        {
            return (int)Invoke(0x77, arg1: (ulong)handle, arg2: (ulong)data, arg3: (ulong)count);
        }

        // 0x78 SETSTDOUT(handle) -> sets current task's stdout handle (-1 = VGA)
        public static void SetStdout(int handle)
        {
            Invoke(0x78, arg1: (ulong)handle);
        }

        // 0x79 SETSTDIN(handle) -> sets current task's stdin handle (-1 = console)
        public static void SetStdin(int handle)
        {
            Invoke(0x79, arg1: (ulong)handle);
        }

        // STAT (60h): mode/uid/gid/size/mtime/links. out buffer is 16 bytes.
        public static bool Stat(byte* path, int len, byte* outBuf)
        {
            return Invoke(0x60, arg1: (ulong)path, arg2: (ulong)len, arg3: (ulong)outBuf) == 1;
        }

        public static bool Stat(string path, byte* outBuf)
        {
            if (path == null) return false;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return false;
            return Invoke(0x60, arg1: (ulong)pbuf, arg2: (ulong)plen, arg3: (ulong)outBuf) == 1;
        }

        public static int Mkdir(byte* path, int len)
        {
            return (int)Invoke(0x25, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int Mkdir(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x25, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        public static int Rmdir(byte* path, int len)
        {
            return (int)Invoke(0x26, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int Rmdir(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x26, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        public static int Delete(byte* path, int len)
        {
            return (int)Invoke(0x27, arg1: (ulong)path, arg2: (ulong)len);
        }

        public static int Delete(string path)
        {
            if (path == null) return -5;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;
            return (int)Invoke(0x27, arg1: (ulong)pbuf, arg2: (ulong)plen);
        }

        public static int Rename(byte* oldPath, int oldLen, byte* newPath, int newLen)
        {
            return (int)Invoke(0x28, arg1: (ulong)oldPath, arg2: (ulong)oldLen, arg3: (ulong)newPath, arg4: (ulong)newLen);
        }

        public static int Rename(string oldPath, string newPath)
        {
            if (oldPath == null || newPath == null) return -5;
            var opbuf = stackalloc byte[260];
            var npbuf = stackalloc byte[260];
            int oplen = Narrow(oldPath, opbuf, 260);
            int nplen = Narrow(newPath, npbuf, 260);
            if (oplen < 0 || nplen < 0) return -5;
            return (int)Invoke(0x28, arg1: (ulong)opbuf, arg2: (ulong)oplen, arg3: (ulong)npbuf, arg4: (ulong)nplen);
        }

        public static int Copy(byte* srcPath, int srcLen, byte* dstPath, int dstLen)
        {
            return (int)Invoke(0x29, arg1: (ulong)srcPath, arg2: (ulong)srcLen, arg3: (ulong)dstPath, arg4: (ulong)dstLen);
        }

        public static int Copy(string srcPath, string dstPath)
        {
            if (srcPath == null || dstPath == null) return -5;
            var spbuf = stackalloc byte[260];
            var dpbuf = stackalloc byte[260];
            int splen = Narrow(srcPath, spbuf, 260);
            int dplen = Narrow(dstPath, dpbuf, 260);
            if (splen < 0 || dplen < 0) return -5;
            return (int)Invoke(0x29, arg1: (ulong)spbuf, arg2: (ulong)splen, arg3: (ulong)dpbuf, arg4: (ulong)dplen);
        }

        public static int Ln(byte* line, int len)
        {
            return (int)Invoke(0x61, arg1: (ulong)line, arg2: (ulong)len);
        }

        public static int Ln(string targetPath, string linkPath)
        {
            if (targetPath == null || linkPath == null) return -5;
            var tpbuf = stackalloc byte[260];
            var lpbuf = stackalloc byte[260];
            int tplen = Narrow(targetPath, tpbuf, 260);
            int lplen = Narrow(linkPath, lpbuf, 260);
            if (tplen < 0 || lplen < 0) return -5;
            return (int)Invoke(0x61, arg1: (ulong)tpbuf, arg2: (ulong)tplen, arg3: (ulong)lpbuf, arg4: (ulong)lplen);
        }

        public static int Trash(byte* line, int len)
        {
            return (int)Invoke(0x62, arg1: (ulong)line, arg2: (ulong)len);
        }

        // GETENV (63h): value of `name` (case-insensitive) as bytes, NUL-
        // terminated; returns the FULL value length (0 for a defined-but-empty
        // value), -4 ENOENT when undefined, -1 EFAULT, -5 EINVAL. With
        // nameLen 0 the kernel writes every "NAME=VALUE\n" line (cap up to
        // 1024) and returns the byte count.
        public static int GetEnv(byte* name, int nameLen, byte* buf, int cap)
        {
            return (int)Invoke(GetEnvNum, arg1: (ulong)name, arg2: (ulong)nameLen,
                arg3: (ulong)buf, arg4: (ulong)cap);
        }

        public static int GetEnv(string name, byte* buf, int cap)
        {
            if (name == null) return -4;
            var nbuf = stackalloc byte[64];
            int nlen = Narrow(name, nbuf, 64);
            if (nlen <= 0) return -5;
            return GetEnv(nbuf, nlen, buf, cap);
        }

        // SETENV (64h): define/redefine a variable. Returns false when the name
        // is kernel-locked (USER/HOME), the store is full, or the arguments
        // are invalid. A null `val` with valLen 0 deletes the variable.
        public static bool SetEnv(byte* name, int nameLen, byte* val, int valLen)
        {
            return Invoke(SetEnvNum, arg1: (ulong)name, arg2: (ulong)nameLen,
                arg3: (ulong)val, arg4: (ulong)valLen) == 0;
        }

        public static bool SetEnv(string name, string value)
        {
            if (name == null) return false;
            var nbuf = stackalloc byte[64];
            int nlen = Narrow(name, nbuf, 64);
            if (nlen <= 0) return false;
            var vbuf = stackalloc byte[256];
            int vlen = Narrow(value, vbuf, 256);
            if (vlen < 0) return false;
            return SetEnv(nbuf, nlen, vbuf, vlen);
        }

        // Narrow a managed string to a byte buffer (B1: all ring-3 string
        // arguments are byte strings — ASCII/UTF-8 — like GETCWD/GETARGS/WRITE).
        // Returns the byte length, or -1 if it does not fit in `cap`. A null
        // string yields 0 (the kernel then sees an empty path/args).
        private static int Narrow(string s, byte* dst, int cap)
        {
            if (s == null) return 0;
            if (s.Length > cap) return -1;
            fixed (char* p = s)
            {
                for (int i = 0; i < s.Length; i++) dst[i] = (byte)p[i];
            }
            return s.Length;
        }

        public static bool ResolvePath(string path, out uint ino, out ushort mode, out ushort uid, out ulong size)
        {
            ino = 0; mode = 0; uid = 0; size = 0;
            if (path == null) return false;
            var pbuf = stackalloc byte[260];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return false;
            byte* outBuf = stackalloc byte[16];
            if (Invoke(0x20, arg1: (ulong)pbuf, arg2: (ulong)plen, arg3: (ulong)outBuf) != 1)
                return false;
            ino = (uint)(outBuf[0] | (outBuf[1] << 8) | (outBuf[2] << 16) | (outBuf[3] << 24));
            mode = (ushort)(outBuf[4] | (outBuf[5] << 8));
            uid = (ushort)(outBuf[6] | (outBuf[7] << 8));
            size = 0;
            for (int k = 0; k < 8; k++) size |= ((ulong)outBuf[8 + k]) << (k * 8);
            return true;
        }

        // RESOLVE (20h) from a char* path (UTF-16 narrowed to bytes on the
        // stack). For ring-3 callers that hold a char* (the shell's completion
        // and PATH search) so they never reach for the register-level Raw().
        // Returns false on any kernel error; out params are 0 on failure.
        // RESOLVE (20h) from a byte* path (the ABI string form). Lets a ring-3
        // caller that already holds a byte buffer (the shell's PATH search)
        // resolve without a narrowing copy and without Raw().
        public static bool ResolvePath(byte* path, int len, out uint ino, out ushort mode, out ushort uid, out ulong size)
        {
            ino = 0; mode = 0; uid = 0; size = 0;
            if (path == null || len <= 0) return false;
            if (len > 260) len = 260;
            byte* outBuf = stackalloc byte[16];
            if (Invoke(0x20, arg1: (ulong)path, arg2: (ulong)len, arg3: (ulong)outBuf) != 1)
                return false;
            ino = (uint)(outBuf[0] | (outBuf[1] << 8) | (outBuf[2] << 16) | (outBuf[3] << 24));
            mode = (ushort)(outBuf[4] | (outBuf[5] << 8));
            uid = (ushort)(outBuf[6] | (outBuf[7] << 8));
            size = 0;
            for (int k = 0; k < 8; k++) size |= ((ulong)outBuf[8 + k]) << (k * 8);
            return true;
        }

        public static bool ResolvePath(char* path, int len, out uint ino, out ushort mode, out ushort uid, out ulong size)
        {
            ino = 0; mode = 0; uid = 0; size = 0;
            if (path == null || len <= 0) return false;
            if (len > 260) len = 260;
            var pbuf = stackalloc byte[260];
            for (int k = 0; k < len; k++) pbuf[k] = (byte)path[k];
            byte* outBuf = stackalloc byte[16];
            if (Invoke(0x20, arg1: (ulong)pbuf, arg2: (ulong)len, arg3: (ulong)outBuf) != 1)
                return false;
            ino = (uint)(outBuf[0] | (outBuf[1] << 8) | (outBuf[2] << 16) | (outBuf[3] << 24));
            mode = (ushort)(outBuf[4] | (outBuf[5] << 8));
            uid = (ushort)(outBuf[6] | (outBuf[7] << 8));
            size = 0;
            for (int k = 0; k < 8; k++) size |= ((ulong)outBuf[8 + k]) << (k * 8);
            return true;
        }

        public static bool DirNext(uint dirIno, int index, byte* outBuf, int cap)
        {
            return Invoke(0x21, arg1: dirIno, arg2: (ulong)index, arg3: (ulong)outBuf, arg4: (ulong)cap) == 1;
        }

        // ---------- PUBLIC API ----------

        // Text output via AH=01h WRITE with explicit length (chunked to the
        // kernel's 4096-byte cap).
        public static void Write(byte* buf, int len)
        {
            int off = 0;
            while (off < len)
            {
                int n = len - off;
                if (n > 4096) n = 4096;
                Invoke(WriteNum, arg1: (ulong)(buf + off), arg2: (ulong)n);
                off += n;
            }
        }

        // C# chars are UTF-16; OSB console text is ASCII, so each char is
        // narrowed to its low byte into a stack buffer and written packed
        // (chars > 255 are truncated).
        public static void Print(char* buf, int len)
        {
            var tmp = stackalloc byte[1024];
            int off = 0;
            while (off < len)
            {
                int n = len - off;
                if (n > 1024) n = 1024;
                for (int i = 0; i < n; i++) tmp[i] = (byte)buf[off + i];
                Invoke(WriteNum, arg1: (ulong)tmp, arg2: (ulong)n);
                off += n;
            }
        }

        public static void PrintString(string s)
        {
            // A null/empty string is a no-op (some callers pass a computed
            // string that may be null); never dereference it.
            if (s == null || s.Length == 0) return;
            fixed (char* p = s)
            {
                Print(p, s.Length);
            }
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

        // SpawnReq: the 6Ah SPAWNPATH argument block (osbkt/docs/ABI.md).
        // 40 bytes, little-endian. Explicit layout so the kernel's Rd32/Rd64
        // offsets (0, 8, 16, 24, 32, 36) match exactly. stdin/stdout = -1
        // inherit the console; a non -1 handle is rejected by the v2 kernel.
        [StructLayout(LayoutKind.Explicit, Size = 40)]
        public struct SpawnReq
        {
            [FieldOffset(0)]  public ulong PathPtr;       // +0  DOS path string
            [FieldOffset(8)]  public uint PathLen;        // +8  path length (1..260)
            [FieldOffset(16)] public ulong ArgsPtr;       // +16 argument string, or 0
            [FieldOffset(24)] public uint ArgsLen;        // +24 argument length (0..128)
            [FieldOffset(32)] public int StdinHandle;     // +32 read handle for child fd 0, or -1
            [FieldOffset(36)] public int StdoutHandle;    // +36 write handle for child fd 1, or -1
        }

        // Spawn an external program by SpawnReq (AH=6Ah, final v2 form).
        // Returns the child pid, or a negative error (B4): -1 EFAULT (bad
        // pointer), -3 EACCES, -4 ENOENT, -5 EINVAL (bad content), -6 ENOMEM.
        // The kernel validates the block and every pointer it holds, resolves
        // and exec-checks the file before creating a task (S3/B3). `req` must
        // stay alive for the call. All string fields are BYTE strings (B1).
        public static int SpawnPath(SpawnReq* req)
        {
            return (int)Invoke(SpawnPathNum, arg1: (ulong)req);
        }

        // Spawn an external program by path + args, narrowing both to bytes
        // (B1). The `SpawnReq` is filled on the stack (zero GC allocations);
        // stdin/stdout inherit the console. Returns the child pid, or a
        // negative error: -1 EFAULT, -3 EACCES, -4 ENOENT, -5 EINVAL, -6 ENOMEM.
        public static int SpawnPath(string path, string args)
        {
            var pbuf = stackalloc byte[260];
            var abuf = stackalloc byte[128];
            int plen = Narrow(path, pbuf, 260);
            if (plen < 0) return -5;              // EINVAL (path > 260)
            int alen = Narrow(args, abuf, 128);
            if (alen < 0) return -5;              // EINVAL (args > 128)
            var req = new SpawnReq
            {
                PathPtr = (ulong)pbuf,
                PathLen = (uint)plen,
                ArgsPtr = alen > 0 ? (ulong)abuf : 0,
                ArgsLen = (uint)alen,
                StdinHandle = -1,
                StdoutHandle = -1
            };
            return (int)Invoke(SpawnPathNum, arg1: (ulong)(&req));
        }

        // TaskList: enumerate visible tasks. Returns count written.
        public static int TaskList(byte* buf, int cap)
        {
            return (int)Invoke(0x6B, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        // Kill: terminate a task by pid. Returns 0 ok, -3 denied, -4 not found.
        public static bool Kill(int pid)
        {
            return Invoke(0x6C, arg1: (ulong)pid) == 0;
        }

        // SetForeground: make a task the foreground job. Returns 0 ok, -3 denied, -4 not found.
        public static bool SetForeground(int pid)
        {
            return Invoke(0x6D, arg1: (ulong)pid) == 0;
        }

        // GetChar: next cooked console byte (AH=6Eh). Merges serial + PS/2.
        // Returns 0..255, -1 when empty (non-blocking), or -2 for a pending
        // Ctrl+C (once per press). -2 here is NOT ENOSYS.
        public static int GetChar()
        {
            return (int)Invoke(GetCharNum);
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

        // MEMGROW (11h): grant memory in 2 MiB steps (512 frames).
        // RDI = requested additional frames (must be multiple of 512, or 0 to query current limit).
        // Returns: 0 on success; current frames on query (RDI=0). Negative on error:
        // -1 EFAULT, -5 EINVAL (not multiple of 512 or exceeds per-process limit),
        // -6 ENOMEM (insufficient physical frames), -3 EACCES.
        public static int MemGrow(int requestedFrames)
        {
            return (int)Invoke(MemGrowNum, arg1: (ulong)requestedFrames);
        }

        // ----- ABI version check (AH=82h) -----
        // The ABI version this osbkt is written against (docs/ABI.md). Call
        // GetABI() once at startup and refuse to run on a mismatch.
        // v6 (slice 6): console (6Fh CLEAR, 70h SETCOLOR, 71h GETCOLOR,
        // 72h DATETIME); WAIT interruptible (-7 EINTR); KILL/SETFG uid-gated.
        // v5 (phase 3, slice 3): environment (63h/64h); v4 (phase 1c): session
        // identity (65h-69h); v3 made 6Ah/20h byte strings (B1).
        public const ushort AbiVersion = 8;

        public static ushort GetABI()
        {
            return (ushort)Invoke(GetAbiNum);
        }

        // ----- session identity (65h-69h, 1c) -----
        // All strings are BYTE strings with explicit lengths. Every auth/authz
        // failure is -3 (EACCES); unknown user and wrong password are the same.

        public static int Login(string name, string password)
        {
            var nbuf = stackalloc byte[32];
            int nlen = Narrow(name, nbuf, 32);
            if (nlen < 0) return -1;
            var pbuf = stackalloc byte[32];
            int plen = Narrow(password, pbuf, 32);
            if (plen < 0) return -1;
            return Login(nbuf, nlen, pbuf, plen);
        }

        // Byte-string overload (zero-allocation; the shell uses this).
        public static int Login(byte* name, int nameLen, byte* pass, int passLen)
        {
            return (int)Invoke(LoginNum, arg1: (ulong)name, arg2: (ulong)nameLen,
                arg3: (ulong)pass, arg4: (ulong)passLen);
        }

        public static int Logout() => (int)Invoke(LogoutNum);

        public static int Sudo() => (int)Invoke(SudoNum);

        public static int EndSudo() => (int)Invoke(EndSudoNum);

        public static int WhoAmI(byte* buf, int cap)
            => (int)Invoke(WhoAmINum, arg1: (ulong)buf, arg2: (ulong)cap);

        // ----- console (6Fh-72h, slice 6+) -----

        public static void Clear()
        {
            Invoke(ClearNum);
        }

        public static void SetColor(int fg, int bg)
        {
            // fg in AL, bg in AH
            Invoke(SetColorNum, al: (ulong)((byte)fg | ((byte)bg << 8)));
        }

        public static void GetColor(out int fg, out int bg)
        {
            var buf = stackalloc byte[2];
            Invoke(GetColorNum, arg1: (ulong)buf);
            fg = buf[0];
            bg = buf[1];
        }

        public static void DateTime(out int y, out int mo, out int d, out int h, out int mi, out int s)
        {
            var buf = stackalloc byte[8];
            Invoke(DateTimeNum, arg1: (ulong)buf);
            y = buf[0]; mo = buf[1]; d = buf[2]; h = buf[3]; mi = buf[4]; s = buf[5];
        }

        // ----- terminal API (7Ah-7Fh) -----

        // CONS_INFO (7Ah): RDI = out u16[2] (cols, rows). Returns true on success.
        public static bool ConsInfo(out ushort cols, out ushort rows)
        {
            var buf = stackalloc byte[4];
            if (Invoke(ConsInfoNum, arg1: (ulong)buf) != 0) { cols = 0; rows = 0; return false; }
            cols = (ushort)(buf[0] | (buf[1] << 8));
            rows = (ushort)(buf[2] | (buf[3] << 8));
            return true;
        }

        // CONS_CURSOR (7Bh): AL subfunction. row/col are 1-based.
        public static bool ConsCursorSet(int row, int col)
            => Invoke(ConsCursorNum, al: 1, arg1: (ulong)row, arg2: (ulong)col) == 0;
        public static bool ConsCursorHide() => Invoke(ConsCursorNum, al: 2) == 0;
        public static bool ConsCursorShow() => Invoke(ConsCursorNum, al: 3) == 0;
        public static bool ConsCursorGet(out int row, out int col)
        {
            var buf = stackalloc byte[4];
            if (Invoke(ConsCursorNum, al: 0, arg1: (ulong)buf) != 0) { row = 0; col = 0; return false; }
            row = buf[0] | (buf[1] << 8);
            col = buf[2] | (buf[3] << 8);
            return true;
        }

        // CONS_WRITE_AT (7Ch): RDI=row(1-based), RSI=col(1-based), RDX=buf, RCX=len.
        // Returns bytes written or negative error.
        public static int ConsWriteAt(int row, int col, byte* buf, int len)
            => (int)Invoke(ConsWriteAtNum, arg1: (ulong)row, arg2: (ulong)col, arg3: (ulong)buf, arg4: (ulong)len);

        // CONS_KEY (7Eh): AL=0 blocking GETKEY, AL=1 non-block READKEY, AL=2 KEYAVAILABLE.
        // GETKEY returns pointer to 12-byte key event in user window (keyCode,char,ctrl,alt,shift,reserved).
        public static ulong ConsKeyGet()
            => Invoke(ConsKeyNum, al: 0);
        public static ulong ConsKeyRead()
            => Invoke(ConsKeyNum, al: 1);
        public static bool ConsKeyAvailable()
            => Invoke(ConsKeyNum, al: 2) != 0;

        // CONS_FRAME (7Fh): AL=0 BEGINFRAME, AL=1 ENDFRAME, AL=2 FLUSH.
        public static void ConsBeginFrame() => Invoke(ConsFrameNum, al: 0);
        public static void ConsEndFrame() => Invoke(ConsFrameNum, al: 1);
        public static void ConsFlushFrame() => Invoke(ConsFrameNum, al: 2);

        // MODS (57h): returns bit0 = Ctrl held.
        public static bool ConsMods()
            => Invoke(0x57) != 0;


        // ----- account management (73h-75h, slice 7) -----

        public static int Chmod(byte* path, int len, ushort mode)
        {
            // mode in DX (RDX)
            return (int)Invoke(ChmodNum, arg1: (ulong)path, arg2: (ulong)len, arg3: (ulong)mode);
        }

        public static int Chown(byte* path, int len, uint uid)
        {
            // uid in DX (RDX)
            return (int)Invoke(ChownNum, arg1: (ulong)path, arg2: (ulong)len, arg3: (ulong)uid);
        }

        // USEROP subfunctions
        public static int UserOpList(byte* buf, int cap)
        {
            return (int)Invoke(UserOpNum, al: 0, arg1: (ulong)buf, arg2: (ulong)cap);
        }

        public static int UserOpAdd(byte* name, int nameLen, byte* pass, int passLen)
        {
            return (int)Invoke(UserOpNum, al: 1, arg1: (ulong)name, arg2: (ulong)nameLen,
                arg3: (ulong)pass, arg4: (ulong)passLen);
        }

        public static int UserOpChangePass(byte* name, int nameLen, byte* pass, int passLen)
        {
            return (int)Invoke(UserOpNum, al: 2, arg1: (ulong)name, arg2: (ulong)nameLen,
                arg3: (ulong)pass, arg4: (ulong)passLen);
        }

        public static int UserOpDel(byte* name, int nameLen, uint flags = 0)
        {
            return (int)Invoke(UserOpNum, al: 3, arg1: (ulong)name, arg2: (ulong)nameLen, arg3: (ulong)flags);
        }

        public static int UserOpSudo(byte* name, int nameLen)
        {
            return (int)Invoke(UserOpNum, al: 4, arg1: (ulong)name, arg2: (ulong)nameLen);
        }

        public static int UserOpUnsudo(byte* name, int nameLen)
        {
            return (int)Invoke(UserOpNum, al: 5, arg1: (ulong)name, arg2: (ulong)nameLen);
        }

        // ----- machine (privileged, root only - decision 5) -----

        public static void PowerOff()
        {
            Invoke(0x58);
        }

        public static void Reboot()
        {
            Invoke(0x59);
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