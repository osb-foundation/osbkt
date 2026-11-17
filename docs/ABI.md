# OSB Ring-3 ABI (v0)

Version: **2** (GETABI, AH=82h). This document records the syscall ABI
**exactly as implemented today** (osb.kernel `Cpu/Syscalls.cs` +
`Cpu/SyscallGfx.cs`), so userspace programs (osb.shell, XWIN) can be written
against a written contract. Planned additions live in
`osb.shell/docs/MIGRATION.md` §3 and will bump this version.

Version 2 (phase 1b) adds GETCHAR (6Eh) and replaces the provisional 1a.1
SPAWNPATH (6Ah) with the final `SpawnReq` form. Version 1's 6Ah (RDI=path,
RSI=len, RDX=args, RCX=argsLen) was **provisional** and is superseded.

## Calling convention

* Gate: `INT 21h`. Function number in **AH**; arguments in the 64-bit
  registers; the result is returned in **RAX** (negative = error, see below).
* Argument registers: **RDI** (arg1), **RSI** (arg2), **RDX** (arg3),
  **RCX** (arg4). AL-argument syscalls carry their argument in **AL**
  (AH = function, AL = value): `4Ch` exit code, `80h` built-in program id.
  All other syscalls pass AL = 0.
* The syscall gate is a **DPL3** IDT gate (ring 3 may `INT 21h`; ring 0
  only). (Supersedes an earlier draft that mislabeled this an "IOPL check".)
* osbkt issues syscalls through the register-based `osb_syscall` stub
  (`osbkt/crt0.S`): `osb_syscall(rax, rdi, rsi, rdx, rcx)`. The kernel's
  internal saved-frame layout is an implementation detail and **not part of
  this ABI**.
* **Error returns:** `-1` = EFAULT (argument pointer outside the user window
  or otherwise rejected), `-2` = ENOSYS (unknown function number). These two
  meanings are frozen (A7) and must not be redefined by later ABI versions.
  SPAWNPATH (6Ah) uses its own negative codes: `-3` EACCES (not executable by
  the caller), `-4` ENOENT (no such file), `-5` EINVAL (bad `SpawnReq`),
  `-6` ENOMEM (no room for the child). GETCHAR (6Eh) has its **own return
  space** (see its row); its `-2` means Ctrl+C, **not** ENOSYS.
* **Pointer rule (A7):** every pointer argument must satisfy
  `ptr >= UserBase && ptr + len <= UserLimit`, with overflow-safe arithmetic
  (a `len` that wraps `ptr + len` past `UserLimit` is rejected). v2 (phase 1b)
  enforces this on **every** pointer-taking syscall: 01h, 09h, 10h, 20h–23h,
  and the graphics/input calls that take pointers (42h, 48h, 50h, 56h). A
  rejected pointer returns `-1` (EFAULT) and never touches the buffer.
* User window: `UserBase = 0x2000000`, `UserLimit = 0x2200000`
  (kernel `Memory/Layout.cs`).

## Boot program: `A:\OSB\CONF\BOOT` (H-B)

The kernel boots the program named on the first line of `A:\OSB\CONF\BOOT`
(owner **root**, mode **0644**), falling back to `A:\OSB\BIN\SHELL.EXE` when
the file is absent or empty. Only root can change it (it lives in `/OSB/CONF`,
root-owned). When the boot program exits, the kernel prints
`Boot program exited. Halting.` and halts.

**Default builds always boot `SHELL.EXE`** regardless of `CONF/BOOT`. The
kernel honors `CONF/BOOT` only when compiled with `BOOT_FROM_CONF` (set by
`build.sh` with `WITH_TESTS=1`), so the golden harness can boot a test
program directly. This is verified by the `tests/smoke.sh` default-image
check: a default image with a `CONF/BOOT` pointing at a non-shell program
still boots `SHELL.EXE`.

## crt0 and the entry symbol

