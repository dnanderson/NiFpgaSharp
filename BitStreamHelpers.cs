using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics; // FIX: Added for BigInteger

namespace FpgaInterface
{
    /// <summary>
    /// Reads arbitrary bit-lengths from a byte buffer.
    /// Assumes data is packed LSB-first.
    /// </summary>
    internal class BitReader
    {
        private readonly byte[] _buffer;
        private int _bitPosition;

        public BitReader(byte[] buffer)
        {
            _buffer = buffer;
            _bitPosition = 0;
        }

        public bool ReadBool()
        {
            int byteIndex = _bitPosition / 8;
            int bitInByte = _bitPosition % 8;
            _bitPosition++;
            if (byteIndex >= _buffer.Length) return false;
            return (_buffer[byteIndex] & (1 << bitInByte)) != 0;
        }

        public ulong ReadUInt(int bitCount)
        {
            ulong value = 0;
            for (int i = 0; i < bitCount; i++)
            {
                if (ReadBool())
                {
                    value |= (1UL << i);
                }
            }
            return value;
        }

        public long ReadInt(int bitCount)
        {
            ulong raw = ReadUInt(bitCount);
            ulong signBit = 1UL << (bitCount - 1);
            if ((raw & signBit) != 0) // Is negative
            {
                ulong mask = (ulong.MaxValue << bitCount);
                return (long)(raw | mask);
            }
            return (long)raw;
        }

        public float ReadFloat32() => BitConverter.ToSingle(_buffer, _bitPosition / 8);
        public double ReadFloat64() => BitConverter.ToDouble(_buffer, _bitPosition / 8);

        public decimal ReadFxp(int wordLength, bool isSigned, decimal delta)
        {
            long rawValue;
            if (isSigned)
            {
                rawValue = ReadInt(wordLength);
            }
            else
            {
                rawValue = (long)ReadUInt(wordLength);
            }
            return (decimal)rawValue * delta;
        }
    }

    /// <summary>
    /// Writes arbitrary bit-lengths to a byte buffer.
    /// Packs data LSB-first.
    /// </summary>
    internal class BitWriter
    {
        private readonly List<byte> _buffer = new List<byte>();
        private int _bitPosition = 0;

        public byte[] GetBytes() => _buffer.ToArray();
        public int BitLength => _bitPosition;

        private void EnsureCapacity(int bitIndex)
        {
            int byteIndex = bitIndex / 8;
            while (_buffer.Count <= byteIndex)
            {
                _buffer.Add(0);
            }
        }

        public void WriteBool(bool value)
        {
            EnsureCapacity(_bitPosition);
            int byteIndex = _bitPosition / 8;
            int bitInByte = _bitPosition % 8;
            if (value)
            {
                _buffer[byteIndex] = (byte)(_buffer[byteIndex] | (1 << bitInByte));
            }
            _bitPosition++;
        }

        public void WriteUInt(ulong value, int bitCount)
        {
            for (int i = 0; i < bitCount; i++)
            {
                WriteBool((value & (1UL << i)) != 0);
            }
        }

        public void WriteInt(long value, int bitCount)
        {
            WriteUInt((ulong)value, bitCount);
        }
        
        public void WriteFloat32(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            foreach (byte b in bytes)
            {
                WriteUInt(b, 8);
            }
        }
        
        public void WriteFloat64(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            foreach (byte b in bytes)
            {
                WriteUInt(b, 8);
            }
        }

        public void WriteFxp(decimal value, int wordLength, bool isSigned, decimal delta)
        {
            BigInteger rawValue = (BigInteger)(value / delta);
            WriteInt((long)rawValue, wordLength);
        }
    }
}