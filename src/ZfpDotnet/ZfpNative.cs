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
        var field = zfp_field_1d(new UIntPtr(dataPtr), type, length);
        var zfpStream = zfp_stream_open(UIntPtr.Zero);

        if (tolerance.HasValue)
            zfp_stream_set_accuracy(zfpStream, tolerance.Value);
        else
            zfp_stream_set_reversible(zfpStream);

        fixed (void* outputPtr = output)
        {
            var fieldPtr = new UIntPtr(field);
            var stream = stream_open(outputPtr, (nuint)output.Length);
            zfp_stream_set_bit_stream(zfpStream, stream);
            zfp_stream_rewind(zfpStream);

            zfp_write_header(zfpStream, fieldPtr, HeaderMask);
            var size = zfp_compress(zfpStream, fieldPtr);

            zfp_field_free(field);
            zfp_stream_close(zfpStream);
            stream_close(stream);

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
            var zfpStream = zfp_stream_open(UIntPtr.Zero);

            fixed (void* outputPtr = output)
            {
                var field = zfp_field_1d(new UIntPtr(outputPtr), FieldType.None, 0);
                var stream = stream_open(dataPtr, (nuint)output.Length);
                zfp_stream_set_bit_stream(zfpStream, stream);
                zfp_stream_rewind(zfpStream);

                var fieldPtr = new UIntPtr(field);

                zfp_read_header(zfpStream, fieldPtr, HeaderMask); // 0x7u - full mask

                unitCount = (uint)field->Nx;
                fieldType = field->Type;

                var size = zfp_decompress(zfpStream, fieldPtr);

                zfp_field_free(field);
                zfp_stream_close(zfpStream);
                stream_close(stream);

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
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern void zfp_stream_set_accuracy(UIntPtr zfpStream, double tolerance);

    /*  zfp compressed stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern UIntPtr zfp_stream_open(UIntPtr stream);

    /* allocate and initialize bit stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern UIntPtr stream_open(void* buffer, nuint bytes);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern Field* zfp_field_1d(UIntPtr data, FieldType type, nuint size);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern UIntPtr zfp_stream_set_bit_stream(UIntPtr zfpStream, UIntPtr bitStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern UIntPtr zfp_stream_rewind(UIntPtr zfpStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_compress(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_decompress(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_field_free(Field* field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_stream_close(UIntPtr field);

    /* close and deallocate bit stream */
    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint stream_close(UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_stream_maximum_size(UIntPtr zfpStream, UIntPtr field);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_stream_set_reversible(UIntPtr zfpStream);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_write_header(UIntPtr zfpStream, UIntPtr field, nuint mask);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_read_header(UIntPtr zfpStream, UIntPtr field, nuint mask);

    [DllImport("libzfp", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern nuint zfp_stream_compressed_size(UIntPtr zfpStream);
}