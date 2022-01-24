using System;
using NUnit.Framework;
using ZfpDotnet;

namespace ZfpTests;

public class ZfpNativeTests
{
    [Test]
    [TestCase(1e-6)]
    [TestCase(1e-5)]
    [TestCase(1e-4)]
    [TestCase(1e-3)]
    [TestCase(null)]
    public void CompressDecompressFloat_ShouldHaveExpectedError(double? tolerance)
    {
        var floats = new float[1000];
        for (var i = 0; i < floats.Length; i++) floats[i] = Random.Shared.NextSingle();
        
        var buffer = new byte[floats.Length * sizeof(float)];
        var written = ZfpNative.Compress(floats, buffer, tolerance);
        var uncompressedBuffer = new byte[floats.Length * sizeof(float)];
        var read = ZfpNative.Decompress(buffer.AsSpan()[..(int)written], uncompressedBuffer, out var type, out var count);

        Assert.AreEqual(floats.Length, (int)count);
        Assert.AreEqual(written, read);
        Assert.AreEqual(ZfpNative.FieldType.Float, type);

        unsafe
        {
            fixed (void* ptr = uncompressedBuffer)
            {
                var span = new ReadOnlySpan<float>(ptr, (int)count);
                for (var i = 0; i < floats.Length; i++)
                {
                    if (floats[i] - span[i] > tolerance)
                        Assert.Fail("check tolerance");
                }
            }
        }
    }
    
    [Test]
    [TestCase(1e-3)]
    [TestCase(1e-4)]
    [TestCase(1e-5)]
    [TestCase(1e-6)]
    [TestCase(null)]
    public void CompressDecompressDouble_ShouldHaveExpectedError(double? tolerance)
    {
        var doubles = new double[1000];
        for (var i = 0; i < doubles.Length; i++) doubles[i] = Random.Shared.NextDouble();

        var buffer = new byte[doubles.Length * sizeof(double)];
        var written = ZfpNative.Compress(doubles, buffer, tolerance);
        var uncompressedBuffer = new byte[doubles.Length * sizeof(double)];

        var read = ZfpNative.Decompress(buffer.AsSpan()[..(int)written], uncompressedBuffer, out var type, out var count);

        Assert.AreEqual(doubles.Length, (int)count);
        Assert.AreEqual(written, read);
        Assert.AreEqual(ZfpNative.FieldType.Double, type);

        unsafe
        {
            fixed (void* ptr = uncompressedBuffer)
            {
                var error = tolerance ?? 0.000000001;
                
                var span = new ReadOnlySpan<double>(ptr, (int)count);
                for (var i = 0; i < doubles.Length; i++)
                {
                    if (doubles[i] - span[i] > error)
                        Assert.Fail("check tolerance");
                }
            }
        }
    }
    
    // [Test]
    // public void CompressDecompressLong_ShouldBeExact()
    // {
    //     var longs = new long[2500];
    //     for (var i = 0; i < longs.Length; i++) longs[i] = Random.Shared.NextInt64();
    //
    //     var buffer = new byte[(int)(longs.Length * sizeof(long))];
    //     var written = ZfpNative.Compress(longs, buffer);
    //     var uncompressedBuffer = new byte[longs.Length * sizeof(long)];
    //
    //     var read = ZfpNative.Decompress(buffer.AsSpan()[..(int)written], uncompressedBuffer, out var type, out var count);
    //
    //     Assert.AreEqual(longs.Length, (int)count);
    //     Assert.AreEqual(written, read);
    //     Assert.AreEqual(ZfpNative.FieldType.Int64, type);
    //
    //     unsafe
    //     {
    //         fixed (void* ptr = uncompressedBuffer)
    //         {
    //             var span = new ReadOnlySpan<long>(ptr, (int)count);
    //             for (var i = 0; i < longs.Length; i++)
    //             {
    //                 if (longs[i] != span[i])
    //                     Assert.Fail("check tolerance");
    //             }
    //         }
    //     }
    // }
    //
    // [Test]
    // public void CompressDecompressInt_ShouldHaveBeExact()
    // {
    //     var ints = new int[1000];
    //     for (var i = 0; i < ints.Length; i++) ints[i] = (int)Random.Shared.NextInt64();
    //
    //     var buffer = new byte[ints.Length * sizeof(int)];
    //     var written = ZfpNative.Compress(ints, buffer);
    //     var uncompressedBuffer = new byte[ints.Length * sizeof(int)];
    //
    //     var read = ZfpNative.Decompress(buffer.AsSpan()[..(int)written], uncompressedBuffer, out var type, out var count);
    //
    //     Assert.AreEqual(ints.Length, (int)count);
    //     Assert.AreEqual(written, read);
    //     Assert.AreEqual(ZfpNative.FieldType.Int32, type);
    //
    //     unsafe
    //     {
    //         fixed (void* ptr = uncompressedBuffer)
    //         {
    //             var span = new ReadOnlySpan<int>(ptr, (int)count);
    //             for (var i = 0; i < ints.Length; i++)
    //             {
    //                 if (ints[i] != span[i])
    //                     Assert.Fail("check tolerance");
    //             }
    //         }
    //     }
    // }
}