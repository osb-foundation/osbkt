# OSB Ring-3 ABI (v0)

Version: **5** (GETABI, AH=82h). This document records the syscall ABI
**exactly as implemented today** (osb.kernel `Cpu/Syscalls.cs` +
`Cpu/SyscallGfx.cs` + `Cpu/Syscalls.Identity.cs`), so userspace programs
(osb.shell, XWIN) can be written against a written contract. Planned additions
live in `osb.shell/docs/MIGRATION.md` §3 and will bump this version.

Version 2 (phase 1b) adds GETCHAR (6Eh) and replaces the provisional 1a.1
SPAWNPATH (6Ah) with the final `SpawnReq` form. Version 1's 6Ah (RDI=path,
RSI=len, RDX=args, RCX=argsLen) was **provisional** and is superseded.

### v2 -> v3 (phase 1c): 6Ah/20h are byte strings, not UTF-16 chars

Version 2 kept `AbiVersion = 2` while changing the **string convention** of the
path-taking syscalls: 6Ah SPAWNPATH (`pathPtr`/`argsPtr`) and 20h RESOLVE
(`path`) now carry **byte strings with an explicit byte length** (B1), exactly
like GETCWD (22h), GETARGS (23h) and WRITE (01h). The earlier convention passed
a UTF-16 `char*` plus a **character** count. That is an incompatible wire-format
change: a caller written for v2 that still passes a UTF-16 buffer with a
character count will be misread by a v3 kernel (and vice-versa). Version 3
therefore makes the change visible: `AbiVersion` is bumped to **3**, and a
program that checks `GETABI < 3` refuses to run against a v3 kernel. No syscall
number, register mapping, or error code changed — only the string encoding of
the path/args buffers of 6Ah and 20h. (osb.xwin was audited: it uses only
01h/40h–59h and never calls 20h/21h/6Ah or GETABI, so it is unaffected.)

### v3 -> v4 (phase 1c): session identity (65h–69h)

Version 4 adds the kernel-owned session identity syscalls **65h LOGIN**,
**66h LOGOUT**, **67h SUDO**, **68h END_SUDO**, **69h WHOAMI**. All strings are
**byte strings with explicit lengths** (validated with `UserPtr`). Every
authentication/authorization failure returns **-3 EACCES**; an unknown user and
a wrong password return the **same** code (no oracle). The kernel owns the
session (uid/gid/user) and the PASSWD/SUDOERS table; password data is **never**
returned through any syscall. POWEROFF (58h) and REBOOT (59h) are now
**root-only** (-3 otherwise). No other syscall changed.

**1c.1 refinement (no number/signature change):** 65h now rejects an empty name
or a name ≥ `NameCap` with **-3** (it never truncates), and a LOGIN while a
session is already active first performs the LOGOUT teardown (kills the session's
tasks except the leader and kernel tasks) before authenticating. 66h LOGOUT and a
re-LOGIN share the same teardown path.

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
  the caller), `-4` ENOENT (no such file), `-5` EINVAL (bad `SpawnReq`
  content), `-6` ENOMEM (no room for the child). GETCHAR (6Eh) has its **own
  return space** (see its row); its `-2` means Ctrl+C, **not** ENOSYS.
* **String convention (B1):** every ring-3 **string argument is a BYTE string**
  (ASCII/UTF-8), with an explicit byte length — the same convention as GETCWD
  (22h), GETARGS (23h) and WRITE (01h). This applies to 6Ah (`pathPtr`/`argsPtr`
  and their byte lengths) and 20h RESOLVE (`path`/`len`), and any future
  path-taking syscall. The kernel converts the bytes to its internal char*
  (UTF-16) form; callers must **not** pass a UTF-16 `char*` with a character
  count. osbkt wrappers narrow `char`→`byte` before the call (see `Narrow` in
  `Syscall.cs`), exactly like `Print`/`Write`.
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
| 20h | RESOLVE | RDI=path (bytes), RSI=len, RDX = out 16 B | 1 ok, 0 fail | out: u32 ino, u16 mode, u16 uid, u64 size. `path` is a **byte** string (B1); `len` is its byte length. `len` must be ≤260 — a larger `len` is rejected with 0 (B2: the validated length is exactly the length used, never clamped). No permission check (X7) |
| 21h | DIRNEXT | RDI=dirIno, RSI=index, RDX=out, RCX=cap (≤240) | 1 entry, 0 end/fail | out: u32 childIno, u8 type, u8 nameLen, name bytes; `.`/`..` skipped |
| 22h | GETCWD | RDI=buf, RSI=cap (≤256) | n (NUL-terminated), -1 EFAULT | |
| 23h | GETARGS | RDI=buf, RSI=cap (≤129) | n | the task's argument string. `cap` up to 129 returns up to 128 chars + NUL, so a 128-byte argument round-trips intact. |

