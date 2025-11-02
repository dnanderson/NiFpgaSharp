using System;
using System.Runtime.InteropServices;

namespace FpgaInterface
{
    /// <summary>
    /// Data types supported by FPGA registers and FIFOs
    /// </summary>
    public enum DataType
    {
        Bool = 1,
        I8 = 2,
        U8 = 3,
        I16 = 4,
        U16 = 5,
        I32 = 6,
        U32 = 7,
        I64 = 8,
        U64 = 9,
        Sgl = 10,
        Dbl = 11,
        Fxp = 12,
        Cluster = 13
    }

    /// <summary>
    /// FIFO flow control modes
    /// </summary>
    public enum FlowControl
    {
        /// <summary>
        /// Disable flow control - FIFO will overwrite data when full
        /// </summary>
        Disabled = 1,
        
        /// <summary>
        /// Enable flow control - no data is lost, transfers only when space available
        /// </summary>
        Enabled = 2
    }

    /// <summary>
    /// DMA buffer allocation type
    /// </summary>
    public enum DmaBufferType
    {
        /// <summary>
        /// Buffer allocated by the driver
        /// </summary>
        AllocatedByDriver = 1,
        
        /// <summary>
        /// Buffer allocated by the user
        /// </summary>
        AllocatedByUser = 2
    }

    /// <summary>
    /// FPGA VI execution state
    /// </summary>
    public enum FpgaViState
    {
        /// <summary>
        /// VI has been downloaded but not run, or was aborted/reset
        /// </summary>
        NotRunning = 0,
        
        /// <summary>
        /// An error has occurred
        /// </summary>
        Invalid = 1,
        
        /// <summary>
        /// VI is currently executing
        /// </summary>
        Running = 2,
        
        /// <summary>
        /// VI stopped normally
        /// </summary>
        NaturallyStopped = 3
    }

    /// <summary>
    /// FIFO property types
    /// </summary>
    public enum FifoProperty
    {
        BytesPerElement = 1,
        BufferAllocationGranularityElements = 2,
        BufferSizeElements = 3,
        MirroredElements = 4,
        DmaBufferType = 5,
        DmaBuffer = 6,
        FlowControl = 7,
        ElementsCurrentlyAcquired = 8,
        PreferredNumaNode = 9
    }

    /// <summary>
    /// Native P/Invoke declarations for the NiFpga C library
    /// </summary>
    internal static class NativeMethods
    {
        private const string LibraryName = "NiFpga";
        
        public const uint OpenAttributeNoRun = 1;
        public const uint OpenAttributeBitfilePathIsUtf8 = 2;
        public const uint RunAttributeWaitUntilDone = 1;
        public const uint CloseAttributeNoResetIfLastSession = 1;
        public const uint InfiniteTimeout = 0xFFFFFFFF;

        //=================================================================
        // Session Management
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Open")]
        public static extern int Open(
            [MarshalAs(UnmanagedType.LPStr)] string bitfilePath,
            [MarshalAs(UnmanagedType.LPStr)] string signature,
            [MarshalAs(UnmanagedType.LPStr)] string resource,
            uint attribute,
            out uint session);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Close")]
        public static extern int Close(uint session, uint attribute);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Run")]
        public static extern int Run(uint session, uint attribute);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Abort")]
        public static extern int Abort(uint session);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Reset")]
        public static extern int Reset(uint session);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_Download")]
        public static extern int Download(uint session);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFpgaViState")]
        public static extern int GetFpgaViState(uint session, out uint state);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ClientFunctionCall")]
        public static extern int ClientFunctionCall(uint session, uint group, uint functionId, IntPtr inBuffer, UIntPtr inBufferSize, IntPtr outBuffer, UIntPtr outBufferSize);

        //=================================================================
        // Resource Management
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_OpenResource")]
        public static extern int OpenResource(uint parentSession, uint parentIndex, uint globalIndex, out uint childSession);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AddResources")]
        public static extern int AddResources(uint session,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] resourceNames,
            uint[] resourceValues,
            uint[] externalRegisters,
            UIntPtr numberOfResources);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetResourceIndex")]
        public static extern int GetResourceIndex([MarshalAs(UnmanagedType.LPStr)] string resourceName, out uint resourceIndex);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReleaseResourceIndex")]
        public static extern int ReleaseResourceIndex([MarshalAs(UnmanagedType.LPStr)] string resourceName);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetResourceName")]
        public static extern int GetResourceName(uint resourceIndex, out IntPtr resourceName); // Caller must free the returned char*

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_FindRegisterPrivate")]
        public static extern int FindRegisterPrivate(uint session, [MarshalAs(UnmanagedType.LPStr)] string name, uint expectedType, out uint offset);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_FindFifoPrivate")]
        public static extern int FindFifoPrivate(uint session, [MarshalAs(UnmanagedType.LPStr)] string name, uint expectedType, out uint fifoNumber);

