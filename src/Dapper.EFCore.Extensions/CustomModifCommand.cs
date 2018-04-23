﻿// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Update.Internal;

namespace Dapper.Internal
{
	internal class CustomModifCommand : ModificationCommand
	{
		public CustomModifCommand(string name,string schema,IUpdateEntry entry,object values,EntityState state)
			: base(name,schema,new ParameterNameGenerator().GenerateNext,false,null)
		{
			_entry = entry;
			_values = values;
			_state = state;			
		}

		private object _values;

		private IReadOnlyList<ColumnModification> _columnModifications;

		public override IReadOnlyList<ColumnModification> ColumnModifications =>
			 NonCapturingLazyInitializer.EnsureInitialized(ref _columnModifications, this,command => command.GenerateColumnModifications());

		private EntityState _state;
		public override EntityState EntityState => _state;

		public override bool RequiresResultPropagation => false;

		private IUpdateEntry _entry;

		public override IReadOnlyList<IUpdateEntry> Entries => new IUpdateEntry[] { _entry };

		public override void AddEntry(IUpdateEntry entry) => throw new NotSupportedException();

		private IReadOnlyList<ColumnModification> GenerateColumnModifications()
		{
			var adding = EntityState == EntityState.Added;
			var columnModifications = new List<ColumnModification>();
			var srcType = _values?.GetType();

			foreach (var property in _entry.EntityType.GetProperties())
			{
				var propertyAnnotations = property.Relational();
				var isKey = property.IsPrimaryKey();
				var isConcurrencyToken = property.IsConcurrencyToken;
				var isCondition = !adding && (isKey || isConcurrencyToken);
				var readValue = _entry.IsStoreGenerated(property);
				var writeValue = false;				
				object value = null;

				var srcProp = srcType?.GetProperty(property.Name);				

				if (!readValue)
				{
					var modified = false;

					if (srcProp != null)
					{
						value = srcProp.GetValue(_values);
						_entry.SetCurrentValue(property,value);
						modified = true;
					}

					if (adding && property.BeforeSaveBehavior == PropertySaveBehavior.Save ||
						property.AfterSaveBehavior == PropertySaveBehavior.Save && modified)
						writeValue = true;					
				}

				if (readValue || writeValue	|| isCondition)
				{
					var columnModification = new ColumnModification(
						_entry,
						property,
						propertyAnnotations,
						() => GenerateParameterName(property.Name,isCondition && isConcurrencyToken),
						readValue,
						writeValue,
						isKey,
						isCondition,
						isConcurrencyToken);

					columnModifications.Add(columnModification);
				}
			}

			return columnModifications;
		}

		private string GenerateParameterName(string name,bool origParam)
		{
			return origParam ? "org_" + name : name;
		}
	}	
}
