using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // FIX: Added for BigInteger
using System.Runtime.InteropServices;

namespace FpgaInterface
{
    /// <summary>
    /// Provides access to a specific FPGA Register (Control or Indicator).
    /// This class is not generic, but provides generic methods for I/O.
    /// </summary>
    public class FpgaRegister
    {
        private readonly FpgaSession _session;
        private readonly uint _offset;
        private readonly bool _isComplex;
        
        // FIX: Made Info internal so FpgaSession can access it
        internal readonly RegisterInfo Info;

        internal FpgaRegister(FpgaSession session, RegisterInfo info)
        {
            _session = session;
            Info = info; // FIX: Was _info
            _offset = info.Offset;
            if (info.AccessMayTimeout)
            {
                _offset |= 0x80000000;
            }

            // "Complex" means it's not a simple primitive and requires
            // packing/unpacking via U32 array.
            _isComplex = !(info.TypeInfo is PrimitiveTypeInfo) || (info.TypeInfo is ArrayTypeInfo);
        }

        /// <summary>
        /// Reads the current value from the register.
        /// </summary>
        /// <typeparam name="T">The expected type (e.g., uint, bool, FxpValue, Dictionary&lt;string, object&gt;).</typeparam>
        /// <returns>The value read from the FPGA.</returns>
        public T Read<T>()
        {
            // FIX: Was _info.TypeInfo
            if (typeof(T) != Info.TypeInfo.PublicType)
            {
                throw new InvalidCastException($"Register '{Info.Name}' is of type {Info.TypeInfo.PublicType.Name}, but was requested as {typeof(T).Name}.");
            }
            return (T)Read();
        }

        /// <summary>
        /// Reads the value from the register and returns it as an object.
        /// </summary>
        public object Read()
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "indicator", _offset } };

            if (!_isComplex)
            {
                // Handle simple primitives directly
                var typeInfo = (PrimitiveTypeInfo)Info.TypeInfo; // FIX: Was _info
                if (typeInfo.PublicType == typeof(bool))
                {
                    StatusChecker.CheckStatus(NativeMethods.ReadBool(_session.Handle, _offset, out byte val), nameof(NativeMethods.ReadBool), args);
                    return val != 0;
                }
                if (typeInfo.PublicType == typeof(sbyte))
                {
                    StatusChecker.CheckStatus(NativeMethods.ReadI8(_session.Handle, _offset, out sbyte val), nameof(NativeMethods.ReadI8), args);
                    return val;
                }
                // ... (Add cases for U8, I16, U16, I32, I64, U64, Sgl, Dbl) ...
                if (typeInfo.PublicType == typeof(uint))
                {
                    StatusChecker.CheckStatus(NativeMethods.ReadU32(_session.Handle, _offset, out uint val), nameof(NativeMethods.ReadU32), args);
                    return val;
                }
                throw new NotSupportedException($"Read for primitive type {typeInfo.PublicType} not implemented.");
            }
            else
            {
                // Handle Complex types (FXP, Cluster, Array)
                // These are all read as U32 arrays, matching _DataConvertingRegister
                //
                int numElements = (Info.TypeInfo.SizeInBits + 31) / 32; // FIX: Was _info
                var buffer = new uint[numElements];
                
                StatusChecker.CheckStatus(NativeMethods.ReadArrayU32(_session.Handle, _offset, buffer, (UIntPtr)numElements), nameof(NativeMethods.ReadArrayU32), args);
                
                // Convert U32[] to byte[] for BitReader
                var byteBuffer = new byte[numElements * 4];
                Buffer.BlockCopy(buffer, 0, byteBuffer, 0, byteBuffer.Length);

                // Shift data to be LSB-aligned, as Python does
                //
                var shiftedBytes = ShiftBytesRight(byteBuffer, (numElements * 32) - Info.TypeInfo.SizeInBits); // FIX: Was _info
                var reader = new BitReader(shiftedBytes);
                return Info.TypeInfo.Unpack(reader); // FIX: Was _info
            }
        }
        
        /// <summary>
        /// Writes a value to the register.
        /// </summary>
        /// <param name="value">The value to write (e.g., uint, bool, FxpValue, Dictionary&lt;string, object&gt;).</param>
        public void Write(object value)
        {
            _session.CheckHandle();
            var args = new Dictionary<string, object> { { "session", _session.Handle }, { "control", _offset }, { "value", value } };

            if (!_isComplex)
            {
                // Handle simple primitives directly
                var typeInfo = (PrimitiveTypeInfo)Info.TypeInfo; // FIX: Was _info
                if (typeInfo.PublicType == typeof(bool))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteBool(_session.Handle, _offset, (bool)value ? (byte)1: (byte)0), nameof(NativeMethods.WriteBool), args);
                    return;
                }
                if (typeInfo.PublicType == typeof(sbyte))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteI8(_session.Handle, _offset, (sbyte)value), nameof(NativeMethods.WriteI8), args);
                    return;
                }
                // ... (Add cases for U8, I16, U16, I32, I64, U64, Sgl, Dbl) ...
                if (typeInfo.PublicType == typeof(uint))
                {
                    StatusChecker.CheckStatus(NativeMethods.WriteU32(_session.Handle, _offset, (uint)value), nameof(NativeMethods.WriteU32), args);
                    return;
                }
                throw new NotSupportedException($"Write for primitive type {typeInfo.PublicType} not implemented.");
            }
            else
            {
                // Handle Complex types (FXP, Cluster, Array)
                // These are all written as U32 arrays
                //
                var writer = new BitWriter();
                Info.TypeInfo.Pack(value, writer); // FIX: Was _info

                int numElements = (Info.TypeInfo.SizeInBits + 31) / 32; // FIX: Was _info
                var byteBuffer = writer.GetBytes();

                // Shift data to be MSB-aligned, as Python does
                //
                var shiftedBytes = ShiftBytesLeft(byteBuffer, (numElements * 32) - Info.TypeInfo.SizeInBits); // FIX: Was _info
                
                // Ensure buffer is correct size
                var finalByteBuffer = new byte[numElements * 4];
                Array.Copy(shiftedBytes, 0, finalByteBuffer, 0, Math.Min(shiftedBytes.Length, finalByteBuffer.Length));

                // Convert byte[] to U32[]
                var buffer = new uint[numElements];
                Buffer.BlockCopy(finalByteBuffer, 0, buffer, 0, finalByteBuffer.Length);
                
                StatusChecker.CheckStatus(NativeMethods.WriteArrayU32(_session.Handle, _offset, buffer, (UIntPtr)numElements), nameof(NativeMethods.WriteArrayU32), args);
            }
        }

        // Helper functions for bit-shifting byte arrays (to align data within the U32 array)
        private static byte[] ShiftBytesRight(byte[] data, int bits)
        {
            var num = new BigInteger(data, isBigEndian: false, isUnsigned: true);
            num >>= bits;
            return num.ToByteArray(isUnsigned: true, isBigEndian: false);
        }

        private static byte[] ShiftBytesLeft(byte[] data, int bits)
        {
            var num = new BigInteger(data, isBigEndian: false, isUnsigned: true);
            num <<= bits;
            return num.ToByteArray(isUnsigned: true, isBigEndian: false);
        }
    }
}