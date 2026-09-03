/*
 * Copyright (c) 2026 Proton AG
 *
 * This file is part of ProtonVPN.
 *
 * ProtonVPN is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * ProtonVPN is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with ProtonVPN.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ProtonVPN.Common.Core.Networking;
using ProtonVPN.Common.Legacy.Vpn;
using ProtonVPN.Configurations.Contracts;
using ProtonVPN.Logging.Contracts;
using ProtonVPN.Logging.Contracts.Events.ConnectionLogs;
using ProtonVPN.Logging.Contracts.Events.ConnectLogs;
using ProtonVPN.Logging.Contracts.Events.DisconnectLogs;
using ProtonVPN.Vpn.Common;

namespace ProtonVPN.Vpn.Aether;

public class AetherConnection : IAetherConnection
{
    private const int SOCKS5_READINESS_ATTEMPTS = 120;
    private const int SOCKS5_READINESS_DELAY_MS = 250;

    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly Channel<VpnState> _stateChannel = Channel.CreateUnbounded<VpnState>();

    private Process? _process;
    private TaskCompletionSource<bool>? _connectionTaskCompletionSource;
    private volatile bool _isConnected;
    private VpnError _lastError = VpnError.None;
    private string? _localIpv4Address;
    private VpnEndpoint? _endpoint;
    private VpnConfig? _vpnConfig;

    public AetherConnection(ILogger logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public string? LocalIpv4Address => _localIpv4Address;

    public NetworkTraffic NetworkTraffic => NetworkTraffic.Zero;

    public async Task<VpnError> ConnectAsync(VpnEndpoint endpoint, VpnCredentials credentials, VpnConfig vpnConfig, CancellationToken cancellationToken)
    {
        _endpoint = endpoint;
        _vpnConfig = vpnConfig;
        _lastError = VpnError.None;
        _localIpv4Address = null;
        _isConnected = false;

        _connectionTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        _logger.Info<ConnectStartLog>("Aether connect action started.");

        if (!File.Exists(_config.Aether.ExePath))
        {
            _logger.Error<ConnectionErrorLog>($"Aether executable not found at {_config.Aether.ExePath}.");
            return VpnError.Unknown;
        }

        StopProcess();

        OnStateChanged(VpnStatus.Connecting);

        if (TryStartProcess(out Process? process))
        {
            _process = process;
            StartMonitoringProcessExitAsync(process, cancellationToken);
        }
        else
        {
            _lastError = VpnError.Unknown;
            return _lastError;
        }

        bool ready = await WaitForSocks5ReadyAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ready)
        {
            _logger.Error<ConnectionErrorLog>("Aether did not expose the SOCKS5 proxy in time.");
            _lastError = VpnError.ServerUnreachable;
            return _lastError;
        }

        _isConnected = true;
        _localIpv4Address = _config.Aether.Socks5Host;
        SetConnectionTaskResult(true);
        OnStateChanged(VpnStatus.Connected);

        _logger.Info<ConnectLog>("Aether tunnel established.");

        return VpnError.None;
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;

        _logger.Info<DisconnectLog>("Aether disconnect action started.");
        OnStateChanged(VpnStatus.Disconnecting);

        StopProcess();

        SetConnectionTaskResult(false);
        OnStateChanged(VpnStatus.Disconnected);

        _logger.Info<DisconnectLog>("Aether disconnect action completed.");
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<VpnState> ObserveStatesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await _stateChannel.Reader.ReadAsync(cancellationToken);
        }
    }

    private bool TryStartProcess(out Process? process)
    {
        process = null;
        try
        {
            string args = $"--bind {_config.Aether.Socks5Host}:{_config.Aether.Socks5Port} " +
                          $"--http-proxy {_config.Aether.Socks5Host}:{_config.Aether.HttpProxyPort} ";

            ProcessStartInfo startInfo = new(_config.Aether.ExePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process = new Process { StartInfo = startInfo };
            process.Start();
            _logger.Info<ConnectLog>($"Aether process started (pid {process.Id}).");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error<ConnectionErrorLog>("Failed to start Aether process.", ex);
            return false;
        }
    }

    private async Task<bool> WaitForSocks5ReadyAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < SOCKS5_READINESS_ATTEMPTS; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process is not null && _process.HasExited)
            {
                _logger.Error<ConnectionErrorLog>("Aether process exited before becoming ready.");
                return false;
            }

            if (IsSocks5PortOpen())
            {
                return true;
            }

            await Task.Delay(SOCKS5_READINESS_DELAY_MS, cancellationToken);
        }

        return false;
    }

    private bool IsSocks5PortOpen()
    {
        try
        {
            using TcpClient client = new();
            client.Connect(_config.Aether.Socks5Host, _config.Aether.Socks5Port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void StartMonitoringProcessExitAsync(Process process, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
                if (!cancellationToken.IsCancellationRequested && _isConnected)
                {
                    _logger.Warn<DisconnectLog>("Aether process exited unexpectedly.");
                    await _stateChannel.Writer.WriteAsync(
                        new VpnState(VpnStatus.Connected, VpnError.Unknown, _endpoint?.VpnProtocol ?? VpnProtocol.Aether),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error<ConnectionLog>("Aether exit monitor failed.", ex);
            }
        }, cancellationToken);
    }

    private void StopProcess()
    {
        Process? process = _process;
        _process = null;

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn<DisconnectLog>("Failed to stop Aether process.", ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    private void SetConnectionTaskResult(bool result)
    {
        if (_connectionTaskCompletionSource?.Task.IsCompletedSuccessfully == false)
        {
            _connectionTaskCompletionSource.SetResult(result);
        }
    }

    private void OnStateChanged(VpnStatus status)
    {
        VpnProtocol protocol = _vpnConfig?.VpnProtocol ?? VpnProtocol.Aether;
        VpnState state = new(status, _lastError, protocol);
        _stateChannel.Writer.TryWrite(state);
    }
}