There are no write/create/delete syscalls in v0; files are seeded into the
ext4 image at build time (kernel `build.sh`).

## Processes

| AH | Name | Args | Returns | Notes |
|---|---|---|---|---|
| 30h | GETPID | — | pid | |
| 4Ch | EXIT | AL = exit code | never returns | task becomes zombie; reaped by WAIT |
| 80h | SPAWN | AL = built-in program id | child pid, -1 fail | ids: kernel-side built-ins only (HELLO/BAD/SPAWNER); not file spawn |
| 6Ah | SPAWNPATH | RDI = `SpawnReq*` (see below) | child pid, or -1/-3/-4/-5/-6 | spawns the file named in `SpawnReq`. `stdinHandle`/`stdoutHandle` = -1 inherit the console, else an already-open handle the kernel validates **against the caller** (A5). Args capped at 128 bytes. The kernel resolves the path, checks it is a regular file and that the caller may execute it, **before** creating any task (a refusal leaves no child). Error codes (B4): any bad pointer (block/path/args) → **-1 EFAULT**; bad content (pathLen 0 or >260, argsLen >128, a handle ≠ -1) → **-5 EINVAL**; **-3** EACCES; **-4** ENOENT; **-6** ENOMEM. |
| 81h | WAIT | RSI = pid | child exit status | blocks the caller |
| 82h | GETABI | — | ABI version (= 4) | osbkt `Syscall.GetABI()`; check at startup |
| 6Eh | GETCHAR | — | byte 0..255, -1 empty, -2 Ctrl+C | **Cooked** input: merges serial + PS/2 into one byte stream. Non-blocking: `-1` when no byte is ready. `-2` is a Ctrl+C marker delivered **in arrival order** (P0.2: the ring holds byte/`-2` entries, so a line typed after Ctrl+C is a new line, not merged with the aborted one). PS/2 extended keys synthesize the same ANSI sequences the serial path delivers (Up `ESC[A`, Down `ESC[B`, Right `ESC[C`, Left `ESC[D`, Home `ESC[H`, End `ESC[F`, Delete `ESC[3~`). The cooked ring is **flushed when a new foreground program is spawned** (S2), so stale input does not leak into the next program. |
| 65h | LOGIN | RDI=name, RSI=nameLen, RDX=pass, RCX=passLen (byte strings) | 0 ok, -1 EFAULT, -3 EACCES | Kernel-owned session (1c). Verifies against PASSWD; unknown user and wrong password are the same **-3**. Only the **session leader** may LOGIN. An empty name or a name ≥ NameCap is rejected **-3** (never truncated). A LOGIN while a session is active first tears that session down (LOGOUT), then authenticates. On success sets session uid/gid/user and cwd to the user's home. Password is never returned by any syscall. |
| 66h | LOGOUT | — | 0 ok, -3 EACCES | Only the **session leader**. Kills every task in the session except the leader and kernel tasks (session 0), clears the session user. The shell then prints a fresh `login:`. |
| 67h | SUDO | — | 0 ok, -3 EACCES | Elevates the **calling task's** effective uid to 0 iff the session user is in SUDOERS. Children spawned while elevated inherit uid 0. |
| 68h | END_SUDO | — | 0 | Restores the calling task's effective uid to the session user's real uid. |
| 69h | WHOAMI | RDI=buf, RSI=cap | name length, -1 EFAULT, -3 EACCES (not logged in) | Writes the session user's name (bytes). Never password data. |
| 63h | GETENV | RDI=name, RSI=nameLen, RDX=buf, RCX=cap | value length (0 for empty), -4 ENOENT (unset), -1 EFAULT, -5 EINVAL (name > 64) | Per-task store (v5). nameLen 0 lists every `NAME=VALUE\n` (cap up to 1024). Names are case-insensitive. |
| 64h | SETENV | RDI=name, RSI=nameLen, RDX=val, RCX=valLen | 0 ok, -1 EFAULT, -3 EACCES (locked USER/HOME), -5 EINVAL, -6 ENOMEM | Per-task store (v5). val NULL + valLen 0 deletes. Names 1..64, values 0..256. |

