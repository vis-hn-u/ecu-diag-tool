using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware.Transports;

public class WaveshareCanFdTransport : ICanTransport
{
    public string TransportId => "waveshare-usb-can-fd";
    public string Name => "Waveshare USB-CAN-FD-B (Dual Channel)";
    public TransportType Type => TransportType.WaveshareCanFd;

    public HardwareCapabilities Capabilities { get; } = new()
    {
        SupportsCanFd = true,
        SupportsBitRateSwitch = true,
        SupportsClassicCan = true,
        ChannelCount = 2,
        MaxPayloadBytes = 64,
        SupportsTransmit = true,
        SupportsHardwareTimestamps = true,
        SupportedNominalBaudRates = new uint[] { 100000, 250000, 500000, 1000000 },
        SupportedDataBaudRates = new uint[] { 1000000, 2000000, 4000000, 5000000 }
    };

    private readonly object _lock = new();
    private bool _isConnected;
    private long _totalFramesReceived;
    private long _totalFramesTransmitted;
    private int _windowFramesReceived;
    private int _windowFramesTransmitted;
    private double _rxFrameRate;
    private double _txFrameRate;
    private DateTime? _lastSeenUtc;
    private string? _lastError;
    private DateTime _lastFpsCalc = DateTime.UtcNow;

    private CancellationTokenSource? _rxCts;
    private Task? _rxTask;
    private IntPtr _deviceHandle = IntPtr.Zero;
    private readonly IntPtr[] _channelHandles = new IntPtr[2];
    private bool _benchLoopbackActive;

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;

    public uint NominalBaudRate { get; set; } = 500000;
    public uint DataBaudRate { get; set; } = 2000000;
    public byte ActiveChannel { get; set; } = 0; // 0 = CAN1, 1 = CAN2
    public bool TerminalResistanceEnabled { get; set; } = true;
    public bool ForceBenchLoopback { get; set; } = false;

    public bool IsHardwareDevicePresent => _deviceHandle != IntPtr.Zero;
    public bool IsBenchLoopbackActive => _benchLoopbackActive;
    public string? SerialNumber { get; private set; }
    public string? HardwareVersion { get; private set; }
    public string? FirmwareVersion { get; private set; }

    public void Configure(uint? nominalBaudRate, uint? dataBaudRate, byte? channel)
    {
        lock (_lock)
        {
            if (nominalBaudRate.HasValue && nominalBaudRate.Value > 0)
            {
                NominalBaudRate = nominalBaudRate.Value;
            }
            if (dataBaudRate.HasValue && dataBaudRate.Value > 0)
            {
                DataBaudRate = dataBaudRate.Value;
            }
            if (channel.HasValue)
            {
                ActiveChannel = channel.Value;
            }
        }
        NotifyStatusChanged();
    }

    public HardwareStatus GetStatus()
    {
        lock (_lock)
        {
            string connDetails = _benchLoopbackActive
                ? $"Bench Loopback (CAN1<->CAN2, 120Ω Terminated), Nominal: {NominalBaudRate / 1000}k, Data: {DataBaudRate / 1000000}M"
                : $"USB Connected (CAN1 & CAN2), Nominal: {NominalBaudRate / 1000}k, Data: {DataBaudRate / 1000000}M, Active CH: CAN{ActiveChannel + 1}";

            return new HardwareStatus
            {
                TransportId = TransportId,
                Name = Name,
                Type = Type,
                IsConnected = _isConnected,
                ConnectionDetails = connDetails,
                TotalFramesReceived = _totalFramesReceived,
                TotalFramesTransmitted = _totalFramesTransmitted,
                RxFrameRate = _rxFrameRate,
                TxFrameRate = _txFrameRate,
                LastSeenUtc = _lastSeenUtc,
                LastError = _lastError,
                Capabilities = Capabilities,
                ExtendedTelemetry = new
                {
                    NominalBaudRate,
                    DataBaudRate,
                    ActiveChannel,
                    IsHardwareDevicePresent,
                    IsBenchLoopbackActive = _benchLoopbackActive,
                    SerialNumber,
                    HardwareVersion,
                    FirmwareVersion,
                    TerminalResistanceEnabled,
                    Driver = "Waveshare ControlCANFD.dll (USB-CAN-FD-B)"
                }
            };
        }
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isConnected) return Task.FromResult(true);

            if (ForceBenchLoopback)
            {
                _isConnected = true;
                _benchLoopbackActive = true;
                _lastError = null;
                NotifyStatusChanged();
                return Task.FromResult(true);
            }