`osbkt/crt0.S` provides `_start`, the register-based `osb_syscall` shim
(`rax, rdi, rsi, rdx, rcx` → `INT 21h`), `memcpy`/`memset`/`memmove`, and
`__security_cookie` for every ring-3 program. The NativeAOT
startup symbol is named after the consuming module, so the link step defines
the generic `osb_startup` per consumer:

```
ld.lld -T osbkt/user.ld -o prog.elf crt0.o prog.o \
    --defsym=osb_startup=OsbShell__Module___StartupCodeMain
```

`<Module>` is the ilc `--systemmodule` value. Images are linked at
`0x2000000` (`osbkt/user.ld`), converted to a flat binary, and wrapped in the
OSBX header by `osbkt/mkosbx.py` (magic "OSBX", u16 version=1, u16 header=16,
u32 entry, u32 size — matching `Scheduler.SpawnFile`).

## Console

| AH | Name | Args | Returns | Notes |
|---|---|---|---|---|
| 01h | WRITE | RDI=buf, RSI=len | RAX = bytes written (max 4096), -1 EFAULT | bytes to VGA console; serial mirrors automatically. **osbkt uses 01h for all text output (F2): `PrintString`/`PutChar`/`Print` no longer use 09h, so `$` no longer terminates text and long strings work** |
| 09h | PRINT | RSI = `$`-terminated string | 0, -1 EFAULT | scans up to 4096 bytes for `$`; kept for XWIN compatibility, unused by osbkt |

## Memory

| AH | Name | Args | Returns | Notes |
|---|---|---|---|---|
| 10h | MEMINFO | RDI = u64[3] out | 1, -1 EFAULT | totalKB, usedKB, freeKB (frames × 4096 / 1024) |

## Filesystem (read-only surface in v0)

| AH | Name | Args | Returns | Notes |
|---|---|---|---|---|
| 20h | RESOLVE | RDI=path, RSI=len, RDX = out 16 B | 1 ok, 0 fail | out: u32 ino, u16 mode, u16 uid, u64 size. No permission check (X7) |
| 21h | DIRNEXT | RDI=dirIno, RSI=index, RDX=out, RCX=cap (≤240) | 1 entry, 0 end/fail | out: u32 childIno, u8 type, u8 nameLen, name bytes; `.`/`..` skipped |
| 22h | GETCWD | RDI=buf, RSI=cap (≤256) | n (NUL-terminated), -1 EFAULT | |
| 23h | GETARGS | RDI=buf, RSI=cap (≤128) | n | the task's argument string |

There are no write/create/delete syscalls in v0; files are seeded into the
ext4 image at build time (kernel `build.sh`).

## Processes

| AH | Name | Args | Returns | Notes |
|---|---|---|---|---|
| 30h | GETPID | — | pid | |
| 4Ch | EXIT | AL = exit code | never returns | task becomes zombie; reaped by WAIT |
| 80h | SPAWN | AL = built-in program id | child pid, -1 fail | ids: kernel-side built-ins only (HELLO/BAD/SPAWNER); not file spawn |
| 6Ah | SPAWNPATH | RDI = `SpawnReq*` (see below) | child pid, or -3/-4/-5/-6 | spawns the file named in `SpawnReq`. `stdinHandle`/`stdoutHandle` = -1 inherit the console, else an already-open handle the kernel validates **against the caller** (A5). Args capped at 128 bytes. |
| 81h | WAIT | RSI = pid | child exit status | blocks the caller |
| 82h | GETABI | — | ABI version (= 2) | osbkt `Syscall.GetABI()`; check at startup |
| 6Eh | GETCHAR | — | byte 0..255, -1 empty, -2 Ctrl+C | **Cooked** input: merges serial + PS/2 into one byte stream. Non-blocking: `-1` when no byte is ready. `-2` is delivered **once per Ctrl+C press** (its own return space; NOT ENOSYS). |

### 6Ah `SpawnReq` — the SPAWNPATH argument block (final, v2)

RDI points to a caller-filled block (all pointers are user-window addresses;
all lengths are bytes and must satisfy `ptr >= UserBase && ptr + len <=
UserLimit`):

