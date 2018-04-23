// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Dmo.Hosting.Extensions
{
	public static class HostBuilderExts
	{		
		public static IConfigurationRoot GetAppSettings(string[] args = null,bool optional = true,bool reloadOnChange = false)
		{
			var hostEnv = GetHostEnvironment();

			var bldr = new ConfigurationBuilder()
				.AddEnvironmentVariables("DOTNETCORE_")
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json",optional,reloadOnChange)
				.AddJsonFile($"appsettings.{hostEnv}.json",optional,reloadOnChange);

			if (IsDevelopment(hostEnv))
				bldr.AddUserSecrets(Assembly.GetEntryAssembly(),optional: true);

			if (args != null && args.Length > 0)
				bldr.AddCommandLine(args);

			return bldr.Build();
		}
		
		private static string GetHostEnvironment()
		{
			return Environment.GetEnvironmentVariable("DOTNETCORE_ENVIRONMENT") ?? EnvironmentName.Production;
		}

		private static bool IsEnvironment(string hostEnv,string envName) => string.Equals(hostEnv,envName,StringComparison.OrdinalIgnoreCase);

		public static bool IsDevelopment(string hostEnv) => IsEnvironment(hostEnv,EnvironmentName.Development);

		public static string[] ExceptEnvArgs(string[] args)
		{
			if (args == null || args.Length == 0)
				return args;

			return GetArgs(args,x => !x);
		}

		private static string[] GetArgs(string[] args,Func<bool,bool> predicate)
		{
			var map = GetSwitchMappings();

			var items = args.Select(x =>
			{
				var idx = x.IndexOf('=');
				if (x[0] == '/') x = (idx == 2 ? "-" : "--") + x.Substring(1);
				return new { Arg = x,Key = (idx > 0) ? x.Substring(0,idx) : x };
			});

			return items.Where(x => predicate(map.ContainsKey(x.Key))).Select(x => x.Arg).ToArray();
		}

		private static Dictionary<string,string> GetSwitchMappings() => new Dictionary<string,string>
		{
			{"--urls","urls"},
			{"--environment","environment"},
			{"-u","urls"},
			{"-e","environment"},
		};

		public static IConfigurationRoot BuildEnvArgConfig(string[] args)
		{
			if (args == null || args.Length == 0)
				return new ConfigurationBuilder().Build();

			var res = new ConfigurationBuilder()
				.AddCommandLine(GetArgs(args,x => x),GetSwitchMappings())
				.Build();

			return res;
		}

		private static class EnvironmentName
		{
			public static readonly string Development = "Development";
			public static readonly string Staging = "Staging";
			public static readonly string Production = "Production";
		}
	}
}
