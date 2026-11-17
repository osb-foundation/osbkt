/* Ring-3 startup, syscall gate, and runtime helpers shared by every OSB
 * userspace program (shell, XWIN, ...).
 *
 * The AOT startup symbol is named after the consuming module
 * (<module>__Module___StartupCodeMain), so this file calls the generic
 * `osb_startup` and the build defines it per consumer:
 *
 *   ld.lld ... --defsym=osb_startup=OsbShell__Module___StartupCodeMain
 *
 * See osbkt/docs/ABI.md ("crt0 and the entry symbol"). */

.section .text

.global _start
.type _start, @function
_start:
    andq $-16, %rsp
    call osb_startup
1:  hlt
    jmp 1b

/* HandleSyscall(frame): load the DOS-style register image, INT 21h, store result. */
.global HandleSyscall
.type HandleSyscall, @function
HandleSyscall:
    movq %rdi, %r8
    movq 112(%r8), %rax
    movq 96(%r8), %rcx
    movq 88(%r8), %rdx
    movq 80(%r8), %rsi
    movq 72(%r8), %rdi
    int $0x21
    movq %rax, 112(%r8)
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