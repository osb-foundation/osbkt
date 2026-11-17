/* Ring-3 startup, syscall gate, and runtime helpers shared by every OSB
 * userspace program (shell, XWIN, test programs).
 *
 * The AOT startup symbol is named after the consuming module
 * (<module>__Module___StartupCodeMain), so this file calls the generic
 * `osb_startup` and the build defines it per consumer:
 *
 *   ld.lld ... --defsym=osb_startup=OsbShell__Module___StartupCodeMain
 *
 * Syscalls go through osb_syscall: register-based (SysV args), RAX carries
 * the function in AH and the AL-argument for the syscalls that take one
 * (4Ch exit code, 80h program id). See osbkt/docs/ABI.md. */

.section .text

.global _start
.type _start, @function
_start:
    andq $-16, %rsp
    call osb_startup
1:  hlt
    jmp 1b

/* osb_syscall(rax, rdi, rsi, rdx, rcx): SysV argument registers are
 * rdi, rsi, rdx, rcx, r8. We shuffle them into the INT 21h register image
 * and return RAX (the kernel writes the result into the frame's RAX slot
 * before iretq, so it arrives here directly). Only caller-saved registers
 * are touched (rax, rcx, rdx, rsi, rdi, r8-r11). */
.global osb_syscall
.type osb_syscall, @function
osb_syscall:
    movq %rdi, %rax
    movq %rsi, %rdi
    movq %rdx, %rsi
    movq %rcx, %rdx
    movq %r8,  %rcx
    int  $0x21
    ret

/* runtime helpers the AOT code may call */
.global memcpy
.type memcpy, @function
memcpy:
    movq %rdi, %rax
    movq %rdx, %rcx
    rep movsb
    ret

.global memset
.type memset, @function
memset:
    movq %rdi, %r9
    movq %rdx, %rcx
    movb %sil, %al
    rep stosb
    movq %r9, %rax
    ret

.global memmove
.type memmove, @function
memmove:
    movq %rdi, %rax
    movq %rdx, %rcx
    cmpq %rsi, %rdi
    jbe  3f
    leaq -1(%rdi,%rcx), %rdi
    leaq -1(%rsi,%rcx), %rsi
    std
    rep movsb
    cld
    ret
3:  rep movsb
    ret

.section .data
.align 8
.global __security_cookie
.type __security_cookie, @object
__security_cookie:
    .quad 0x2B992DDFA232

.section .note.GNU-stack, "", @progbits