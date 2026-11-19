#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ABI_VER=$(grep -oE 'Version: \*\*[0-9]+\*\*' "$ROOT/docs/ABI.md" | grep -oE '[0-9]+' | head -1)
SDK_VER=$(grep -oE 'AbiVersion = [0-9]+' "$ROOT/Syscall.cs" | grep -oE '[0-9]+' | head -1)
KERNEL_VER=$(grep -oE 'AbiVersion = [0-9]+' "$ROOT/../osb.kernel/Cpu/Syscalls.cs" | grep -oE '[0-9]+' | head -1)
echo "ABI.md=$ABI_VER SDK=$SDK_VER KERNEL=$KERNEL_VER"
test "$ABI_VER" = "$SDK_VER"
test "$SDK_VER" = "$KERNEL_VER"
# no raw INT 21h / syscall numbers in shell outside osbkt
if grep -rn "0x21" "$ROOT/../osb.shell" --include=*.cs | grep -iv "osbkt" >/dev/null; then
  echo "raw syscall usage in shell"; exit 1
fi
echo OK