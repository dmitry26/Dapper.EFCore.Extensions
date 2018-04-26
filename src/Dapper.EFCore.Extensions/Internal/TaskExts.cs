// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dapper.Internal
{
	internal static class TaskExts
	{
		public static Task ForEachAsync<T>(this IEnumerable<T> source,Func<T,Task> asyncFunc,bool wrapIntoTask = false,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (!wrapIntoTask)
				return Task.WhenAll(source.Select(item => asyncFunc(item)));

			return Task.WhenAll(source.Select(item => Task.Run(() => asyncFunc(item),cancelToken)));
		}

		public static Task ForEachAsync<T>(this IEnumerable<T> source,Func<T,int,Task> asyncFunc,bool wrapIntoTask = false,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (!wrapIntoTask)
				return Task.WhenAll(source.Select((item,idx) => asyncFunc(item,idx)));

			return Task.WhenAll(source.Select((item,idx) => Task.Run(() => asyncFunc(item,idx),cancelToken)));
		}

		public static Task<TRes[]> ForEachAsync<T, TRes>(this IEnumerable<T> source,Func<T,Task<TRes>> asyncFunc,bool wrapIntoTask = false,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (!wrapIntoTask)
				return Task.WhenAll(source.Select(item => asyncFunc(item)));

			return Task.WhenAll(source.Select(item => Task.Run(() => asyncFunc(item))));
		}

		public static Task<TRes[]> ForEachAsync<T, TRes>(this IEnumerable<T> source,Func<T,int,Task<TRes>> asyncFunc,bool wrapIntoTask = false,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (!wrapIntoTask)
				return Task.WhenAll(source.Select((item,idx) => asyncFunc(item,idx)));

			return Task.WhenAll(source.Select((item,idx) => Task.Run(() => asyncFunc(item,idx),cancelToken)));
		}

		public static Task ForEachAsync<T>(this IEnumerable<T> source,Action<T> action,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (action == null) throw new ArgumentNullException(nameof(action));

			return Task.WhenAll(source.Select(item => Task.Run(() => action(item),cancelToken)));
		}

		public static Task ForEachAsync<T>(this IEnumerable<T> source,Action<T,int> action,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (action == null) throw new ArgumentNullException(nameof(action));

			return Task.WhenAll(source.Select((item,idx) => Task.Run(() => action(item,idx),cancelToken)));
		}

		public static Task<TRes[]> ForEachAsync<T, TRes>(this IEnumerable<T> source,Func<T,TRes> func,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (func == null) throw new ArgumentNullException(nameof(func));

			return Task.WhenAll(source.Select(item => Task.Run(() => func(item),cancelToken)));
		}

		public static Task<TRes[]> ForEachAsync<T, TRes>(this IEnumerable<T> source,Func<T,int,TRes> func,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (func == null) throw new ArgumentNullException(nameof(func));

			return Task.WhenAll(source.Select((item,idx) => Task.Run(() => func(item,idx),cancelToken)));
		}

		public static Task ForEachLimitAsync<T>(this IEnumerable<T> source,Func<T,Task> asyncFunc,int degreeOfParal = 0,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (degreeOfParal <= 0)
				degreeOfParal = Environment.ProcessorCount;

			return Task.WhenAll(Partitioner.Create(source).GetPartitions(degreeOfParal).Select(p =>
				Task.Run(async () =>
				{
					using (p)
					{
						while (p.MoveNext())
						{
							await asyncFunc(p.Current).ConfigureAwait(false);
						}
					}
				},cancelToken)));
		}

		public static Task ForEachLimitAsync<T>(this IEnumerable<T> source,Func<T,int,Task> asyncFunc,int degreeOfParal = 0,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (degreeOfParal <= 0)
				degreeOfParal = Environment.ProcessorCount;

			var items = source.Select((x,i) => new
			{
				SrcItem = x,
				Index = i
			});

			return Task.WhenAll(Partitioner.Create(items).GetPartitions(degreeOfParal).Select(p =>
				Task.Run(async () =>
				{
					using (p)
					{
						while (p.MoveNext())
						{
							await asyncFunc(p.Current.SrcItem,p.Current.Index).ConfigureAwait(false);
						}
					}
				},cancelToken)));
		}

		public static async Task<TRes[]> ForEachLimitAsync<T, TRes>(this IEnumerable<T> source,Func<T,Task<TRes>> asyncFunc,int degreeOfParal = 0,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (degreeOfParal <= 0) degreeOfParal = Environment.ProcessorCount;

			return (await Task.WhenAll(Partitioner.Create(source).GetPartitions(degreeOfParal).Select(p =>
				Task.Run(async () =>
				{
					var list = new List<TRes>();

					using (p)
					{
						while (p.MoveNext())
						{
							list.Add(await asyncFunc(p.Current).ConfigureAwait(false));
						}
					}

					return list;
				},cancelToken)))).SelectMany(l => l).ToArray();
		}

		public static async Task<TRes[]> ForEachLimitAsync<T, TRes>(this IEnumerable<T> source,Func<T,int,Task<TRes>> asyncFunc,int degreeOfParal = 0,CancellationToken cancelToken = default)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

			if (degreeOfParal <= 0) degreeOfParal = Environment.ProcessorCount;

			var items = source.Select((x,i) => new
			{
				SrcItem = x,
				Index = i
			});

			return (await Task.WhenAll(Partitioner.Create(items).GetPartitions(degreeOfParal).Select(p =>
				Task.Run(async () =>
				{
					var list = new List<TRes>();

					using (p)
					{
						while (p.MoveNext())
						{
							list.Add(await asyncFunc(p.Current.SrcItem,p.Current.Index).ConfigureAwait(false));
						}
					}

					return list;
				},cancelToken)))).SelectMany(l => l).ToArray();
		}
	}
}
