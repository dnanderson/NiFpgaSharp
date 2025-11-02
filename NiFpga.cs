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

        // Register operations
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadBool")]
        public static extern int ReadBool(uint session, uint indicator, out byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteBool")]
        public static extern int WriteBool(uint session, uint control, byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU8")]
        public static extern int ReadU8(uint session, uint indicator, out byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU8")]
        public static extern int WriteU8(uint session, uint control, byte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU16")]
        public static extern int ReadU16(uint session, uint indicator, out ushort value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU16")]
        public static extern int WriteU16(uint session, uint control, ushort value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU32")]
        public static extern int ReadU32(uint session, uint indicator, out uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU32")]
        public static extern int WriteU32(uint session, uint control, uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadU64")]
        public static extern int ReadU64(uint session, uint indicator, out ulong value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteU64")]
        public static extern int WriteU64(uint session, uint control, ulong value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI8")]
        public static extern int ReadI8(uint session, uint indicator, out sbyte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI8")]
        public static extern int WriteI8(uint session, uint control, sbyte value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI16")]
        public static extern int ReadI16(uint session, uint indicator, out short value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI16")]
        public static extern int WriteI16(uint session, uint control, short value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI32")]
        public static extern int ReadI32(uint session, uint indicator, out int value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI32")]
        public static extern int WriteI32(uint session, uint control, int value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadI64")]
        public static extern int ReadI64(uint session, uint indicator, out long value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteI64")]
        public static extern int WriteI64(uint session, uint control, long value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadSgl")]
        public static extern int ReadSgl(uint session, uint indicator, out float value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteSgl")]
        public static extern int WriteSgl(uint session, uint control, float value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadDbl")]
        public static extern int ReadDbl(uint session, uint indicator, out double value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteDbl")]
        public static extern int WriteDbl(uint session, uint control, double value);

        // Array operations
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU8")]
        public static extern int ReadArrayU8(uint session, uint indicator, byte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU8")]
        public static extern int WriteArrayU8(uint session, uint control, byte[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU16")]
        public static extern int ReadArrayU16(uint session, uint indicator, ushort[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU16")]
        public static extern int WriteArrayU16(uint session, uint control, ushort[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadArrayU32")]
        public static extern int ReadArrayU32(uint session, uint indicator, uint[] array, UIntPtr size);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteArrayU32")]
        public static extern int WriteArrayU32(uint session, uint control, uint[] array, UIntPtr size);

        // FIFO operations
        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ConfigureFifo2")]
        public static extern int ConfigureFifo2(uint session, uint fifo, UIntPtr requestedDepth, out UIntPtr actualDepth);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_StartFifo")]
        public static extern int StartFifo(uint session, uint fifo);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_StopFifo")]
        public static extern int StopFifo(uint session, uint fifo);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_ReadFifoU32")]
        public static extern int ReadFifoU32(
            uint session,
            uint fifo,
            uint[] data,
            UIntPtr numberOfElements,
            uint timeoutMs,
            out UIntPtr elementsRemaining);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_WriteFifoU32")]
        public static extern int WriteFifoU32(
            uint session,
            uint fifo,
            uint[] data,
            UIntPtr numberOfElements,
            uint timeoutMs,
            out UIntPtr emptyElementsRemaining);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_GetFifoPropertyU32")]
        public static extern int GetFifoPropertyU32(
            uint session,
            uint fifo,
            uint property,
            out uint value);

        [DllImport(LibraryName, EntryPoint = "NiFpgaDll_SetFifoPropertyU32")]
        public static extern int SetFifoPropertyU32(
            uint session,
            uint fifo,
            uint property,
            uint value);

        // IRQ operations
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