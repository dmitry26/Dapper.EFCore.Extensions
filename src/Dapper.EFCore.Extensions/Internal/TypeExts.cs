// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dapper.Internal
{
    internal static class TypeExts
    {
		// Based on the article: http://tmont.com/blargh/2011/3/determining-if-an-open-generic-type-isassignablefrom-a-type		
		/// <summary>
		/// Determines whether the <paramref name="source"/> is assignable to
		/// <paramref name="toType"/> taking into account generic definitions
		/// </summary>
		/// <param name="source">The type of the source.</param>
		/// <param name="toType">The type to compare with the source type.</param>
		public static bool IsAssignableTo(this Type source,Type toType)
		{			
			if (source == null || toType == null)
				return false;

			if (!toType.IsGenericTypeDefinition)
				return toType.IsAssignableFrom(source);

			//open generic type
			return source == toType
			  || source.MapsToOpenGenericType(toType)
			  || (toType.IsInterface && source.ImplementOpenGenericInterface(toType))
			  || source.BaseType.IsAssignableTo(toType);
		}

		private static bool ImplementOpenGenericInterface(this Type source,Type toType) =>
			source.GetInterfaces().Where(it => it.IsGenericType)
				.Any(it => it.GetGenericTypeDefinition() == toType);

		private static bool MapsToOpenGenericType(this Type source,Type toType) =>
			source.IsGenericType && source.GetGenericTypeDefinition() == toType;

		/// <summary>
		/// Determines whether the <paramref name="source"/> implements interface
		/// <paramref name="ifaceType"/> 
		/// </summary>
		/// <param name="source">The type of the source.</param>
		/// <param name="ifaceType">An interface type.</param>
		/// <returns>true if the source implements interface; otherwise, false.</returns>
		public static bool ImplementInterface(this Type source,Type ifaceType)
		{			
			if (source == null || ifaceType == null || !ifaceType.IsInterface)
				return false;

			var t = source;

			while (t != null)
			{
				var interfaces = t.GetInterfaces();

				if (interfaces != null)
				{
					// Interfaces don't derive from other interfaces, they implement them.
					if (interfaces.Any(it => it == ifaceType || (it != null && it.ImplementInterface(ifaceType))))
						return true;
				}

				t = t.BaseType;
			}

			return false;
		}
	}
}
