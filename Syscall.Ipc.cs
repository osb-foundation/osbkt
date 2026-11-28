// OSBKT IPC wrappers (ABI v10, AH=83h..8Ah). Thin, idiomatic C# over the
// kernel's non-blocking topic IPC. Every method returns a signed long:
//   >= 0  success (handle for CreateTopic, MessageId for Publish, 0 otherwise)
//   <  0  a negative IpcStatus error code (see docs/ABI.md §IPC)
// No auto-ACK, no implicit retries, no background workers: Receive is
// non-blocking and ACK/Fail are explicit.

using System;

namespace OsbKT
{
    public struct IpcMessageView
    {
        public ulong MessageId;   // monotonic, unique per boot
        public ulong Delivery;    // delivery handle (u64) for Ack/Fail
        public int Size;          // payload bytes copied
    }

    public static unsafe partial class Syscall
    {
        // ----- IPC syscall numbers (83h-8Ah) -----
        private const int IpcCreateNum      = 0x83;
        private const int IpcDestroyNum     = 0x84;
        private const int IpcSubscribeNum   = 0x85;
        private const int IpcUnsubscribeNum = 0x86;
        private const int IpcPublishNum     = 0x87;
        private const int IpcReceiveNum     = 0x88;
        private const int IpcAckNum         = 0x89;
        private const int IpcFailNum        = 0x8A;

        // Create a topic. `system` topics are Kernel-only and fail with EACCES
        // from ring 3. Returns the topic handle (u64) or a negative error.
        public static long CreateTopic(string name, bool system = false)
        {
            var nbuf = stackalloc byte[32];
            int n = Narrow(name, nbuf, 31);
            if (n <= 0) return -5; // EINVAL
            return (long)Invoke(IpcCreateNum, (ulong)(system ? 1 : 0),
                                (ulong)nbuf, (ulong)n);
        }

        public static long DestroyTopic(ulong topic)
        {
            return (long)Invoke(IpcDestroyNum, 0, topic);
        }

        // Subscribe is idempotent per (pid, topic): a repeat returns 0.
        public static long Subscribe(ulong topic)
        {
            return (long)Invoke(IpcSubscribeNum, 0, topic);
        }

        public static long Unsubscribe(ulong topic)
        {
            return (long)Invoke(IpcUnsubscribeNum, 0, topic);
        }

        // Publish an immutable copy of `payload` to every eligible subscriber.
        // Returns the MessageId (u64) or a negative error (QueueFull => no
        // partial fan-out). A null/empty payload publishes a zero-length message.
        public static long Publish(ulong topic, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return (long)Invoke(IpcPublishNum, 0, topic, 0, 0);
            fixed (byte* p = payload)
                return (long)Invoke(IpcPublishNum, 0, topic, (ulong)p, (ulong)payload.Length);
        }

        // Non-blocking receive into `dest`. Returns 0 and fills `view` on
        // success, EAGAIN (-10) when nothing is queued, or another negative
        // error. Does NOT acknowledge; call Ack(view.Delivery) explicitly.
        public static long Receive(ulong topic, byte[] dest, out IpcMessageView view)
        {
            view = default;
            var outp = stackalloc byte[24];
            long r;
            if (dest == null)
                r = (long)Invoke(IpcReceiveNum, 0, topic, 0, 0, (ulong)outp);
            else
                fixed (byte* p = dest)
                    r = (long)Invoke(IpcReceiveNum, 0, topic, (ulong)p, (ulong)dest.Length, (ulong)outp);
            if (r == 0)
            {
                view.MessageId = Rd64(outp, 0);
                view.Delivery = Rd64(outp, 8);
                view.Size = (int)Rd32(outp, 16);
            }
            return r;
        }

        // Explicitly acknowledge an in-flight delivery (frees its capacity).
        public static long Ack(ulong delivery)
        {
            return (long)Invoke(IpcAckNum, 0, delivery);
        }

        // Record a failed attempt. transient=true retries (same Delivery ID,
        // up to the kernel MaxAttempts); false is terminal.
        public static long Fail(ulong delivery, bool transient)
        {
            return (long)Invoke(IpcFailNum, (ulong)(transient ? 1 : 0), delivery);
        }

        private static ulong Rd64(byte* p, int o)
        {
            ulong v = 0;
            for (int k = 0; k < 8; k++) v |= ((ulong)p[o + k]) << (k * 8);
            return v;
        }

        private static uint Rd32(byte* p, int o)
        {
            return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24));
        }
    }
}