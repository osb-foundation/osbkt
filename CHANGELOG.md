# Changelog

## [0.1.0] - Olorun (Generation 0)

### Added
- Initial release of osbkt under OSB generation 0 (Olorun).
- OSB ring-3 toolkit: INT 21h syscall wrappers (AH=20h resolve, AH=21h dirnext, AH=23h getargs, AH=4Ch exit), crt0.S, user.ld, mkosbx.py (OSBX packaging), ABI contract AbiVersion=8.
- Static library wrapping kernel INT 21h syscalls in a clean C# API (console PrintString/PutChar/GetKeyScancode; process Exit/Spawn/Wait/GetPid/GetABI; memory MemInfo; read-only fs ResolvePath/DirNext; environment GetCwd/GetArgs).
- Provides ring-3 infrastructure: crt0.S startup shim, user.ld link at 0x2000000, mkosbx.py OSBX header/wrapper.

### Stable
- Build succeeds (`dotnet build`); compiles CRT0.S, userspace.o, module.o, probe.S via ilc NativeAOT; links user.ld; generates EXE staged at /OSB/BIN/<staged>.EXE mode 0100755; mkosbx.py wraps bin into EXE.

### Experimental / Incomplete
- File creation/write/delete, directories, environment and cooked input NOT implemented yet (reserved AH numbers return ENOSYS).
- ABI v6 was bumped to v7 in f79f11e STAGE-B fix; ABI v8 expected.