        //=================================================================
        // Register operations (Scalar)
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadBool")]
        public static extern int ReadBool(uint session, uint indicator, out byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteBool")]
        public static extern int WriteBool(uint session, uint control, byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI8")]
        public static extern int ReadI8(uint session, uint indicator, out sbyte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI8")]
        public static extern int WriteI8(uint session, uint control, sbyte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU8")]
        public static extern int ReadU8(uint session, uint indicator, out byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU8")]
        public static extern int WriteU8(uint session, uint control, byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI16")]
        public static extern int ReadI16(uint session, uint indicator, out short value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI16")]
        public static extern int WriteI16(uint session, uint control, short value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU16")]
        public static extern int ReadU16(uint session, uint indicator, out ushort value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU16")]
        public static extern int WriteU16(uint session, uint control, ushort value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI32")]
        public static extern int ReadI32(uint session, uint indicator, out int value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI32")]
        public static extern int WriteI32(uint session, uint control, int value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU32")]
        public static extern int ReadU32(uint session, uint indicator, out uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU32")]
        public static extern int WriteU32(uint session, uint control, uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI64")]
        public static extern int ReadI64(uint session, uint indicator, out long value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI64")]
        public static extern int WriteI64(uint session, uint control, long value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU64")]
        public static extern int ReadU64(uint session, uint indicator, out ulong value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU64")]
        public static extern int WriteU64(uint session, uint control, ulong value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadSgl")]
        public static extern int ReadSgl(uint session, uint indicator, out float value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteSgl")]
        public static extern int WriteSgl(uint session, uint control, float value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadDbl")]
        public static extern int ReadDbl(uint session, uint indicator, out double value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteDbl")]
        public static extern int WriteDbl(uint session, uint control, double value);

        //=================================================================
        // Register operations (Array)
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayBool")]
        public static extern int ReadArrayBool(uint session, uint indicator, [Out] byte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayBool")]
        public static extern int WriteArrayBool(uint session, uint control, [In] byte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayI8")]
        public static extern int ReadArrayI8(uint session, uint indicator, [Out] sbyte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayI8")]
        public static extern int WriteArrayI8(uint session, uint control, [In] sbyte[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU8")]
        public static extern int ReadArrayU8(uint session, uint indicator, [Out] byte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU8")]
        public static extern int WriteArrayU8(uint session, uint control, [In] byte[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayI16")]
        public static extern int ReadArrayI16(uint session, uint indicator, [Out] short[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayI16")]
        public static extern int WriteArrayI16(uint session, uint control, [In] short[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU16")]
        public static extern int ReadArrayU16(uint session, uint indicator, [Out] ushort[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU16")]
        public static extern int WriteArrayU16(uint session, uint control, [In] ushort[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayI32")]
        public static extern int ReadArrayI32(uint session, uint indicator, [Out] int[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayI32")]
        public static extern int WriteArrayI32(uint session, uint control, [In] int[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU32")]
        public static extern int ReadArrayU32(uint session, uint indicator, [Out] uint[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU32")]
        public static extern int WriteArrayU32(uint session, uint control, [In] uint[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayI64")]
        public static extern int ReadArrayI64(uint session, uint indicator, [Out] long[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayI64")]
        public static extern int WriteArrayI64(uint session, uint control, [In] long[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU64")]
        public static extern int ReadArrayU64(uint session, uint indicator, [Out] ulong[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU64")]
        public static extern int WriteArrayU64(uint session, uint control, [In] ulong[] array, UIntPtr size);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArraySgl")]
        public static extern int ReadArraySgl(uint session, uint indicator, [Out] float[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArraySgl")]
        public static extern int WriteArraySgl(uint session, uint control, [In] float[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayDbl")]
        public static extern int ReadArrayDbl(uint session, uint indicator, [Out] double[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayDbl")]
        public static extern int WriteArrayDbl(uint session, uint control, [In] double[] array, UIntPtr size);

        //=================================================================
        // FIFO operations - Control
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ConfigureFifo")]
        public static extern int ConfigureFifo(uint session, uint fifo, UIntPtr depth);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ConfigureFifo2")]
        public static extern int ConfigureFifo2(uint session, uint fifo, UIntPtr requestedDepth, out UIntPtr actualDepth);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_StartFifo")]
        public static extern int StartFifo(uint session, uint fifo);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_StopFifo")]
        public static extern int StopFifo(uint session, uint fifo);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_UnreserveFifo")]
        public static extern int UnreserveFifo(uint session, uint fifo);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReleaseFifoElements")]
        public static extern int ReleaseFifoElements(uint session, uint fifo, UIntPtr elements);
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetPeerToPeerFifoEndpoint")]
        public static extern int GetPeerToPeerFifoEndpoint(uint session, uint fifo, out uint endpoint);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_CommitFifoConfiguration")]
        public static extern int CommitFifoConfiguration(uint session, uint fifo);

        //=================================================================
        // FIFO operations - Read/Write
        //=================================================================

        // Bool
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoBool")]
        public static extern int ReadFifoBool(uint session, uint fifo, [Out] byte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoBool")]
        public static extern int WriteFifoBool(uint session, uint fifo, [In] byte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // I8
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoI8")]
        public static extern int ReadFifoI8(uint session, uint fifo, [Out] sbyte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoI8")]
        public static extern int WriteFifoI8(uint session, uint fifo, [In] sbyte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // U8
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoU8")]
        public static extern int ReadFifoU8(uint session, uint fifo, [Out] byte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoU8")]
        public static extern int WriteFifoU8(uint session, uint fifo, [In] byte[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // I16
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoI16")]
        public static extern int ReadFifoI16(uint session, uint fifo, [Out] short[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoI16")]
        public static extern int WriteFifoI16(uint session, uint fifo, [In] short[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // U16
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoU16")]
        public static extern int ReadFifoU16(uint session, uint fifo, [Out] ushort[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoU16")]
        public static extern int WriteFifoU16(uint session, uint fifo, [In] ushort[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);
        
        // I32
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoI32")]
        public static extern int ReadFifoI32(uint session, uint fifo, [Out] int[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoI32")]
        public static extern int WriteFifoI32(uint session, uint fifo, [In] int[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // U32
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoU32")]
        public static extern int ReadFifoU32(uint session, uint fifo, [Out] uint[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoU32")]
        public static extern int WriteFifoU32(uint session, uint fifo, [In] uint[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // I64
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoI64")]
        public static extern int ReadFifoI64(uint session, uint fifo, [Out] long[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoI64")]
        public static extern int WriteFifoI64(uint session, uint fifo, [In] long[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // U64
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoU64")]
        public static extern int ReadFifoU64(uint session, uint fifo, [Out] ulong[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoU64")]
        public static extern int WriteFifoU64(uint session, uint fifo, [In] ulong[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // Sgl
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoSgl")]
        public static extern int ReadFifoSgl(uint session, uint fifo, [Out] float[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoSgl")]
        public static extern int WriteFifoSgl(uint session, uint fifo, [In] float[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);

        // Dbl
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoDbl")]
        public static extern int ReadFifoDbl(uint session, uint fifo, [Out] double[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoDbl")]
        public static extern int WriteFifoDbl(uint session, uint fifo, [In] double[] data, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);
        
        // Composite
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoComposite")]
        public static extern int ReadFifoComposite(uint session, uint fifo, [Out] byte[] data, uint bytesPerElement, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoComposite")]
        public static extern int WriteFifoComposite(uint session, uint fifo, [In] byte[] data, uint bytesPerElement, UIntPtr numberOfElements, uint timeoutMs, out UIntPtr emptyElementsRemaining);


        //=================================================================
        // FIFO operations - Acquire Elements
        //=================================================================

        // Bool
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsBool")]
        public static extern int AcquireFifoReadElementsBool(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsBool")]
        public static extern int AcquireFifoWriteElementsBool(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // I8
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsI8")]
        public static extern int AcquireFifoReadElementsI8(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsI8")]
        public static extern int AcquireFifoWriteElementsI8(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // U8
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsU8")]
        public static extern int AcquireFifoReadElementsU8(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsU8")]
        public static extern int AcquireFifoWriteElementsU8(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // I16
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsI16")]
        public static extern int AcquireFifoReadElementsI16(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsI16")]
        public static extern int AcquireFifoWriteElementsI16(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // U16
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsU16")]
        public static extern int AcquireFifoReadElementsU16(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsU16")]
        public static extern int AcquireFifoWriteElementsU16(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        
        // I32
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsI32")]
        public static extern int AcquireFifoReadElementsI32(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsI32")]
        public static extern int AcquireFifoWriteElementsI32(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // U32
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsU32")]
        public static extern int AcquireFifoReadElementsU32(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsU32")]
        public static extern int AcquireFifoWriteElementsU32(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // I64
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsI64")]
        public static extern int AcquireFifoReadElementsI64(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsI64")]
        public static extern int AcquireFifoWriteElementsI64(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // U64
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsU64")]
        public static extern int AcquireFifoReadElementsU64(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsU64")]
        public static extern int AcquireFifoWriteElementsU64(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // Sgl
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsSgl")]
        public static extern int AcquireFifoReadElementsSgl(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsSgl")]
        public static extern int AcquireFifoWriteElementsSgl(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // Dbl
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsDbl")]
        public static extern int AcquireFifoReadElementsDbl(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsDbl")]
        public static extern int AcquireFifoWriteElementsDbl(uint session, uint fifo, out IntPtr elements, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        // Composite
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadElementsComposite")]
        public static extern int AcquireFifoReadElementsComposite(uint session, uint fifo, out IntPtr elements, uint bytesPerElement, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteElementsComposite")]
        public static extern int AcquireFifoWriteElementsComposite(uint session, uint fifo, out IntPtr elements, uint bytesPerElement, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);
        
        //=================================================================
        // FIFO operations - Acquire Region
        //=================================================================
        
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoReadRegion")]
        public static extern int AcquireFifoReadRegion(uint session, uint fifo, out IntPtr region, out IntPtr elements, 
            [MarshalAs(UnmanagedType.U1)] bool isSigned, 
            uint bytesPerElement, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcquireFifoWriteRegion")]
        public static extern int AcquireFifoWriteRegion(uint session, uint fifo, out IntPtr region, out IntPtr elements, 
            [MarshalAs(UnmanagedType.U1)] bool isSigned, 
            uint bytesPerElement, UIntPtr elementsRequested, uint timeoutMs, out UIntPtr elementsAcquired, out UIntPtr elementsRemaining);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReleaseFifoRegion")]
        public static extern int ReleaseFifoRegion(uint session, uint fifo, IntPtr region);

        //=================================================================
        // FIFO operations - Properties
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyI32")]
        public static extern int GetFifoPropertyI32(uint session, uint fifo, uint property, out int value);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyI32")]
        public static extern int SetFifoPropertyI32(uint session, uint fifo, uint property, int value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyU32")]
        public static extern int GetFifoPropertyU32(uint session, uint fifo, uint property, out uint value);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyU32")]
        public static extern int SetFifoPropertyU32(uint session, uint fifo, uint property, uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyI64")]
        public static extern int GetFifoPropertyI64(uint session, uint fifo, uint property, out long value);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyI64")]
        public static extern int SetFifoPropertyI64(uint session, uint fifo, uint property, long value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyU64")]
        public static extern int GetFifoPropertyU64(uint session, uint fifo, uint property, out ulong value);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyU64")]
        public static extern int SetFifoPropertyU64(uint session, uint fifo, uint property, ulong value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyPtr")]
        public static extern int GetFifoPropertyPtr(uint session, uint fifo, uint property, out IntPtr value);
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyPtr")]
        public static extern int SetFifoPropertyPtr(uint session, uint fifo, uint property, IntPtr value);

        //=================================================================
        // IRQ operations
        //=================================================================
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReserveIrqContext")]
        public static extern int ReserveIrqContext(uint session, out IntPtr context);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_UnreserveIrqContext")]
        public static extern int UnreserveIrqContext(uint session, IntPtr context);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WaitOnIrqs")]
        public static extern int WaitOnIrqs(
            uint session,
            IntPtr context,
            uint irqs,
            uint timeoutMs,
            out uint irqsAsserted,
            out byte timedOut);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_AcknowledgeIrqs")]
        public static extern int AcknowledgeIrqs(uint session, uint irqs);
    }
}