### 6Ah `SpawnReq` — the SPAWNPATH argument block (final, v3)

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

Validation (kernel, before any load): the block itself is in the user window
(a bad block pointer → **-1 EFAULT**); `pathPtr`/`pathLen` and
`argsPtr`/`argsLen` are in range using the true **byte** length (a bad pointer
→ **-1 EFAULT**); `pathLen` in 1..260; `argsLen` in 0..128; a handle ≠ -1 is
rejected. Bad **content** (pathLen 0 or >260, argsLen >128, handle ≠ -1) →
**-5 EINVAL**. The path is then resolved and checked to be a regular file
(**-4 ENOENT** if not) and executable by the caller (**-3 EACCES**) — all
**before** a task is created. Returns the child pid, or the error above;
**-6 ENOMEM** if there is no room for the child.

**Exec check (B3/S4):** the execute bit is checked for the caller's class
(owner/group/other), never "any x bit". The caller's uid/gid come from
`Scheduler.CurrentUid()`; in phase 1b this is **provisional** (always 0 — the
console runs as root and there is no session/credential layer yet). 1c wires
the real session uid in that one helper.

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
| 58h | POWEROFF | — | never returns (ACPI S5). **root-only** (1c, I3): -3 EACCES for non-root |
| 59h | REBOOT | — | never returns (8042 pulse). **root-only** (1c, I3): -3 EACCES for non-root |

### v4 -> v5 (slice 3): per-task environment (63h/64h)

Version 5 adds **63h GETENV** and **64h SETENV**. The environment is
**kernel-owned and per-task**: every task slot has a private variable store,
and a spawn copies the parent's store (changes never propagate back). Strings
are byte strings with explicit lengths (validated with `UserPtr`). Error codes:
GETENV returns the full value length (0 for a defined-but-empty value), **-4
ENOENT** when undefined, **-1 EFAULT**, **-5 EINVAL** (name > 64). With
nameLen 0 it writes every `NAME=VALUE\\n` line (cap up to 1024) and returns
the byte count. SETENV returns 0 on success, **-1 EFAULT**, **-3 EACCES** when
the name is kernel-locked (USER/HOME), **-5 EINVAL** (bad name/oversize),
**-6 ENOMEM** (store full). A NULL value pointer with length 0 **deletes**
the variable. USER and HOME are set by the kernel at LOGIN/LOGOUT and are
read-only from every task (including elevated ones); the environment never
leaks across LOGOUT/LOGIN.

## Out of v0 (planned, see osb.shell/docs/MIGRATION.md §3)

File writes (OPEN/READ/WRITE/CLOSE/LSEEK), CHDIR/MKDIR/RMDIR/UNLINK/RENAME/
LINK, STAT/CHMOD/CHOWN,
TASKLIST/KILL/SETFG, CLEAR/SETCOLOR, DATETIME, network,
PCI, USEROP. Reserved function numbers: 24h–2Fh, 60h–62h, 6Ah–6Dh, 6Fh–7Fh.
When added, this file gains the entries and GETABI is bumped.
(6Ah SPAWNPATH, 6Eh GETCHAR and 65h–69h session identity are implemented in v4
above; the `SpawnReq` block is the final form, not the provisional 1a.1
`RDI=path` form.)

OSLANG is kernel-internal (A12) and not part of this ABI.