# OSB Toolkit (osbkt)

A static library that wraps the OSB kernel's INT 21h syscalls in a clean C# API for userspace programs.

## Overview

`osbkt` provides a high-level C# API for userspace programs running on the OSB kernel. It wraps the low-level INT 21h syscall interface into convenient methods for console I/O, file operations, process management, and more.

## What's Included

- **Syscall.cs** - INT 21h syscall wrappers (the full contract is in
  [`docs/ABI.md`](docs/ABI.md)):
  - Console: `PrintString`, `PutChar`, `GetKeyScancode`
  - Process: `Exit`, `Spawn`, `Wait`, `GetPid`, `GetABI`
  - Memory: `MemInfo`
  - Filesystem: `ResolvePath`, `DirNext` (read-only surface)
  - Environment: `GetCwd`, `GetArgs`
- **crt0.S** - ring-3 startup (`_start`), the `HandleSyscall` INT 21h shim,
  and memcpy/memset/memmove for every userspace program
- **user.ld** - linker script for ring-3 images (linked at `0x2000000`)
- **mkosbx.py** - wraps a flat image in the OSBX header the kernel loader
  expects (`mkosbx.py IMAGE.bin OUT.EXE [ENTRY]`)

File create/write/delete, directories, rename, environment and cooked input
(`GetChar`) are **not implemented yet** — they arrive with the userspace
shell migration (see `osb.shell/docs/MIGRATION.md`); the ABI numbers are
reserved but calling them returns ENOSYS (-2) today.

## Usage

Userspace programs link against `osbkt` to access OSB kernel services. The library enforces freestanding constraints:
- No BCL convenience
- No GC allocations
- No exceptions for normal control flow

## Building

```bash
dotnet build
```

osbkt is consumed by **linked sources**: userspace projects (osb.shell,
osb.xwin) compile `Syscall.cs` and the canonical
`osb.core/src/MiniCoreLib.cs` directly into their own freestanding module
(there is no osbkt.dll at runtime). The sibling checkout layout is required
(repos side by side; the build fails with a clear message if a sibling is
missing).

## Part of OSB

This is part of the [OSB](https://github.com/osb-foundation) - Operating System Basic project, a DOS-like operating system built from scratch.

## License

MIT License - see [LICENSE](LICENSE) for details.