```c
typedef struct SpawnReq {   // 40 bytes, little-endian
    uint64_t pathPtr;       // +0   pointer to the DOS path string (NUL not required)
    uint32_t pathLen;       // +8   path length in bytes (1..260)
    uint64_t argsPtr;       // +16  pointer to the raw argument string, or 0
    uint32_t argsLen;       // +24  argument length in bytes (0..128)
    int32_t  stdinHandle;   // +32  read handle to dup into child fd 0, or -1 = inherit console
    int32_t  stdoutHandle;  // +36  write handle to dup into child fd 1, or -1 = inherit console
};                          // +40
```

Validation (kernel, before any load): the block itself is in the user window;
`pathPtr`/`pathLen` and `argsPtr`/`argsLen` are in range; `pathLen` in 1..260;
`argsLen` in 0..128. Returns the child pid, or `-3` EACCES (file not
executable by the caller), `-4` ENOENT, `-5` EINVAL (bad block), `-6` ENOMEM.

## Graphics + input (40h–59h, XWIN services; kernel `SyscallGfx.cs`)

Packed coordinates: one argument carries `(x, y)` or `(w, h)` as
`((lo16) << 16) | (hi16)`, sign-extended. Colors are 8-bit palette indices.

| AH | Name | Args | Returns |
|---|---|---|---|
| 40h | GFX_ENTER | — | 0, -1 if no display |
| 41h | GFX_EXIT | — | 0 (text mode, console repainted) |
| 42h | GFX_INFO | RDI = i32[2] out | 0 (width, height) |
| 43h | FILL | RDI=xy, RSI=wh, RDX=color | 0 |
| 44h | RECT | RDI=xy, RSI=wh, RDX=color | 0 |
| 45h | BEVEL | RDI=xy, RSI=wh, RDX=face\|light<<8\|dark<<16 | 0 |
| 46h | PIXEL | RDI=xy, RSI=color | 0 |
| 47h | LINE | RDI=xy0, RSI=xy1, RDX=color | 0 (Bresenham) |
| 48h | TEXT | RDI=xy, RSI=ptr, RDX=len (≤1024), RCX=fg\|bg<<8 | 0, -1 EFAULT |
| 50h | MOUSE | RDI = i32[4] out | 0 (x, y, buttons bit0/1/2, visible) |
| 51h | CURSOR | RDI=xy, RSI=show | 0 |
| 52h | KEY | — | raw PS/2 set-1 scancode, -1 empty (hardware keyboard only) |
| 53h | CLOSE_REQUEST | — | 1 if Ctrl+F4 was pressed |
| 54h | TICKS | — | PIT tick count |
| 55h | SLEEP | RDI = ticks | 0 (busy-halts the CPU until elapsed) |
| 56h | TIME | RDI = u8[3] out | 0 (hour, minute, second — RTC) |
| 57h | MODS | — | bit0 = Ctrl held |
| 58h | POWEROFF | — | never returns (ACPI S5). v0: **no privilege check** (X8) |
| 59h | REBOOT | — | never returns (8042 pulse). v0: **no privilege check** (X8) |

## Out of v0 (planned, see osb.shell/docs/MIGRATION.md §3)

File writes (OPEN/READ/WRITE/CLOSE/LSEEK), CHDIR/MKDIR/RMDIR/UNLINK/RENAME/
LINK, STAT/CHMOD/CHOWN, environment, LOGIN/LOGOUT/SUDO/WHOAMI,
TASKLIST/KILL/SETFG, CLEAR/SETCOLOR, DATETIME, network,
PCI, USEROP. Reserved function numbers: 24h–2Fh, 60h–6Dh, 6Fh–7Fh.
When added, this file gains the entries and GETABI is bumped.
(6Ah SPAWNPATH and 6Eh GETCHAR are implemented in v2 above; the `SpawnReq`
block is the final form, not the provisional 1a.1 `RDI=path` form.)

OSLANG is kernel-internal (A12) and not part of this ABI.