using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FpgaInterface
{
    /// <summary>
    /// Holds information about a single register, parsed from the bitfile.
    /// </summary>
    internal class RegisterInfo
    {
        public string Name { get; }
        public uint Offset { get; }
        public FpgaTypeInfo TypeInfo { get; }
        public bool IsIndicator { get; }
        public bool AccessMayTimeout { get; }

        public RegisterInfo(string name, uint offset, FpgaTypeInfo typeInfo, bool isIndicator, bool accessMayTimeout)
        {
            Name = name;
            Offset = offset;
            TypeInfo = typeInfo;
            IsIndicator = isIndicator;
            AccessMayTimeout = accessMayTimeout;
        }
    }

    /// <summary>
    /// Holds information about a single FIFO, parsed from the bitfile.
    /// </summary>
    internal class FifoInfo
    {
        public string Name { get; }
        public uint Number { get; }
        public FpgaTypeInfo TypeInfo { get; }
        public int TransferSizeBytes { get; }

        public FifoInfo(string name, uint number, FpgaTypeInfo typeInfo, int transferSizeBytes)
        {
            Name = name;
            Number = number;
            TypeInfo = typeInfo;
            TransferSizeBytes = transferSizeBytes;
        }
    }

    /// <summary>
    /// Internal class to parse and represent the contents of an .lvbitx file.
    /// This mimics the functionality of the Python nifpga.bitfile.Bitfile class.
    ///
    /// </summary>
    internal class Bitfile
    {
        public string FilePath { get; }
        public string Signature { get; }
        public uint BaseAddressOnDevice { get; }
        public IReadOnlyDictionary<string, RegisterInfo> Registers { get; }
        public IReadOnlyDictionary<string, FifoInfo> Fifos { get; }

        public Bitfile(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException("Bitfile not found.", filepath);
            }
            FilePath = Path.GetFullPath(filepath);

            XDocument doc = XDocument.Load(FilePath);
            var registers = new Dictionary<string, RegisterInfo>();
            var fifos = new Dictionary<string, FifoInfo>();

            // Parse Signature
            // FIX: Added null-conditional and null-forgiving operators
            Signature = doc.Root?.Element("SignatureRegister")?.Value.ToUpper() ?? throw new InvalidDataException("Bitfile missing SignatureRegister.");

            // Parse Base Address
            // FIX: Added null-conditional and null-forgiving operators
            BaseAddressOnDevice = uint.Parse(doc.Root?
                .Element("Project")?
                .Element("CompilationResultsTree")?
                .Element("CompilationResults")?
                .Element("NiFpga")?
                .Element("BaseAddressOnDevice")?.Value ?? "0");

            // Parse Registers
            // FIX: Added null-conditional and null-forgiving operators
            var registerList = doc.Root?.Element("VI")?.Element("RegisterList")?.Elements("Register");
            if (registerList != null)
            {
                foreach (var regXml in registerList)
                {
                    try
                    {
                        string name = regXml.Element("Name")!.Value;
                        uint offset = uint.Parse(regXml.Element("Offset")!.Value);
                        bool isIndicator = regXml.Element("Indicator")!.Value.ToLower() == "true";
                        bool isInternal = regXml.Element("Internal")!.Value.ToLower() == "true";
                        bool accessMayTimeout = regXml.Element("AccessMayTimeout")!.Value.ToLower() == "true";

                        if (isInternal) continue; // Skip internal registers

                        XElement typeElement = regXml.Element("Datatype")!.Elements().First();
                        FpgaTypeInfo typeInfo = ParseTypeElement(typeElement);

                        var regInfo = new RegisterInfo(name, offset, typeInfo, isIndicator, accessMayTimeout);
                        if (!registers.ContainsKey(name))
                        {
                            registers.Add(name, regInfo);
                        }
                    }
                    catch (Exception ex) when (ex is NotSupportedException || ex is NullReferenceException)
                    {
                        // Log warning: Skipping unsupported register type
                        Console.WriteLine($"Warning: Skipping register '{regXml.Element("Name")?.Value}': {ex.Message}");
                    }
                }
            }

            // Parse FIFOs (DMA Channels)
            // FIX: Added null-conditional and null-forgiving operators
            var fifoList = doc.Root?
                .Element("Project")?
                .Element("CompilationResultsTree")?
                .Element("CompilationResults")?
                .Element("NiFpga")?
                .Element("DmaChannelAllocationList")?.Elements("Channel");

            if (fifoList != null)
            {
                foreach (var channelXml in fifoList)
                {
                    try
                    {
                        string name = channelXml.Attribute("name")!.Value;
                        uint number = uint.Parse(channelXml.Element("Number")!.Value);
                    
                        // TransferSizeBytes exists for composite types, but not always for simple types
                        var transferSizeEl = channelXml.Element("TransferSizeBytes");
                        int transferSize;
                    
                        XElement typeElement = channelXml.Element("DataType")!.Elements().First();
                        FpgaTypeInfo typeInfo = ParseTypeElement(typeElement);

                        if(transferSizeEl != null)
                        {
                            transferSize = int.Parse(transferSizeEl.Value);
                        }
                        else
                        {
                            // Infer size for simple types
                            transferSize = (typeInfo.SizeInBits + 7) / 8;
                            // FXP defaults to 8 bytes (U64) if not specified
                            if(typeInfo is FxpTypeInfo) transferSize = 8;
                        }

                        var fifoInfo = new FifoInfo(name, number, typeInfo, transferSize);
                        if (!fifos.ContainsKey(name))
                        {
                            fifos.Add(name, fifoInfo);
                        }
                    }
                    catch (Exception ex) when (ex is NotSupportedException || ex is NullReferenceException)
                    {
                        Console.WriteLine($"Warning: Skipping FIFO '{channelXml.Attribute("name")?.Value}': {ex.Message}");
                    }
                }
            }

            Registers = registers;
            Fifos = fifos;
        }

        /// <summary>
        /// Recursively parses an XML type definition into an FpgaTypeInfo object.
        ///
        /// </summary>
        private FpgaTypeInfo ParseTypeElement(XElement typeElement)
        {
            string typeName = typeElement.Name.LocalName;
            string name = typeElement.Element("Name")?.Value ?? "";

            switch (typeName)
            {
                // Primitives
                case "Boolean": return new PrimitiveTypeInfo(name, typeof(bool), 1, false);
                case "I8": return new PrimitiveTypeInfo(name, typeof(sbyte), 8, true);
                case "U8": return new PrimitiveTypeInfo(name, typeof(byte), 8, false);
                case "I16": return new PrimitiveTypeInfo(name, typeof(short), 16, true);
                case "U16": return new PrimitiveTypeInfo(name, typeof(ushort), 16, false);
                case "I32": return new PrimitiveTypeInfo(name, typeof(int), 32, true);
                case "U32": return new PrimitiveTypeInfo(name, typeof(uint), 32, false);
                case "I64": return new PrimitiveTypeInfo(name, typeof(long), 64, true);
                case "U64": return new PrimitiveTypeInfo(name, typeof(ulong), 64, false);
                case "SGL": return new PrimitiveTypeInfo(name, typeof(float), 32, true, true);
                case "DBL": return new PrimitiveTypeInfo(name, typeof(double), 64, true, true);
                
                // Enums (treated as their underlying integer)
                case "EnumU8": return new PrimitiveTypeInfo(name, typeof(byte), 8, false);
                case "EnumI8": return new PrimitiveTypeInfo(name, typeof(sbyte), 8, true);
                case "EnumU16": return new PrimitiveTypeInfo(name, typeof(ushort), 16, false);
                case "EnumI16": return new PrimitiveTypeInfo(name, typeof(short), 16, true);
                case "EnumU32": return new PrimitiveTypeInfo(name, typeof(uint), 32, false);
                case "EnumI32": return new PrimitiveTypeInfo(name, typeof(int), 32, true);

                // Complex Types
                case "FXP":
                    return new FxpTypeInfo(name, typeElement);
                case "Array":
                    return new ArrayTypeInfo(name, typeElement, ParseTypeElement);
                case "Cluster":
                    return new ClusterTypeInfo(name, typeElement, ParseTypeElement);
                
                // *** FIX: Gracefully handle String type ***
                case "String":
                    return new StringTypeInfo(name); 
                
                // Unsupported
                case "CFXP":
                    throw new NotSupportedException("Complex Fixed Point (CFXP) is not supported."); //
                
                default:
                    throw new NotSupportedException($"Unknown data type tag: {typeName}");
            }
        }
    }
}