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

- **Console I/O**: `PrintString`, `PutChar`, `GetKeyScancode`
- **Process Management**: `Exit`, `Spawn`, `Wait`, `GetPid`, `GetABI`
- **Memory**: `MemInfo`
- **Filesystem**: `ResolvePath`, `DirNext` (read-only surface)
- **Environment**: `GetCwd`, `GetArgs`

Plus ring-3 infrastructure: `crt0.S` (startup + INT 21h shim + mem
helpers), `user.ld` (link at 0x2000000), `mkosbx.py` (OSBX packaging).
The full ABI contract is `docs/ABI.md`; it is the source of truth — update
it in the same commit as any syscall change.

## Sibling checkouts

Consumers (osb.shell, osb.xwin, osb.kernel) are expected side by side with
this repo; they consume osbkt by **linked sources** (`Syscall.cs`) plus the
canonical MiniCoreLib from `osb.core/src/MiniCoreLib.cs`. There is no
osbkt.dll at runtime; each consumer is a single freestanding NativeAOT
module.

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

- `docs/ABI.md`: the syscall ABI contract (source of truth).
- `README.md`: High-level project/user documentation.