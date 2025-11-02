using System;
using System.Collections.Generic;

namespace FpgaInterface
{
    /// <summary>
    /// Base exception for all FPGA Interface errors
    /// </summary>
    public class FpgaException : Exception
    {
        public int StatusCode { get; }
        public string FunctionName { get; }
        // FIX: Made dictionary nullable
        public IReadOnlyDictionary<string, object>? Arguments { get; }

        // FIX: Made arguments nullable
        public FpgaException(int statusCode, string message, string functionName, Dictionary<string, object>? arguments)
            : base(FormatMessage(statusCode, message, functionName, arguments))
        {
            StatusCode = statusCode;
            FunctionName = functionName;
            Arguments = arguments;
        }

        // FIX: Made arguments nullable
        private static string FormatMessage(int statusCode, string message, string functionName, Dictionary<string, object>? arguments)
        {
            var argString = string.Empty;
            if (arguments != null && arguments.Count > 0)
            {
                var argList = new List<string>();
                foreach (var kvp in arguments)
                {
                    argList.Add($"  {kvp.Key}: {FormatValue(kvp.Value)}");
                }
                argString = "\n" + string.Join("\n", argList);
            }

            return $"Error: {message} ({statusCode}) when calling '{functionName}'{argString}";
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => "null",
                byte b => $"0x{b:X2}",
                ushort us => $"0x{us:X4}",
                uint ui => $"0x{ui:X8}",
                ulong ul => $"0x{ul:X16}",
                sbyte sb => $"0x{sb:X2}",
                short s => $"0x{s:X4}",
                int i => $"0x{i:X8}",
                long l => $"0x{l:X16}",
                string str => $"\"{str}\"",
                // *** FIX: Handle possible null from ToString() ***
                _ => value.ToString() ?? "null"
            };
        }
    }

    /// <summary>
    /// FPGA warning base class
    /// </summary>
    public class FpgaWarning : FpgaException
    {
        // FIX: Made arguments nullable
        public FpgaWarning(int statusCode, string message, string functionName, Dictionary<string, object>? arguments)
            : base(statusCode, message, functionName, arguments)
        {
        }
    }

    // Specific error types (auto-generated from status codes)
    public class FifoTimeoutException : FpgaException
    {
        public FifoTimeoutException(string functionName, Dictionary<string, object>? arguments)
            : base(-50400, "FifoTimeout", functionName, arguments) { }
    }

    public class TransferAbortedException : FpgaException
    {
        public TransferAbortedException(string functionName, Dictionary<string, object>? arguments)
            : base(-50405, "TransferAborted", functionName, arguments) { }
    }

    public class MemoryFullException : FpgaException
    {
        public MemoryFullException(string functionName, Dictionary<string, object>? arguments)
            : base(-52000, "MemoryFull", functionName, arguments) { }
    }

    public class SoftwareFaultException : FpgaException
    {
        public SoftwareFaultException(string functionName, Dictionary<string, object>? arguments)
            : base(-52003, "SoftwareFault", functionName, arguments) { }
    }

    public class InvalidParameterException : FpgaException
    {
        public InvalidParameterException(string functionName, Dictionary<string, object>? arguments)
            : base(-52005, "InvalidParameter", functionName, arguments) { }
    }

    public class ResourceNotFoundException : FpgaException
    {
        public ResourceNotFoundException(string functionName, Dictionary<string, object>? arguments)
            : base(-52006, "ResourceNotFound", functionName, arguments) { }
    }

    public class FpgaAlreadyRunningException : FpgaException
    {
        public FpgaAlreadyRunningException(string functionName, Dictionary<string, object>? arguments)
            : base(-61003, "FpgaAlreadyRunning", functionName, arguments) { }
    }

    public class DownloadErrorException : FpgaException
    {
        public DownloadErrorException(string functionName, Dictionary<string, object>? arguments)
            : base(-61018, "DownloadError", functionName, arguments) { }
    }

    public class DeviceTypeMismatchException : FpgaException
    {
        public DeviceTypeMismatchException(string functionName, Dictionary<string, object>? arguments)
            : base(-61024, "DeviceTypeMismatch", functionName, arguments) { }
    }

    public class CommunicationTimeoutException : FpgaException
    {
        public CommunicationTimeoutException(string functionName, Dictionary<string, object>? arguments)
            : base(-61046, "CommunicationTimeout", functionName, arguments) { }
    }

    public class IrqTimeoutException : FpgaException
    {
        public IrqTimeoutException(string functionName, Dictionary<string, object>? arguments)
            : base(-61060, "IrqTimeout", functionName, arguments) { }
    }

    public class CorruptBitfileException : FpgaException
    {
        public CorruptBitfileException(string functionName, Dictionary<string, object>? arguments)
            : base(-61070, "CorruptBitfile", functionName, arguments) { }
    }

    public class BadDepthException : FpgaException
    {
        public BadDepthException(string functionName, Dictionary<string, object>? arguments)
            : base(-61072, "BadDepth", functionName, arguments) { }
    }

    public class FpgaBusyException : FpgaException
    {
        public FpgaBusyException(string functionName, Dictionary<string, object>? arguments)
            : base(-61141, "FpgaBusy", functionName, arguments) { }
    }

    public class InvalidSessionException : FpgaException
    {
        public InvalidSessionException(string functionName, Dictionary<string, object>? arguments)
            : base(-63195, "InvalidSession", functionName, arguments) { }
    }

    public class SignatureMismatchException : FpgaException
    {
        public SignatureMismatchException(string functionName, Dictionary<string, object>? arguments)
            : base(-63106, "SignatureMismatch", functionName, arguments) { }
    }

    public class VersionMismatchException : FpgaException
    {
        public VersionMismatchException(string functionName, Dictionary<string, object>? arguments)
            : base(-63194, "VersionMismatch", functionName, arguments) { }
    }

    /// <summary>
    /// Helper class to check status codes and throw appropriate exceptions
    /// </summary>
    internal static class StatusChecker
    {
        // FIX: Made arguments nullable
        private static readonly Dictionary<int, Func<string, Dictionary<string, object>?, FpgaException>> ErrorFactories = new()
        {
            { -50400, (fn, args) => new FifoTimeoutException(fn, args) },
            { -50405, (fn, args) => new TransferAbortedException(fn, args) },
            { -52000, (fn, args) => new MemoryFullException(fn, args) },
            { -52003, (fn, args) => new SoftwareFaultException(fn, args) },
            { -52005, (fn, args) => new InvalidParameterException(fn, args) },
            { -52006, (fn, args) => new ResourceNotFoundException(fn, args) },
            { -61003, (fn, args) => new FpgaAlreadyRunningException(fn, args) },
            { -61018, (fn, args) => new DownloadErrorException(fn, args) },
            { -61024, (fn, args) => new DeviceTypeMismatchException(fn, args) },
            { -61046, (fn, args) => new CommunicationTimeoutException(fn, args) },
            { -61060, (fn, args) => new IrqTimeoutException(fn, args) },
            { -61070, (fn, args) => new CorruptBitfileException(fn, args) },
            { -61072, (fn, args) => new BadDepthException(fn, args) },
            { -61141, (fn, args) => new FpgaBusyException(fn, args) },
            { -63195, (fn, args) => new InvalidSessionException(fn, args) },
            { -63106, (fn, args) => new SignatureMismatchException(fn, args) },
            { -63194, (fn, args) => new VersionMismatchException(fn, args) },
        };

        // FIX: Made arguments nullable
        public static void CheckStatus(int status, string functionName, Dictionary<string, object>? arguments = null)
        {
            if (status == 0) return;

            // FIX: No longer need this, as null is allowed
            // arguments ??= new Dictionary<string, object>();

            if (ErrorFactories.TryGetValue(status, out var factory))
            {
                throw factory(functionName, arguments);
            }

            // Unknown error
            var message = status < 0 ? "Unknown error" : "Unknown warning";
            throw new FpgaException(status, message, functionName, arguments);
        }
    }
}