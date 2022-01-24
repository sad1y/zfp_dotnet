namespace ZfpDotnet;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static unsafe class ZfpNative
{
    private const nuint HeaderMask = 0x7u; // full mask

    /// <summary>
    /// Compress one dimensional array of float
    /// <example>
    /// lossy example:
    /// <code>
    /// var written = ZfpNative.Compress(values, result, 1e-05);
    /// </code>
    /// <code>
    /// lossless example:
    /// var written = ZfpNative.Compress(values, result);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="data">source array</param>
    /// <param name="output">span which going to contain compressed data</param>
    /// <param name="tolerance">indicate level of compression. if tolerance not provided then lossless algorithm will be applied</param>
    /// <returns>bytes written</returns>
    public static uint Compress(ReadOnlySpan<float> data, Span<byte> output, double? tolerance = null)
    {
        fixed (void* dataPtr = data)
            return Compress(FieldType.Float, dataPtr, (nuint)data.Length, output, tolerance);
    }

    /// <summary>
    /// Compress one dimensional array of double
    /// <example>
    /// lossy example:
    /// <code>
    /// var written = ZfpNative.Compress(values, result, 1e-05);
    /// </code>
    /// <code>
    /// lossless example:
    /// var written = ZfpNative.Compress(values, result);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="data">source array</param>
    /// <param name="output">span which going to contain compressed data</param>
    /// <param name="tolerance">indicate level of compression. if tolerance not provided then lossless algorithm will be applied</param>
    /// <returns>bytes written</returns>
    public static uint Compress(ReadOnlySpan<double> data, Span<byte> output, double? tolerance = null)
    {
        fixed (void* dataPtr = data)
            return Compress(FieldType.Double, dataPtr, (nuint)data.Length, output, tolerance);
    }

    /// <summary>
    /// Compress one dimensional array of int
    /// <example>
    /// usage:
    /// <code>
    /// ZfpNative.Compress(values, result);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="data">source array</param>
    /// <param name="output">span which going to contain compressed data</param>
    /// <returns>bytes written</returns>
    [Obsolete("not supported")]
    public static uint Compress(ReadOnlySpan<int> data, Span<byte> output)
    {
        fixed (void* dataPtr = data)
            return Compress(FieldType.Int32, dataPtr, (nuint)data.Length, output);
    }

    /// <summary>
    /// Compress one dimensional array of long
    /// <example>
    /// usage:
    /// <code>
    /// ZfpNative.Compress(values, result);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="data">source array</param>
    /// <param name="output">span which going to contain compressed data</param>
    /// <returns>bytes written</returns>
    [Obsolete("not supported")]
    public static uint Compress(ReadOnlySpan<long> data, Span<byte> output)
    {
        fixed (void* dataPtr = data)
            return Compress(FieldType.Int64, dataPtr, (nuint)data.Length, output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Compress(FieldType type, void* dataPtr, nuint length, Span<byte> output, double? tolerance = null)
    {
        var field = ZfpField1d(new UIntPtr(dataPtr), type, length);
        var zfpStream = ZfpStreamOpen(UIntPtr.Zero);

        if (tolerance.HasValue)
            ZfpStreamSetAccuracy(zfpStream, tolerance.Value);
        else
            SetReversible(zfpStream);

        fixed (void* outputPtr = output)
        {
            var fieldPtr = new UIntPtr(field);
            var stream = StreamOpen(outputPtr, (nuint)output.Length);
            SetBitStream(zfpStream, stream);
            RewindStream(zfpStream);

            WriteHeader(zfpStream, fieldPtr, HeaderMask);
            var size = Compress(zfpStream, fieldPtr);

            FreeField(field);
            ZfpStreamClose(zfpStream);
            StreamClose(stream);

            return (uint)size;
        }
    }

    /// <summary>
    /// Decompress bytes array
    /// <example>
    /// usage:
    /// <code>
    ///     var read = ZfpNative.Decompress(compressed, uncompressed, out var field, out var count);
    ///     unsafe
    ///     {
    ///         fixed (byte* bufferPtr = uncompressed)
    ///         {
    ///             var values = new ReadOnlySpan<!--<int>-->(bufferPtr, (int)count);
    ///             ...
    ///         }
    ///     }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="compressedStream">source array</param>
    /// <param name="output">span which going to contain uncompressed data</param>
    /// <param name="fieldType">will be contains type of elements</param>
    /// <param name="unitCount">will be contains elements count</param>
    /// <returns>bytes read</returns>
    public static uint Decompress(ReadOnlySpan<byte> compressedStream, Span<byte> output, out FieldType fieldType, out uint unitCount)
    {
        fixed (void* dataPtr = compressedStream)
        {
            var zfpStream = ZfpStreamOpen(UIntPtr.Zero);

            fixed (void* outputPtr = output)
            {
                var field = ZfpField1d(new UIntPtr(outputPtr), FieldType.None, 0);
                var stream = StreamOpen(dataPtr, (nuint)output.Length);
                SetBitStream(zfpStream, stream);
                RewindStream(zfpStream);

                var fieldPtr = new UIntPtr(field);

                ReadHeader(zfpStream, fieldPtr, HeaderMask); // 0x7u - full mask

                unitCount = (uint)field->Nx;
                fieldType = field->Type;

                var size = Decompress(zfpStream, fieldPtr);

                FreeField(field);
                ZfpStreamClose(zfpStream);
                StreamClose(stream);

                return (uint)size;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Field
    {
        public readonly FieldType Type;

        /* sizes (zero for unused dimensions) */
        public readonly nuint Nx;
        public readonly nuint Ny;
        public readonly nuint Nz;

        public readonly nuint Nw;

        /* strides (zero for contiguous array a[nw][nz][ny][nx]) */
        public readonly nint Sx;
        public readonly nint Sy;
        public readonly nint Sz;
        public readonly nint Sw;

        public readonly IntPtr data;
    }

    /* scalar type */
    public enum FieldType
    {
        None = 0, /* unspecified type */
        Int32 = 1, /* 32-bit signed integer */
        Int64 = 2, /* 64-bit signed integer */
        Float = 3, /* single precision floating point */
        Double = 4 /* double precision floating point */
    }

    /*  zfp_stream_set_accuracy */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, 
        EntryPoint = "zfp_stream_set_accuracy")]
    private static extern void ZfpStreamSetAccuracy(UIntPtr zfpStream, double tolerance);

    /*  zfp compressed stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_open")]
    private static extern UIntPtr ZfpStreamOpen(UIntPtr stream);

    /* allocate and initialize bit stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "stream_open")]
    private static extern UIntPtr StreamOpen(void* buffer, nuint bytes);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "zfp_field_1d")]
    private static extern Field* ZfpField1d(UIntPtr data, FieldType type, nuint size);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_set_bit_stream")]
    private static extern UIntPtr SetBitStream(UIntPtr zfpStream, UIntPtr bitStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_rewind")]
    private static extern UIntPtr RewindStream(UIntPtr zfpStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "zfp_compress")]
    private static extern nuint Compress(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "zfp_decompress")]
    private static extern nuint Decompress(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "zfp_field_free")]
    private static extern nuint FreeField(Field* field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_close")]
    private static extern nuint ZfpStreamClose(UIntPtr field);

    /* close and deallocate bit stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, EntryPoint = "stream_close")]
    private static extern nuint StreamClose(UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_maximum_size")]
    private static extern nuint StreamMaximumSize(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_set_reversible")]
    private static extern nuint SetReversible(UIntPtr zfpStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_write_header")]
    private static extern nuint WriteHeader(UIntPtr zfpStream, UIntPtr field, nuint mask);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_read_header")]
    private static extern nuint ReadHeader(UIntPtr zfpStream, UIntPtr field, nuint mask);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall,
        EntryPoint = "zfp_stream_compressed_size")]
    private static extern nuint ZfpStreamSize(UIntPtr zfpStream);
}