            try
            {
                // Attempt native device open via documented ControlCANFD.dll
                _deviceHandle = NativeMethods.OpenDevice(NativeMethods.ZCAN_USBCANFD_200U, 0, 0);
                if (_deviceHandle == IntPtr.Zero || _deviceHandle == new IntPtr(-1))
                {
                    _deviceHandle = IntPtr.Zero;
                    _benchLoopbackActive = false;
                    _isConnected = false;
                    _lastError = "Physical Waveshare USB-CAN-FD-B hardware not detected on USB bus (ZCAN_OpenDevice returned 0)";
                    NotifyStatusChanged();
                    return Task.FromResult(false);
                }

                // Physical hardware successfully opened: query device info
                var devInfo = new NativeMethods.ZCAN_DEVICE_INFO();
                if (NativeMethods.GetDeviceInf(_deviceHandle, ref devInfo) == NativeMethods.STATUS_OK)
                {
                    HardwareVersion = $"0x{devInfo.hw_Version:X4}";
                    FirmwareVersion = $"0x{devInfo.fw_Version:X4}";
                    SerialNumber = devInfo.str_Serial_Num != null
                        ? System.Text.Encoding.ASCII.GetString(devInfo.str_Serial_Num).TrimEnd('\0', ' ')
                        : null;
                }

                // Initialize CAN1 and CAN2
                for (uint ch = 0; ch < 2; ch++)
                {
                    NativeMethods.SetAbitBaud(_deviceHandle, ch, NominalBaudRate);
                    NativeMethods.SetDbitBaud(_deviceHandle, ch, DataBaudRate);
                    NativeMethods.SetResistanceEnable(_deviceHandle, ch, TerminalResistanceEnabled ? 1u : 0u);

                    var initConfig = new NativeMethods.ZCAN_CHANNEL_INIT_CONFIG
                    {
                        can_type = 1, // TYPE_CANFD
                        acc_code = 0,
                        acc_mask = 0xFFFFFFFF,
                        filter = 1,
                        mode = 0 // Normal
                    };

                    _channelHandles[ch] = NativeMethods.InitCAN(_deviceHandle, ch, ref initConfig);
                    if (_channelHandles[ch] != IntPtr.Zero)
                    {
                        NativeMethods.ClearFilter(_channelHandles[ch]);
                        NativeMethods.AckFilter(_channelHandles[ch]);
                        NativeMethods.StartCAN(_channelHandles[ch]);
                    }
                }

                _isConnected = true;
                _benchLoopbackActive = false;
                _lastError = null;

                _rxCts = new CancellationTokenSource();
                _rxTask = Task.Run(() => ReceiveLoopAsync(_rxCts.Token));
            }
            catch (DllNotFoundException)
            {
                _deviceHandle = IntPtr.Zero;
                _benchLoopbackActive = false;
                _isConnected = false;
                _lastError = "Native ControlCANFD.dll not found on system path";
            }
            catch (Exception ex)
            {
                _deviceHandle = IntPtr.Zero;
                _benchLoopbackActive = false;
                _isConnected = false;
                _lastError = $"Hardware open error: {ex.Message}";
            }

