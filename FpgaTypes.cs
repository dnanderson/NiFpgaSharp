using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace FpgaInterface
{
    //=================================================================
    // Public Facing Types
    //=================================================================
    
    /// <summary>
    /// Represents the result of a FIFO read operation.
    /// </summary>
    /// <typeparam name="T">The type of data read from the FIFO.</typeparam>
    public record FifoReadResult<T>(T[] Data, uint ElementsRemaining);

    /// <summary>
    /// Represents the result of an IRQ wait operation.
    /// </summary>
    /// <param name="IrqsAsserted">A bitmask of the IRQs that asserted.</param>
    /// <param name="TimedOut">True if the wait timed out, false otherwise.</param>
    public record IrqWaitResult(uint IrqsAsserted, bool TimedOut);

    /// <summary>
    /// Represents a Fixed Point (FXP) value without overflow status.
    /// </summary>
    public readonly struct FxpValue : IEquatable<FxpValue>
    {
        public decimal Value { get; }
        public FxpValue(decimal value) { Value = value; }
        public bool Equals(FxpValue other) => Value == other.Value;
        // FIX: Changed to object? to match base
        public override bool Equals(object? obj) => obj is FxpValue other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static implicit operator decimal(FxpValue fxp) => fxp.Value;
        public static explicit operator FxpValue(decimal d) => new FxpValue(d);
    }

    /// <summary>
    /// Represents a Fixed Point (FXP) value that includes an overflow status.
    ///
    /// </summary>
    public readonly struct FxpValueWithOverflow : IEquatable<FxpValueWithOverflow>
    {
        public bool Overflow { get; }
        public decimal Value { get; }
        public FxpValueWithOverflow(bool overflow, decimal value) { Overflow = overflow; Value = value; }
        public void Deconstruct(out bool overflow, out decimal value) { overflow = Overflow; value = Value; }
        public bool Equals(FxpValueWithOverflow other) => Overflow == other.Overflow && Value == other.Value;
        // FIX: Changed to object? to match base
        public override bool Equals(object? obj) => obj is FxpValueWithOverflow other && Equals(other);
        public override int GetHashCode() => (Value, Overflow).GetHashCode();
        public override string ToString() => $"(Overflow: {Overflow}, Value: {Value})";
    }

    /// <summary>
    /// Base class for all internal type information, parsed from the bitfile.
    /// Mimics the internal _BaseType hierarchy from the Python library.
    /// </summary>
    internal abstract class FpgaTypeInfo
    {
        public string Name { get; }
        public abstract int SizeInBits { get; }
        public abstract Type PublicType { get; }

        protected FpgaTypeInfo(string name) { Name = name; }

        public abstract object Unpack(BitReader reader);
        public abstract void Pack(object value, BitWriter writer);
    }

    /// <summary>
    /// Type info for primitive types (bool, int, uint, etc.)
    /// </summary>
    internal class PrimitiveTypeInfo : FpgaTypeInfo
    {
        public override int SizeInBits { get; }
        public override Type PublicType { get; }
        public bool IsSigned { get; }
        public bool IsFloat { get; }

        public PrimitiveTypeInfo(string name, Type publicType, int sizeInBits, bool isSigned, bool isFloat = false) : base(name)
        {
            PublicType = publicType;
            SizeInBits = sizeInBits;
            IsSigned = isSigned;
            IsFloat = isFloat;
        }

        public override object Unpack(BitReader reader)
        {
            if (PublicType == typeof(bool)) return reader.ReadBool();
            if (IsFloat)
            {
                if (SizeInBits == 32) return reader.ReadFloat32();
                if (SizeInBits == 64) return reader.ReadFloat64();
            }
            if (IsSigned) return (object)reader.ReadInt(SizeInBits);
            return (object)reader.ReadUInt(SizeInBits);
        }

        public override void Pack(object value, BitWriter writer)
        {
            if (PublicType == typeof(bool)) writer.WriteBool((bool)value);
            else if (IsFloat)
            {
                if (SizeInBits == 32) writer.WriteFloat32((float)Convert.ChangeType(value, typeof(float)));
                else if (SizeInBits == 64) writer.WriteFloat64((double)Convert.ChangeType(value, typeof(double)));
            }
            else if (IsSigned) writer.WriteInt(Convert.ToInt64(value), SizeInBits);
            else writer.WriteUInt(Convert.ToUInt64(value), SizeInBits);
        }
    }

    /// <summary>
    /// Type info for Fixed Point (FXP) types.
    ///
    /// </summary>
    internal class FxpTypeInfo : FpgaTypeInfo
    {
        public bool IsSigned { get; }
        public int WordLength { get; }
        public int IntegerWordLength { get; }
        public bool HasOverflow { get; }
        public decimal Delta { get; }
        public decimal MaxValue { get; }
        public decimal MinValue { get; }

        public override int SizeInBits => WordLength + (HasOverflow ? 1 : 0);
        public override Type PublicType => HasOverflow ? typeof(FxpValueWithOverflow) : typeof(FxpValue);

        public FxpTypeInfo(string name, XElement xml) : base(name)
        {
            IsSigned = xml.Element("Signed")?.Value.ToLower() == "true";
            WordLength = int.Parse(xml.Element("WordLength")!.Value); // FIX: Added null-forgiving operator
            IntegerWordLength = int.Parse(xml.Element("IntegerWordLength")!.Value); // FIX: Added null-forgiving operator
            HasOverflow = xml.Element("IncludeOverflowStatus")?.Value.ToLower() == "true";

            // Calculate Delta, Min, Max as in the Python _FXP class
            Delta = (decimal)BigInteger.Pow(2, IntegerWordLength - WordLength);
            if (IsSigned)
            {
                var magnitudeBits = WordLength - 1;
                MinValue = -1 * (decimal)BigInteger.Pow(2, magnitudeBits) * Delta;
                MaxValue = ((decimal)BigInteger.Pow(2, magnitudeBits) - 1) * Delta;
            }
            else
            {
                MinValue = 0;
                MaxValue = ((decimal)BigInteger.Pow(2, WordLength) - 1) * Delta;
            }
        }

        public override object Unpack(BitReader reader)
        {
            bool overflow = false;
            if (HasOverflow)
            {
                overflow = reader.ReadBool();
            }
            decimal value = reader.ReadFxp(WordLength, IsSigned, Delta);

            if (HasOverflow)
                return new FxpValueWithOverflow(overflow, value);
            else
                return new FxpValue(value);
        }

        public override void Pack(object value, BitWriter writer)
        {
            bool overflow = false;
            decimal decimalValue;

            if (HasOverflow)
            {
                if (value is FxpValueWithOverflow fxp)
                {
                    (overflow, decimalValue) = fxp;
                }
                else
                {
                    // Allow writing a raw decimal, assuming overflow is false
                    decimalValue = Convert.ToDecimal(value);
                }
                writer.WriteBool(overflow);
            }
            else
            {
                if (value is FxpValue fxp)
                {
                    decimalValue = fxp.Value;
                }
                else
                {
                    decimalValue = Convert.ToDecimal(value);
                }
            }
            
            // Note: Add coercion warning logic here if desired
            if (decimalValue < MinValue) decimalValue = MinValue;
            if (decimalValue > MaxValue) decimalValue = MaxValue;
            
            writer.WriteFxp(decimalValue, WordLength, IsSigned, Delta);
        }
    }

    /// <summary>
    /// Type info for Array types.
    ///
    /// </summary>
    internal class ArrayTypeInfo : FpgaTypeInfo
    {
        public FpgaTypeInfo ElementType { get; }
        public int Size { get; }

        public override int SizeInBits => ElementType.SizeInBits * Size;
        public override Type PublicType => ElementType.PublicType.MakeArrayType();

        public ArrayTypeInfo(string name, XElement xml, Func<XElement, FpgaTypeInfo> typeParser) : base(name)
        {
            Size = int.Parse(xml.Element("Size")!.Value); // FIX: Added null-forgiving operator
            ElementType = typeParser(xml.Element("Type")!.Elements().First()); // FIX: Added null-forgiving operator
        }

        public override object Unpack(BitReader reader)
        {
            var array = Array.CreateInstance(ElementType.PublicType, Size);
            for (int i = 0; i < Size; i++)
            {
                array.SetValue(ElementType.Unpack(reader), i);
            }
            
            // *** FIX 1: Arrays are packed LSB-first, so elements are read in reverse order.
            Array.Reverse(array);
            
            return array;
        }

        public override void Pack(object value, BitWriter writer)
        {
            var array = (Array)value;
            if (array.Length != Size)
                throw new ArgumentException($"Array length mismatch for '{Name}'. Expected {Size}, got {array.Length}.");
            
            // *** FIX 1 (companion): Write in reverse order to match Python's forward-order packing
            for (int i = Size - 1; i >= 0; i--)
            {
                // FIX: Check for null element
                object? elementValue = array.GetValue(i);
                if (elementValue == null)
                    throw new ArgumentNullException(nameof(value), $"Array element at index {i} is null.");
                ElementType.Pack(elementValue, writer);
            }
        }
    }

    /// <summary>
    /// Type info for Cluster types.
    ///
    /// </summary>
    internal class ClusterTypeInfo : FpgaTypeInfo
    {
        public List<(string Name, FpgaTypeInfo Type)> Elements { get; }

        public override int SizeInBits => Elements.Sum(e => e.Type.SizeInBits);
        public override Type PublicType => typeof(Dictionary<string, object>);

        public ClusterTypeInfo(string name, XElement xml, Func<XElement, FpgaTypeInfo> typeParser) : base(name)
        {
            Elements = new List<(string, FpgaTypeInfo)>();
            var names = new HashSet<string>();
            foreach (var elementXml in xml.Element("TypeList")!.Elements()) // FIX: Added null-forgiving operator
            {
                var elementTypeInfo = typeParser(elementXml);
                // Allow empty names (from strings in error clusters) but still check for real duplicates
                if (!string.IsNullOrEmpty(elementTypeInfo.Name))
                {
                    if (!names.Add(elementTypeInfo.Name))
                    {
                        throw new NotSupportedException($"Cluster '{name}' contains duplicate element name '{elementTypeInfo.Name}'.");
                    }
                }
                Elements.Add((elementTypeInfo.Name, elementTypeInfo));
            }
        }

        public override object Unpack(BitReader reader)
        {
            // Python packs LSB first, so we read in reverse order of definition.
            var dict = new Dictionary<string, object>();
            var tempValues = new object[Elements.Count];
            for(int i = Elements.Count - 1; i >= 0; i--)
            {
                tempValues[i] = Elements[i].Type.Unpack(reader);
            }

            for(int i = 0; i < Elements.Count; i++)
            {
                // Don't add elements with no name (e.g., StringTypeInfo)
                if (!string.IsNullOrEmpty(Elements[i].Name))
                {
                    dict.Add(Elements[i].Name, tempValues[i]);
                }
            }
            return dict;
        }

        public override void Pack(object value, BitWriter writer)
        {
            // FIX: Check for null dictionary
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            var dict = (Dictionary<string, object>)value;
            // Python packs LSB first, so we write in order of definition.
            foreach (var (name, type) in Elements)
            {
                // Skip types with no name (e.g. String)
                if (string.IsNullOrEmpty(name))
                {
                    type.Pack("", writer); // Pack default/empty
                    continue;
                }

                if (!dict.TryGetValue(name, out var elementValue))
                {
                    throw new KeyNotFoundException($"Cluster dictionary is missing key '{name}'.");
                }
                type.Pack(elementValue, writer);
            }
        }
    }
    
    // *** FIX 2: Add StringTypeInfo stub class ***
    /// <summary>
    /// Stub type for Strings (which appear in error clusters)
    /// </summary>
    internal class StringTypeInfo : FpgaTypeInfo
    {
        public StringTypeInfo(string name) : base(name) { }
        public override int SizeInBits => 0; // Strings are not supported and don't take bitfile space
        public override Type PublicType => typeof(string);
        public override object Unpack(BitReader reader) => "";
        public override void Pack(object value, BitWriter writer) { /* Do nothing */ }
    }
}