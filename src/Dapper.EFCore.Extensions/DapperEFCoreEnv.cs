// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dapper.Extensions
{
    public static class DapperEFCoreEnv
    {
		private static ILogger _logger;

		public static ILogger Logger
		{
			internal get => _logger;
			set => _logger = value ?? throw new ArgumentNullException(nameof(Logger));
		}

		/// <summary>
		/// Checks if the Logger is not null and LogLevel.Debug is enabled
		/// </summary>
		/// <returns></returns>
		public static bool IsLogEnabled() => _logger?.IsEnabled(LogLevel.Debug) ?? false;
    }
}
