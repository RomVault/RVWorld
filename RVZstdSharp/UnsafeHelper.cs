﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InlineIL;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

[module: SkipLocalsInit]

namespace RVZstdSharp
{
    public static unsafe class UnsafeHelper
    {
        public static void* PoisonMemory(void* destination, ulong size)
        {
            memset(destination, 0xCC, (uint) size);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* malloc(uint size)
        {
#if DEBUG
            return PoisonMemory((void*)Marshal.AllocHGlobal((int)size), size);
#else
            return (void*) Marshal.AllocHGlobal((int) size);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* malloc(ulong size)
        {
#if DEBUG
            return PoisonMemory((void*) Marshal.AllocHGlobal((nint) size), size);
#else
            return (void*) Marshal.AllocHGlobal((nint) size);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* calloc(ulong num, ulong size)
        {
            var total = num * size;
            assert(total <= uint.MaxValue);
            var destination = (void*) Marshal.AllocHGlobal((nint) total);
            memset(destination, 0, (uint) total);
            return destination;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memcpy(void* destination, void* source, uint size)
            => System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(destination, source, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memset(void* memPtr, byte val, uint size)
            => System.Runtime.CompilerServices.Unsafe.InitBlockUnaligned(memPtr, val, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void memset<T>(ref T memPtr, byte val, uint size)
            => System.Runtime.CompilerServices.Unsafe.InitBlockUnaligned(
                ref System.Runtime.CompilerServices.Unsafe.As<T, byte>(ref memPtr), val, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void free(void* ptr)
        {
            Marshal.FreeHGlobal((IntPtr) ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetArrayPointer<T>(T[] array) where T : unmanaged
        {
            var size = (uint) (sizeof(T) * array.Length);
            var destination = (T*) malloc(size);
            fixed (void* source = &array[0])
                System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(destination, source, size);

            return destination;
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void assert(bool condition, string message = null)
        {
            if (!condition)
                throw new ArgumentException(message ?? "assert failed");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void memmove(void* destination, void* source, ulong size)
            => Buffer.MemoryCopy(source, destination, size, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int memcmp(void* buf1, void* buf2, ulong size)
        {
            assert(size <= int.MaxValue);
            var intSize = (int)size;
            return new ReadOnlySpan<byte>(buf1, intSize)
                .SequenceCompareTo(new ReadOnlySpan<byte>(buf2, intSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [InlineMethod.Inline]
        public static void SkipInit<T>(out T value)
        {
            /* 
             * Can be rewritten with
             * System.Runtime.CompilerServices.Unsafe.SkipInit(out value);
             * in .NET 5+
             */
            IL.Emit.Ret();
            throw IL.Unreachable();
        }
    }
}
