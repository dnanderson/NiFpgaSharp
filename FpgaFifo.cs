using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // Required for BitShiftHelpers

namespace FpgaInterface
{
    /// <summary>
    /// Provides access to a specific FPGA FIFO.
    /// This class is not generic, but provides generic methods for I/O.
    /// </summary>
    public class FpgaFifo
    {
        private readonly FpgaSession _session;
        private readonly uint _fifoNumber;
        private readonly bool _isComplex;

        // FIX: Made Info internal so FpgaSession can access it
        internal readonly FifoInfo Info;

        internal FpgaFifo(FpgaSession session, FifoInfo info)
        {
            _session = session;
            Info = info; // FIX: Was _info
            _fifoNumber = info.Number;
            
            // "Complex" means it uses the ...Composite functions (raw bytes)
            //
            _isComplex = (info.TypeInfo is FxpTypeInfo) || (info.TypeInfo is ClusterTypeInfo) || (info.TypeInfo is ArrayTypeInfo);
        }

        /// <summary>
        /// Specifies the depth of the host memory part of the DMA FIFO.
        /// This must be called before the FIFO is started.
        /// </summary>
        /// <param name="requestedDepth">The depth of the FIFO in number of elements.</param>
        /// <returns>The actual depth of the FIFO.</returns>
        public ulong Configure(ulong requestedDepth)
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "fifo", _fifoNumber }, { "depth", requestedDepth } };
            StatusChecker.CheckStatus(NativeMethods.ConfigureFifo2(_session.Handle, _fifoNumber, (UIntPtr)requestedDepth, out UIntPtr actualDepth), nameof(NativeMethods.ConfigureFifo2), args);
            return (ulong)actualDepth;
        }

        /// <summary>Starts the FIFO.</summary>
        public void Start()
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "fifo", _fifoNumber } };
            StatusChecker.CheckStatus(NativeMethods.StartFifo(_session.Handle, _fifoNumber), nameof(NativeMethods.StartFifo), args);
        }

        /// <summary>Stops the FIFO.</summary>
        public void Stop()
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "fifo", _fifoNumber } };
            StatusChecker.CheckStatus(NativeMethods.StopFifo(_session.Handle, _fifoNumber), nameof(NativeMethods.StopFifo), args);
        }

        /// <summary>
        /// Reads a specific number of elements from the FIFO.
        /// </summary>
        /// <typeparam name="T">The expected element type (e.g., uint, FxpValue, Dictionary&lt;string, object&gt;).</typeparam>
        /// <param name="count">The number of elements to read.</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>A FifoReadResult record containing the data and elements remaining.</returns>
        public FifoReadResult<T> Read<T>(int count, uint timeoutMs = 0)
        {
            var expectedType = typeof(T);
            // FIX: Was _info.TypeInfo
            if (Info.TypeInfo.PublicType != expectedType)
            {
                throw new InvalidCastException($"FIFO '{Info.Name}' is of type {Info.TypeInfo.PublicType.Name}, but was requested as {expectedType.Name}.");
            }

            var (data, remaining) = Read(count, timeoutMs);
            return new FifoReadResult<T>(data.Cast<T>().ToArray(), remaining);
        }

        /// <summary>
        /// Reads a specific number of elements from the FIFO.
        /// </summary>
        /// <param name="count">The number of elements to read.</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>A tuple containing the object array of data and elements remaining.</returns>
        public (object[] Data, uint ElementsRemaining) Read(int count, uint timeoutMs = 0)
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "fifo", _fifoNumber }, { "count", count }, { "timeout", timeoutMs } };

            object[] results = new object[count];
            UIntPtr remaining;

            if (!_isComplex)
            {
                // Handle simple primitives directly
                var typeInfo = (PrimitiveTypeInfo)Info.TypeInfo; // FIX: Was _info
                // *** FIX: Implemented all primitive types ***
                if (typeInfo.PublicType == typeof(bool))
                {
                    var buffer = new byte[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoBool(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoBool), args);
                    for (int i = 0; i < count; i++) results[i] = buffer[i] != 0;
                }
                else if (typeInfo.PublicType == typeof(sbyte))
                {
                    var buffer = new sbyte[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoI8(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoI8), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(sbyte));
                }
                else if (typeInfo.PublicType == typeof(byte))
                {
                    var buffer = new byte[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoU8(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoU8), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(byte));
                }
                else if (typeInfo.PublicType == typeof(short))
                {
                    var buffer = new short[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoI16(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoI16), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(short));
                }
                else if (typeInfo.PublicType == typeof(ushort))
                {
                    var buffer = new ushort[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoU16(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoU16), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(ushort));
                }
                else if (typeInfo.PublicType == typeof(int))
                {
                    var buffer = new int[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoI32(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoI32), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(int));
                }
                else if (typeInfo.PublicType == typeof(uint))
                {
                    var buffer = new uint[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoU32(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoU32), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(uint));
                }
                else if (typeInfo.PublicType == typeof(long))
                {
                    var buffer = new long[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoI64(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoI64), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(long));
                }
                else if (typeInfo.PublicType == typeof(ulong))
                {
                    var buffer = new ulong[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoU64(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoU64), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(ulong));
                }
                else if (typeInfo.PublicType == typeof(float))
                {
                    var buffer = new float[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoSgl(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoSgl), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(float));
                }
                else if (typeInfo.PublicType == typeof(double))
                {
                    var buffer = new double[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoDbl(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoDbl), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(double));
                }
                else
                {
                    throw new NotSupportedException($"Read for primitive FIFO type {typeInfo.PublicType} not implemented.");
                }
            }
            else
            {
                // Handle Complex types (FXP, Cluster, Array) via ...Composite functions
                //
                int bytesPerElement = Info.TransferSizeBytes; // FIX: Was _info
                var buffer = new byte[count * bytesPerElement];
                StatusChecker.CheckStatus(NativeMethods.ReadFifoComposite(_session.Handle, _fifoNumber, buffer, (uint)bytesPerElement, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoComposite), args);

                for (int i = 0; i < count; i++)
                {
                    var elementBytes = new byte[bytesPerElement];
                    Array.Copy(buffer, i * bytesPerElement, elementBytes, 0, bytesPerElement);
                    
                    // The ...Composite functions in Python swap endianness
                    var swappedBytes = SwapFifoEndianness(elementBytes, bytesPerElement);
                    
                    // *** BUG FIX 2: Apply MSB-to-LSB alignment shift ***
                    // Replicates Python's _combine_array_of_u8_into_one_value logic
                    int bitsToShift = (bytesPerElement * 8) - Info.TypeInfo.SizeInBits;
                    var shiftedBytes = BitShiftHelpers.ShiftBytesRight(swappedBytes, bitsToShift);

                    var reader = new BitReader(shiftedBytes);
                    results[i] = Info.TypeInfo.Unpack(reader); // FIX: Was _info
                }
            }
            return (results, (uint)remaining);
        }

        /// <summary>
        /// Writes an array of elements to the FIFO.
        /// </summary>
        /// <param name="data">The array of data to write.</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>The number of empty elements remaining in the FIFO.</returns>
        public uint Write(Array data, uint timeoutMs = 0)
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "fifo", _fifoNumber }, { "count", data.Length }, { "timeout", timeoutMs } };
            UIntPtr remaining;
            
            if (!_isComplex)
            {
                // Handle simple primitives directly
                var typeInfo = (PrimitiveTypeInfo)Info.TypeInfo; // FIX: Was _info
                // *** FIX: Implemented all primitive types ***
                if (typeInfo.PublicType == typeof(bool))
                {
                    var buffer = data.Cast<bool>().Select(b => b ? (byte)1 : (byte)0).ToArray();
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoBool(_session.Handle, _fifoNumber, buffer, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoBool), args);
                }
                else if (typeInfo.PublicType == typeof(sbyte))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoI8(_session.Handle, _fifoNumber, (sbyte[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoI8), args);
                }
                else if (typeInfo.PublicType == typeof(byte))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoU8(_session.Handle, _fifoNumber, (byte[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoU8), args);
                }
                else if (typeInfo.PublicType == typeof(short))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoI16(_session.Handle, _fifoNumber, (short[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoI16), args);
                }
                else if (typeInfo.PublicType == typeof(ushort))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoU16(_session.Handle, _fifoNumber, (ushort[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoU16), args);
                }
                else if (typeInfo.PublicType == typeof(int))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoI32(_session.Handle, _fifoNumber, (int[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoI32), args);
                }
                else if (typeInfo.PublicType == typeof(uint))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoU32(_session.Handle, _fifoNumber, (uint[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoU32), args);
                }
                else if (typeInfo.PublicType == typeof(long))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoI64(_session.Handle, _fifoNumber, (long[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoI64), args);
                }
                else if (typeInfo.PublicType == typeof(ulong))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoU64(_session.Handle, _fifoNumber, (ulong[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoU64), args);
                }
                else if (typeInfo.PublicType == typeof(float))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoSgl(_session.Handle, _fifoNumber, (float[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoSgl), args);
                }
                else if (typeInfo.PublicType == typeof(double))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoDbl(_session.Handle, _fifoNumber, (double[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoDbl), args);
                }
                else
                {
                    throw new NotSupportedException($"Write for primitive FIFO type {typeInfo.PublicType} not implemented.");
                }
            }
            else
            {
                // Handle Complex types (FXP, Cluster, Array) via ...Composite functions
                //
                int bytesPerElement = Info.TransferSizeBytes; // FIX: Was _info
                var buffer = new byte[data.Length * bytesPerElement];
                var writer = new BitWriter();

                for (int i = 0; i < data.Length; i++)
                {
                    writer.WriteUInt(0, 0); // Reset writer

                    // FIX: Handle possible null value in array
                    object? elementValue = data.GetValue(i);
                    if (elementValue == null)
                        throw new ArgumentNullException(nameof(data), $"Element at index {i} is null.");
                    
                    Info.TypeInfo.Pack(elementValue, writer); // FIX: Was _info
                    var elementBytes = writer.GetBytes();

                    // *** BUG FIX 3: Apply LSB-to-MSB alignment shift ***
                    // Replicates Python's _convert_to_u8_array logic
                    int bitsToShift = (bytesPerElement * 8) - Info.TypeInfo.SizeInBits;
                    var shiftedBytes = BitShiftHelpers.ShiftBytesLeft(elementBytes, bitsToShift);

                    // Replicate Python's endian swap
                    var swappedBytes = SwapFifoEndianness(shiftedBytes, bytesPerElement);
                    
                    // Ensure the swapped bytes fit into the per-element buffer slice
                    var finalElementBytes = new byte[bytesPerElement];
                    Array.Copy(swappedBytes, 0, finalElementBytes, 0, Math.Min(swappedBytes.Length, finalElementBytes.Length));
                    
                    Array.Copy(finalElementBytes, 0, buffer, i * bytesPerElement, finalElementBytes.Length);
                }
                
                StatusChecker.CheckStatus(NativeMethods.WriteFifoComposite(_session.Handle, _fifoNumber, buffer, (uint)bytesPerElement, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoComposite), args);
            }
            return (uint)remaining;
        }

        /// <summary>
        /// Swaps endianness of FIFO data to match the Python implementation.
        ///
        /// </summary>
        private byte[] SwapFifoEndianness(byte[] data, int bytesPerElement)
        {
            if (bytesPerElement <= 2) return data; // 1 and 2 byte elements are not swapped in python

            var swapped = new byte[data.Length];
            int words = data.Length / 4;
            
            for (int i = 0; i < words; i++)
            {
                int baseIdx = i * 4;
                // Handle partial last word if data.Length is not a multiple of 4
                if (baseIdx + 3 < data.Length)
                {
                    swapped[baseIdx] = data[baseIdx + 3];
                    swapped[baseIdx + 1] = data[baseIdx + 2];
                    swapped[baseIdx + 2] = data[baseIdx + 1];
                    swapped[baseIdx + 3] = data[baseIdx];
                }
                else if (baseIdx + 2 < data.Length)
                {
                    swapped[baseIdx] = data[baseIdx + 2];
                    swapped[baseIdx + 1] = data[baseIdx + 1];
                    swapped[baseIdx + 2] = data[baseIdx];
                }
                else if (baseIdx + 1 < data.Length)
                {
                    swapped[baseIdx] = data[baseIdx + 1];
                    swapped[baseIdx + 1] = data[baseIdx];
                }
                else if (baseIdx < data.Length)
                {
                    swapped[baseIdx] = data[baseIdx];
                }
            }
            return swapped;
        }
    }
}