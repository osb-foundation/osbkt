# AGENTS.md — OSB Toolkit (osbkt)

This file guides coding agents working in the `osbkt` repository.

## Project Identity

**OSB Toolkit (osbkt)** is a static library that wraps the OSB kernel's INT 21h syscalls in a clean C# API for userspace programs. It provides console I/O, file operations, process management, and environment access for freestanding OSB applications.

## Code Policy

1. **Implementation Language**
   - Use **C#** for the syscall wrapper library.

2. **Freestanding Constraints**
   - No BCL convenience, no GC allocations, no exceptions for normal control flow.
   - Fixed/stack buffers where appropriate.
   - Deterministic, bounded behavior over convenience frameworks.

3. **Compatibility**
   - This library is used by userspace programs (shell, utilities, etc.).
   - Changes must maintain compatibility with the OSB kernel's INT 21h ABI.

## Architecture

The library provides wrappers for:

- **Console I/O**: `PrintString`, `PutChar`, `GetChar`
- **Process Management**: `Exit`, `Spawn`, `Wait`, `GetPid`
- **Memory**: `MemInfo`
- **Filesystem**: `CreateFile`, `OpenFile`, `ReadFile`, `WriteFile`, `DeleteFile`, `CreateDir`, `RemoveDir`, `Rename`, `ResolvePath`, `DirNext`
- **Environment**: `GetCwd`, `GetArgs`

## Build and Validation

```bash
dotnet build
```

## Change Workflow for Agents

1. Inspect relevant files first.
2. Keep edits targeted.
3. Do not break compatibility with the OSB kernel's INT 21h ABI.
4. Check file lengths before finishing (no file may exceed 400 lines).

## Non-Goals and Cautions

- Do not introduce BCL dependencies.
- Do not use exceptions for ordinary control flow.
- Do not break API compatibility with existing userspace programs.

## Documentation Anchors

- `README.md`: High-level project/user documentation.