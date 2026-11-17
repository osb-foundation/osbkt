# OSB Toolkit (osbkt)

A static library that wraps the OSB kernel's INT 21h syscalls in a clean C# API for userspace programs.

## Overview

`osbkt` provides a high-level C# API for userspace programs running on the OSB kernel. It wraps the low-level INT 21h syscall interface into convenient methods for console I/O, file operations, process management, and more.

## What's Included

- **Syscall.cs** - INT 21h syscall wrappers
  - Console: `PrintString`, `PutChar`, `GetChar`
  - Process: `Exit`, `Spawn`, `Wait`, `GetPid`
  - Memory: `MemInfo`
  - Filesystem: `CreateFile`, `OpenFile`, `ReadFile`, `WriteFile`, `DeleteFile`, `CreateDir`, `RemoveDir`, `Rename`, `ResolvePath`, `DirNext`
  - Environment: `GetCwd`, `GetArgs`

## Usage

Userspace programs link against `osbkt` to access OSB kernel services. The library enforces freestanding constraints:
- No BCL convenience
- No GC allocations
- No exceptions for normal control flow

## Building

```bash
dotnet build
```

## Part of OSB

This is part of the [OSB](https://github.com/osb-foundation) - Operating System Basic project, a DOS-like operating system built from scratch.

## License

MIT License - see [LICENSE](LICENSE) for details.