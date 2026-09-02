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

using ProtonVPN.Configurations.Contracts.Entities;

namespace ProtonVPN.Configurations.Entities;

public class AetherConfigurations : IAetherConfigurations
{
    public required string ExePath { get; init; }
    public required string Socks5Host { get; init; }
    public required int Socks5Port { get; init; }
    public required int HttpProxyPort { get; init; }
    public required string LogFilePath { get; init; }
    public required TimeSpan ConnectTimeout { get; init; }
}
