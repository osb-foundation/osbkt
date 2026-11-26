# Changelog

## [0.1.0] - Olorun (Generation 0)

### Added
- Initial release of osbkt under OSB generation 0 (Olorun).
- OSB ring-3 toolkit: INT 21h syscall wrappers, crt0.S, user.ld, mkosbx.py (OSBX packaging), ABI contract AbiVersion=8.
- Static library wrapping kernel INT 21h syscalls in a clean C# API: console (PrintString/PutChar/GetKeyScancode); process (Exit/Spawn/Wait/GetPid/GetABI); memory (MemInfo); filesystem (Open/Read/Close/CreateFile/WriteFile/Stat/Mkdir/Rmdir/Delete/Rename/Copy/Ln/Trash, ResolvePath/DirNext); environment (GetCwd/GetArgs/GetEnv); console modes (SetStdin/SetStdout).
- Provides ring-3 infrastructure: crt0.S startup shim, user.ld link at 0x2000000, mkosbx.py OSBX header/wrapper.

### Stable
- Build succeeds (`dotnet build`); compiles CRT0.S, userspace.o, module.o, probe.S via ilc NativeAOT; links user.ld; generates EXE staged at /OSB/BIN/<staged>.EXE mode 0100755; mkosbx.py wraps bin into EXE.

### Experimental / Incomplete
- ABI v8 (filesystem write operations 24h–2Ch, 60h, 61h, 76h–79h) is implemented in the kernel and exposed here; some reserved AH numbers still return ENOSYS.
- Cooked/line input support is partial.