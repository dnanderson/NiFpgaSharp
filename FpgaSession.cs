using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FpgaInterface
{
    /// <summary>
    /// Represents an FPGA session, providing access to registers, FIFOs, and session controls.
    /// This class must be disposed (e.g., with a 'using' block) to release the native session handle.
    /// </summary>
    public class FpgaSession : IDisposable
    {
        private uint _sessionHandle;
        private bool _disposed = false;
        private readonly Bitfile _bitfile;

        // Caches for the register/FIFO wrapper objects
        private readonly Dictionary<string, FpgaRegister> _registers = new();
        private readonly Dictionary<string, FpgaFifo> _fifos = new();
        private readonly object _lock = new object();


        /// <summary>
        /// Gets the native session handle.
        /// </summary>
        internal uint Handle => _sessionHandle;

        /// <summary>
        /// Gets the current execution state of the FPGA VI.
        /// </summary>
        public FpgaViState State
        {
            get
            {
                CheckHandle();
                StatusChecker.CheckStatus(NativeMethods.GetFpgaViState(_sessionHandle, out uint state), nameof(NativeMethods.GetFpgaViState));
                return (FpgaViState)state;
            }
        }
        
        /// <summary>
        /// Opens a new session to an FPGA resource and programs it with the specified bitfile.
        /// </summary>
        /// <param name="bitfilePath">The file path to the .lvbitx bitfile.</param>
        /// <param name="resourceName">The RIO resource name (e.g., "RIO0").</param>
        /// <param name="noRun">If true, opens the session without running the FPGA VI.</param>
        public FpgaSession(string bitfilePath, string resourceName, bool noRun = false)
        {
            _bitfile = new Bitfile(bitfilePath);

            uint attribute = 0;
            if (noRun)
            {
                attribute |= NativeMethods.OpenAttributeNoRun;
            }
            attribute |= NativeMethods.OpenAttributeBitfilePathIsUtf8;

            var args = new Dictionary<string, object>
            {
                { "bitfilePath", bitfilePath },
                { "signature", _bitfile.Signature },
                { "resource", resourceName },
                { "attribute", attribute }
            };

            StatusChecker.CheckStatus(
                NativeMethods.Open(_bitfile.FilePath, _bitfile.Signature, resourceName, attribute, out _sessionHandle),
                nameof(NativeMethods.Open),
                args);
        }

        #region Session Control
        /// <summary>
        /// Runs the FPGA VI on the target.
        /// </summary>
        public void Run()
        {
            CheckHandle();
            StatusChecker.CheckStatus(NativeMethods.Run(_sessionHandle, 0), nameof(NativeMethods.Run));
        }

        /// <summary>
        /// Aborts the FPGA VI.
        /// </summary>
        public void Abort()
        {
            CheckHandle();
            // FIX: Removed second argument '0'
            StatusChecker.CheckStatus(NativeMethods.Abort(_sessionHandle), nameof(NativeMethods.Abort));
        }

        /// <summary>
        /// Resets the FPGA VI.
        /// </summary>
        public void Reset()
        {
            CheckHandle();
            // FIX: Removed second argument '0'
            StatusChecker.CheckStatus(NativeMethods.Reset(_sessionHandle), nameof(NativeMethods.Reset));
        }

        /// <summary>
        /// Downloads the bitfile to the FPGA again.
        /// </summary>
        public void Download()
        {
            CheckHandle();
            // FIX: Removed second argument '0'
            StatusChecker.CheckStatus(NativeMethods.Download(_sessionHandle), nameof(NativeMethods.Download));
        }
        #endregion

        #region Registers and FIFOs

        /// <summary>
        /// Gets a handle to a typed FPGA register (Control or Indicator).
        /// </summary>
        /// <typeparam name="T">The data type of the register (e.g., uint, bool, FxpValue, Dictionary&lt;string, object&gt;).</typeparam>
        /// <param name="name">The name of the register from the bitfile.</param>
        /// <returns>An FpgaRegister object for reading or writing.</returns>
        public FpgaRegister GetRegister<T>(string name)
        {
            lock (_lock)
            {
                if (_registers.TryGetValue(name, out var reg))
                {
                    // Validate cached type
                    // FIX: Changed _info to public 'Info' property
                    if (reg.Info.TypeInfo.PublicType == typeof(T))
                        return reg;
                    // FIX: Changed _info to public 'Info' property
                    throw new InvalidCastException($"Register '{name}' was previously accessed as {reg.Info.TypeInfo.PublicType.Name}, but was requested as {typeof(T).Name}.");
                }

                if (!_bitfile.Registers.TryGetValue(name, out var regInfo))
                {
                    throw new KeyNotFoundException($"Register '{name}' not found in bitfile.");
                }

                if (regInfo.TypeInfo.PublicType != typeof(T))
                {
                    throw new InvalidCastException($"Register '{name}' is of type {regInfo.TypeInfo.PublicType.Name}, but was requested as {typeof(T).Name}.");
                }

                var newReg = new FpgaRegister(this, regInfo);
                _registers.Add(name, newReg);
                return newReg;
            }
        }

        /// <summary>
        /// Gets a handle to a typed FPGA FIFO.
        /// </summary>
        /// <typeparam name="T">The element type of the FIFO (e.g., uint, bool[], FxpValue, Dictionary&lt;string, object&gt;).</typeparam>
        /// <param name="name">The name of the FIFO from the bitfile.</param>
        /// <returns>An FpgaFifo object for configuration and I/O.</returns>
        public FpgaFifo GetFifo<T>(string name)
        {
             lock (_lock)
            {
                if (_fifos.TryGetValue(name, out var fifo))
                {
                    // Validate cached type
                    // FIX: Changed _info to public 'Info' property
                    if (fifo.Info.TypeInfo.PublicType == typeof(T))
                        return fifo;
                    // FIX: Changed _info to public 'Info' property
                    throw new InvalidCastException($"FIFO '{name}' was previously accessed as {fifo.Info.TypeInfo.PublicType.Name}, but was requested as {typeof(T).Name}.");
                }

                if (!_bitfile.Fifos.TryGetValue(name, out var fifoInfo))
                {
                    throw new KeyNotFoundException($"FIFO '{name}' not found in bitfile.");
                }

                if (fifoInfo.TypeInfo.PublicType != typeof(T))
                {
                    throw new InvalidCastException($"FIFO '{name}' is of type {fifoInfo.TypeInfo.PublicType.Name}, but was requested as {typeof(T).Name}.");
                }

                var newFifo = new FpgaFifo(this, fifoInfo);
                _fifos.Add(name, newFifo);
                return newFifo;
            }
        }

        #endregion

        #region IRQ Handling
        /// <summary>
        /// Waits for one or more IRQs to be asserted by the FPGA.
        /// </summary>
        /// <param name="irqMask">A bitmask of the IRQs to wait on (e.g., 0x1 for IRQ0, 0x3 for IRQ0 and IRQ1).</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>An IrqWaitResult record.</returns>
        public IrqWaitResult WaitOnIrqs(uint irqMask, uint timeoutMs)
        {
            CheckHandle();
            IntPtr context = IntPtr.Zero;
            try
            {
                StatusChecker.CheckStatus(NativeMethods.ReserveIrqContext(_sessionHandle, out context), nameof(NativeMethods.ReserveIrqContext));
                if (context == IntPtr.Zero)
                {
                    // FIX: Pass null for arguments, matching new Exceptions.cs
                    throw new FpgaException(-1, "Failed to reserve IRQ context.", nameof(NativeMethods.ReserveIrqContext), null);
                }

                StatusChecker.CheckStatus(NativeMethods.WaitOnIrqs(_sessionHandle, context, irqMask, timeoutMs, out uint irqsAsserted, out byte timedOut), nameof(NativeMethods.WaitOnIrqs));
                return new IrqWaitResult(irqsAsserted, timedOut != 0);
            }
            finally
            {
                if (context != IntPtr.Zero)
                {
                    NativeMethods.UnreserveIrqContext(_sessionHandle, context);
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for one or more IRQs to be asserted by the FPGA.
        /// </summary>
        /// <param name="irqMask">A bitmask of the IRQs to wait on.</param>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>A Task returning an IrqWaitResult record.</returns>
        public Task<IrqWaitResult> WaitOnIrqsAsync(uint irqMask, uint timeoutMs)
        {
            // Wrap the synchronous, blocking call in a Task to make it awaitable.
            return Task.Run(() => WaitOnIrqs(irqMask, timeoutMs));
        }

        /// <summary>
        /// Acknowledges that one or more IRQs have been handled.
        /// </summary>
        /// <param name="irqMask">A bitmask of the IRQs to acknowledge.</param>
        public void AcknowledgeIrqs(uint irqMask)
        {
            CheckHandle();
            StatusChecker.CheckStatus(NativeMethods.AcknowledgeIrqs(_sessionHandle, irqMask), nameof(NativeMethods.AcknowledgeIrqs));
        }
        #endregion

        #region IDisposable Implementation
        ~FpgaSession()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (_sessionHandle != 0)
            {
                // We should try to abort/reset here, but for simplicity
                // we just close the session. The Python library has a
                // 'reset_if_last_session_on_exit' flag.
                NativeMethods.Close(_sessionHandle, 0);
                _sessionHandle = 0;
            }
            _disposed = true;
        }

        internal void CheckHandle()
        {
            if (_disposed || _sessionHandle == 0)
            {
                throw new ObjectDisposedException(nameof(FpgaSession), "The FPGA session has been closed or disposed.");
            }
        }
        #endregion
    }
}