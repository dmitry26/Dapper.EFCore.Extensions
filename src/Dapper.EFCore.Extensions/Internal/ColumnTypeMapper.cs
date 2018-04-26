// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Dapper.Internal
{
	internal class ColumnTypeMapper : SqlMapper.ITypeMap
	{
		private CustomPropertyTypeMap _mapper;
		private Dictionary<string,string> _columnMap;

		public ColumnTypeMapper(DbContext dbContext,Type type)
		{
			_columnMap = GetEFMapping(dbContext,type);
			_mapper = new CustomPropertyTypeMap(type,SelectProperty);
		}

		public ConstructorInfo FindConstructor(string[] names,Type[] types) => _mapper.FindConstructor(names,types);
		public ConstructorInfo FindExplicitConstructor() => _mapper.FindExplicitConstructor();
		public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor,string columnName) => _mapper.GetConstructorParameter(constructor,columnName);
		public SqlMapper.IMemberMap GetMember(string columnName) => _mapper.GetMember(columnName);

		private PropertyInfo SelectProperty(Type objType,string columnName) =>
			objType.GetProperty(_columnMap == null ? columnName
				: (_columnMap.TryGetValue(columnName,out string propName) ? propName : columnName));

		private Dictionary<string,string> GetEFMapping(DbContext dbContext,Type objType)
		{
			var entityType = dbContext.Model.FindEntityType(objType);
			var dict = entityType.GetProperties().Select(p => new { p.Name,p.Relational()?.ColumnName })
				.Where(p => p.ColumnName != null && p.Name != p.ColumnName).ToDictionary(x => x.ColumnName,x => x.Name);

			return dict.Count > 0 ? dict : null;
		}
	}
}
