// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dapper.Internal
{
    internal static class EnumerableExts
    {
		public static IList<IEnumerator<T>> GetPartitionsBySize<T>(this IEnumerable<T> src,int partSize)
		{
			if (src == null) throw new ArgumentNullException("src");

			if (partSize <= 0)	throw new ArgumentOutOfRangeException("partSize");

			var arr = src as IList<T> ?? src.ToArray();			

			int partCount = (int)Math.Ceiling((double)arr.Count/partSize);
			var enumList = new List<IEnumerator<T>>(partCount);

			for (int i = 0; i < arr.Count; i += partSize)
			{				
				enumList.Add(GetEnumerator(arr,i,partSize));
			}

			return enumList;
		}

		private static IEnumerator<T> GetEnumerator<T>(IList<T> array,int start,int len)
		{
			var arrLen = array.Count;

			if (start > arrLen)
				yield break;
			
			var end = Math.Min(start + len,arrLen);

			for (int i = start; i < end; ++i)
			{
				yield return array[i];
			}
		}

		public static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> src)
		{
			if (src == null)
				throw new ArgumentNullException("src");

			return new Iterator<T>(src);
		}

		public class Iterator<T> : IEnumerable<T>
		{
			private IEnumerator<T> _iterator;

			public Iterator(IEnumerator<T> iter)
			{
				_iterator = iter;
			}

			#region IEnumerable<T> Members			

			public IEnumerator<T> GetEnumerator()
			{
				var iterator = _iterator ?? throw new InvalidOperationException("Can't iterate more than once.");
				_iterator = null;

				return iterator;				
			}

			#endregion

			#region IEnumerable Members

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();			

			#endregion
		}
	}
}