            NotifyStatusChanged();
            return Task.FromResult(_isConnected);
        }
    }

    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            _isConnected = false;
            _benchLoopbackActive = false;
            _rxCts?.Cancel();

            if (_deviceHandle != IntPtr.Zero && _deviceHandle != new IntPtr(-1))
            {
                try
                {
                    for (int ch = 0; ch < 2; ch++)
                    {
                        if (_channelHandles[ch] != IntPtr.Zero)
                        {
                            NativeMethods.ResetCAN(_channelHandles[ch]);
                            _channelHandles[ch] = IntPtr.Zero;
                        }
                    }
                    NativeMethods.CloseDevice(_deviceHandle);
                }
                catch
                {
                    // Suppress unmanaged cleanup errors
                }
                _deviceHandle = IntPtr.Zero;
            }
        }

        NotifyStatusChanged();
        return Task.CompletedTask;
    }

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isConnected)
            {
                return ValueTask.FromResult(false);
            }

            _totalFramesTransmitted++;
            _windowFramesTransmitted++;
            _lastSeenUtc = DateTime.UtcNow;

            if (_benchLoopbackActive || _deviceHandle == IntPtr.Zero)
            {
                // CAN1 -> CAN2 / CAN2 -> CAN1 bench loopback
                byte oppositeChannel = (byte)(frame.Channel == 0 ? 1 : 0);
                string source = oppositeChannel == 0 ? "Waveshare-CAN1" : "Waveshare-CAN2";

                var echoed = new CanFrame(
                    (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000),
                    frame.Id,
                    frame.Dlc,
                    frame.Flags,
                    frame.Data.AsSpan(0, frame.ActualLength),
                    oppositeChannel,
                    source
                );

                _totalFramesReceived++;
                _windowFramesReceived++;

                // Asynchronously deliver echo on opposite channel to emulate bus reception
                Task.Run(() => OnFrameReceived?.Invoke(echoed));
                return ValueTask.FromResult(true);
            }

            try
            {
                uint ch = frame.Channel;
                if (ch > 1 || _channelHandles[ch] == IntPtr.Zero)
                {
                    return ValueTask.FromResult(false);
                }

                var txData = new NativeMethods.ZCAN_TransmitFD_Data();
                txData.frame.can_id = frame.Id | (frame.IsExtended ? NativeMethods.CAN_EFF_FLAG : 0) | (frame.IsRtr ? NativeMethods.CAN_RTR_FLAG : 0);
                txData.frame.len = frame.ActualLength;

                byte flags = 0;
                if (frame.BitRateSwitch) flags |= NativeMethods.CANFD_BRS;
                if (frame.ErrorStateIndicator) flags |= NativeMethods.CANFD_ESI;
                txData.frame.flags = flags;

                if (frame.Data != null && frame.Data.Length > 0)
                {
                    int copyLen = Math.Min(64, (int)frame.ActualLength);
                    Array.Copy(frame.Data, txData.frame.data, copyLen);
                }

                txData.transmit_type = 0; // Normal transmission

                uint result = NativeMethods.TransmitFD(_channelHandles[ch], ref txData, 1);
                return ValueTask.FromResult(result == 1);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return ValueTask.FromResult(false);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var rxBuffer = new NativeMethods.ZCAN_ReceiveFD_Data[50];

        while (!token.IsCancellationRequested && _isConnected && !_benchLoopbackActive)
        {
            try
            {
                bool receivedAny = false;
                for (uint ch = 0; ch < 2; ch++)
                {
                    if (_channelHandles[ch] == IntPtr.Zero) continue;

                    uint count = NativeMethods.ReceiveFD(_channelHandles[ch], rxBuffer, (uint)rxBuffer.Length, 10);
                    if (count > 0)
                    {
                        receivedAny = true;
                        DateTime now = DateTime.UtcNow;

                        for (int i = 0; i < count; i++)
                        {
                            var item = rxBuffer[i];
                            uint rawId = item.frame.can_id;
                            bool isExt = (rawId & NativeMethods.CAN_EFF_FLAG) != 0;
                            bool isRtr = (rawId & NativeMethods.CAN_RTR_FLAG) != 0;
                            uint cleanId = rawId & (isExt ? CanFrame.ExtendedIdMax : CanFrame.StandardIdMax);

                            bool isBrs = (item.frame.flags & NativeMethods.CANFD_BRS) != 0;
                            bool isEsi = (item.frame.flags & NativeMethods.CANFD_ESI) != 0;

                            // Reconstruct CanFrame
                            byte dlc = CanFrame.LengthToDlc(item.frame.len);
                            byte flags = 0;
                            if (item.frame.len > 8 || isBrs || isEsi)
                            {
                                flags |= CanFrame.FlagCanFd;
                            }
                            if (isExt) flags |= CanFrame.FlagExtended;
                            if (isRtr) flags |= CanFrame.FlagRtr;
                            if (isBrs) flags |= CanFrame.FlagBrs;
                            if (isEsi) flags |= CanFrame.FlagEsi;

                            var frame = new CanFrame(
                                (uint)item.timestamp,
                                cleanId,
                                dlc,
                                flags,
                                item.frame.data.AsSpan(0, item.frame.len),
                                (byte)ch,
                                $"Waveshare-CAN{ch + 1}"
                            );

                            lock (_lock)
                            {
                                _totalFramesReceived++;
                                _windowFramesReceived++;
                                _lastSeenUtc = now;
                            }

                            OnFrameReceived?.Invoke(frame);
                        }
                    }
                }

                // Update frame rates
                var nowUtc = DateTime.UtcNow;
                var fpsElapsed = (nowUtc - _lastFpsCalc).TotalSeconds;
                if (fpsElapsed >= 1.0)
                {
                    lock (_lock)
                    {
                        _rxFrameRate = Math.Round(_windowFramesReceived / fpsElapsed, 1);
                        _txFrameRate = Math.Round(_windowFramesTransmitted / fpsElapsed, 1);
                        _windowFramesReceived = 0;
                        _windowFramesTransmitted = 0;
                        _lastFpsCalc = nowUtc;
                    }
                    NotifyStatusChanged();
                }

                if (!receivedAny)
                {
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _lastError = ex.Message;
                }
                await Task.Delay(100, token);
            }
        }
    }

    private void NotifyStatusChanged()
    {
        OnStatusChanged?.Invoke(GetStatus());
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisconnectAsync().GetAwaiter().GetResult();
        _rxCts?.Dispose();
    }

    /// <summary>
    /// Isolated Native Methods and Structs for Waveshare USB-CAN-FD-B / ControlCANFD Library.
    /// Strictly kept internal to prevent coupling other modules to vendor SDK.
    /// </summary>
    private static class NativeMethods
    {
        public const uint ZCAN_USBCANFD_200U = 41;
        public const uint STATUS_OK = 1;
        public const uint CAN_EFF_FLAG = 0x80000000U;
        public const uint CAN_RTR_FLAG = 0x40000000U;
        public const byte CANFD_BRS = 0x01;
        public const byte CANFD_ESI = 0x02;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct canfd_frame
        {
            public uint can_id;
            public byte len;
            public byte flags;
            public byte __res0;
            public byte __res1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] data;

            public canfd_frame()
            {
                can_id = 0;
                len = 0;
                flags = 0;
                __res0 = 0;
                __res1 = 0;
                data = new byte[64];
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZCAN_TransmitFD_Data
        {
            public canfd_frame frame;
            public uint transmit_type;

            public ZCAN_TransmitFD_Data()
            {
                frame = new canfd_frame();
                transmit_type = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZCAN_ReceiveFD_Data
        {
            public canfd_frame frame;
            public ulong timestamp;

            public ZCAN_ReceiveFD_Data()
            {
                frame = new canfd_frame();
                timestamp = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZCAN_DEVICE_INFO
        {
            public ushort hw_Version;
            public ushort fw_Version;
            public ushort dr_Version;
            public ushort in_Version;
            public ushort irq_Num;
            public byte can_Num;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] str_Serial_Num;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] str_hw_Type;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZCAN_CHANNEL_INIT_CONFIG
        {
            public uint can_type;
            public uint acc_code;
            public uint acc_mask;
            public uint abit_timing;
            public uint dbit_timing;
            public uint brp;
            public byte filter;
            public byte mode;
            public ushort pad;
            public uint reserved;
        }

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_OpenDevice", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr OpenDevice(uint deviceType, uint deviceIndex, uint reserved);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_CloseDevice", CallingConvention = CallingConvention.StdCall)]
        public static extern uint CloseDevice(IntPtr deviceHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_GetDeviceInf", CallingConvention = CallingConvention.StdCall)]
        public static extern uint GetDeviceInf(IntPtr deviceHandle, ref ZCAN_DEVICE_INFO pInfo);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_IsDeviceOnLine", CallingConvention = CallingConvention.StdCall)]
        public static extern uint IsDeviceOnLine(IntPtr deviceHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_InitCAN", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr InitCAN(IntPtr deviceHandle, uint canIndex, ref ZCAN_CHANNEL_INIT_CONFIG pInitConfig);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_StartCAN", CallingConvention = CallingConvention.StdCall)]
        public static extern uint StartCAN(IntPtr channelHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_ResetCAN", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ResetCAN(IntPtr channelHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_ClearFilter", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ClearFilter(IntPtr channelHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_AckFilter", CallingConvention = CallingConvention.StdCall)]
        public static extern uint AckFilter(IntPtr channelHandle);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_TransmitFD", CallingConvention = CallingConvention.StdCall)]
        public static extern uint TransmitFD(IntPtr channelHandle, ref ZCAN_TransmitFD_Data pTransmit, uint len);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_ReceiveFD", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ReceiveFD(IntPtr channelHandle, [Out] ZCAN_ReceiveFD_Data[] pReceive, uint len, int waitTime);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_SetAbitBaud", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SetAbitBaud(IntPtr deviceHandle, uint canIndex, uint abitbaud);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_SetDbitBaud", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SetDbitBaud(IntPtr deviceHandle, uint canIndex, uint dbitbaud);

        [DllImport("ControlCANFD.dll", EntryPoint = "ZCAN_SetResistanceEnable", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SetResistanceEnable(IntPtr deviceHandle, uint canIndex, uint enable);
    }
}
