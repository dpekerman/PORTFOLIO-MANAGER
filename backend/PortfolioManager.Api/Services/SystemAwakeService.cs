using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Keeps the system awake for the duration of a long-running async operation using the handle-based
/// Windows power-request API (PowerCreateRequest/PowerSetRequest/PowerClearRequest) rather than
/// SetThreadExecutionState, which is thread-affine and unsafe once an async method's continuations
/// resume on a different thread-pool thread. Requests SystemRequired only — never DisplayRequired,
/// so the screen is still allowed to turn off.
/// </summary>
public interface ISystemAwakeService
{
    /// <summary>Acquires a system-awake lease. Dispose (or "await using") to release it.</summary>
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct = default);
}

public sealed class SystemAwakeService(ILogger<SystemAwakeService> logger) : ISystemAwakeService
{
    public Task<IAsyncDisposable> AcquireAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("[SystemAwake] Not running on Windows — power-request lease is a no-op.");
            return Task.FromResult<IAsyncDisposable>(new NoopLease());
        }

        var handle = PowerRequestNative.CreateSystemRequiredRequest(logger);
        return Task.FromResult<IAsyncDisposable>(new PowerRequestLease(handle, logger));
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class PowerRequestLease(PowerRequestNative.PowerRequestSafeHandle? handle, ILogger logger) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            if (handle is { IsInvalid: false })
            {
                try
                {
#pragma warning disable CA1416 // only ever constructed with a real handle when OperatingSystem.IsWindows()
                    PowerRequestNative.PowerClearRequest(handle, PowerRequestNative.PowerRequestType.SystemRequired);
#pragma warning restore CA1416
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[SystemAwake] PowerClearRequest failed.");
                }
                finally
                {
                    handle.Dispose();
                }
            }
            return ValueTask.CompletedTask;
        }
    }
}

[SupportedOSPlatform("windows")]
internal static class PowerRequestNative
{
    private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

    public enum PowerRequestType
    {
        DisplayRequired = 0,
        SystemRequired = 1,
        AwayModeRequired = 2,
        ExecutionRequired = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    public sealed class PowerRequestSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public PowerRequestSafeHandle() : base(true) { }

        protected override bool ReleaseHandle() => CloseHandle(handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern PowerRequestSafeHandle PowerCreateRequest(ref REASON_CONTEXT context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(PowerRequestSafeHandle powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PowerClearRequest(PowerRequestSafeHandle powerRequest, PowerRequestType requestType);

    /// <summary>Creates and activates a SystemRequired power request. Returns null (with a logged
    /// warning) if the OS call fails — automation continues without a keep-awake guarantee rather
    /// than aborting the run.</summary>
    public static PowerRequestSafeHandle? CreateSystemRequiredRequest(ILogger logger)
    {
        var reasonPtr = Marshal.StringToHGlobalUni("Portfolio Manager EOD automation run");
        try
        {
            var context = new REASON_CONTEXT
            {
                Version = POWER_REQUEST_CONTEXT_VERSION,
                Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                SimpleReasonString = reasonPtr,
            };

            var handle = PowerCreateRequest(ref context);
            if (handle.IsInvalid)
            {
                logger.LogWarning("[SystemAwake] PowerCreateRequest failed (Win32 error {Error}).", Marshal.GetLastWin32Error());
                return null;
            }

            if (!PowerSetRequest(handle, PowerRequestType.SystemRequired))
            {
                logger.LogWarning("[SystemAwake] PowerSetRequest failed (Win32 error {Error}).", Marshal.GetLastWin32Error());
                handle.Dispose();
                return null;
            }

            return handle;
        }
        finally
        {
            // Windows copies the reason string internally during PowerCreateRequest; safe to free now.
            Marshal.FreeHGlobal(reasonPtr);
        }
    }
}
