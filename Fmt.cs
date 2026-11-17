using System;

namespace OsbKT
{
    // Minimal unsigned decimal formatting for freestanding programs (no BCL,
    // no allocation). Shared ring-3 infrastructure, like crt0: the shell and
    // the test programs both need numbers (exit codes, sizes, pids, ticks).
    public static unsafe class Fmt
    {
        // Writes v as decimal into buf (needs 10 bytes); returns the length.
        public static int U32(char* buf, uint v)
        {
            if (v == 0)
            {
                buf[0] = '0';
                return 1;
            }
            var t = stackalloc char[10];
            int i = 0;
            while (v > 0)
            {
                t[i++] = (char)('0' + (int)(v % 10));
                v /= 10;
            }
            for (int k = 0; k < i; k++) buf[k] = t[i - 1 - k];
            return i;
        }

        // Signed variant (two's complement; int.MinValue handled via uint).
        public static int S32(char* buf, int v)
        {
            if (v < 0)
            {
                buf[0] = '-';
                return 1 + U32(buf + 1, (uint)(-(long)v));
            }
            return U32(buf, (uint)v);
        }
    }
}