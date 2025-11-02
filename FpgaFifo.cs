using System;
using System.Collections.Generic;
using System.Linq;

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
                if (typeInfo.PublicType == typeof(uint))
                {
                    var buffer = new uint[count];
                    StatusChecker.CheckStatus(NativeMethods.ReadFifoU32(_session.Handle, _fifoNumber, buffer, (UIntPtr)count, timeoutMs, out remaining), nameof(NativeMethods.ReadFifoU32), args);
                    Buffer.BlockCopy(buffer, 0, results, 0, count * sizeof(uint));
                }
                // ... (Add cases for I8, U8, I16, U16, I32, I64, U64, Sgl, Dbl, Bool) ...
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
                    // Note: Python _combine_array_of_u8_into_one_value does byte swapping. We assume
                    // the BitReader handles this by being LSB-first.
                    // The ...Composite functions in Python swap endianness, which we must replicate.
                    var reader = new BitReader(SwapFifoEndianness(elementBytes, bytesPerElement));
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
                if (typeInfo.PublicType == typeof(uint))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteFifoU32(_session.Handle, _fifoNumber, (uint[])data, (UIntPtr)data.Length, timeoutMs, out remaining), nameof(NativeMethods.WriteFifoU32), args);
                }
                // ... (Add cases for I8, U8, I16, U16, I32, I64, U64, Sgl, Dbl, Bool) ...
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
                    // Replicate Python's _convert_to_u8_array logic (endian swap)
                    var swappedBytes = SwapFifoEndianness(elementBytes, bytesPerElement);
                    Array.Copy(swappedBytes, 0, buffer, i * bytesPerElement, swappedBytes.Length);
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
            for (int i = 0; i < data.Length; i += 4)
            {
                swapped[i] = data[i + 3];
                swapped[i + 1] = data[i + 2];
                swapped[i + 2] = data[i + 1];
                swapped[i + 3] = data[i];
            }
            return swapped;
        }
    }
}