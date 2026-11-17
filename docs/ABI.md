# OSB Ring-3 ABI (v0)

Version: **1** (GETABI, AH=82h). This document records the syscall ABI
**exactly as implemented today** (osb.kernel `Cpu/Syscalls.cs` +
`Cpu/SyscallGfx.cs`), so userspace programs (osb.shell, XWIN) can be written
against a written contract. Planned additions live in
`osb.shell/docs/MIGRATION.md` §3 and will bump this version.

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
* **Pointer rule (target, A7):** every pointer argument must satisfy
  `ptr >= UserBase && ptr + len <= UserLimit`. v0 checks only the start
  address for most calls (kernel debt X9); new syscalls must implement the
  full check, and v0 callers must not rely on out-of-range len being safe.
* User window: `UserBase = 0x2000000`, `UserLimit = 0x2200000`
  (kernel `Memory/Layout.cs`).

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
| 6Ah | SPAWNPATH | RDI=path, RSI=pathLen, RDX=args, RCX=argsLen | child pid, -1 fail | spawns the file at `path` (DOS path, e.g. `A:\OSB\BIN\X.EXE`); `args` may be null with argsLen 0. v0 minimal form (1a.1): stdin/stdout are the console, no redirection handles; the full handle-based form is phase 1b. -1 = not found / bad image / no slot |
| 81h | WAIT | RSI = pid | child exit status | blocks the caller |
| 82h | GETABI | — | ABI version (= 1) | osbkt `Syscall.GetABI()`; check at startup |

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
TASKLIST/KILL/SETFG, GETCHAR (cooked), CLEAR/SETCOLOR, DATETIME, network,
PCI, USEROP. Reserved function numbers: 24h–2Fh, 60h–69h, 6Bh–6Fh, 70h–79h.
When added, this file gains the entries and GETABI is bumped. (6Ah SPAWNPATH is
already implemented in the 1a.1 minimal form above; the handle-based form will
be phase 1b.)

OSLANG is kernel-internal (A12) and not part of this ABI.