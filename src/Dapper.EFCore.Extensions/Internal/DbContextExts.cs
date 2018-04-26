// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation;
using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
using Remotion.Linq.Parsing.Structure;
using Remotion.Linq.Parsing.Structure.ExpressionTreeProcessors;

namespace Dapper.Internal
{
	internal static partial class DbContextExts
	{
		// Based on the article: https://weblogs.asp.net/dixin/entity-framework-core-and-linq-to-entities-5-query-translation-implementation
		public static (SelectExpression, IReadOnlyDictionary<string,object>) Compile(
			this DbContext dbContext,Expression linqExp,bool extractParams = true)
		{
			if (linqExp == null)
				throw new ArgumentNullException(nameof(linqExp));

			var evalExpFilter = (dbContext ?? throw new ArgumentNullException(nameof(dbContext)))
				.GetService<IEvaluatableExpressionFilter>();

			QueryContext queryContext = null;

			if (extractParams)
			{
				queryContext = dbContext.GetService<IQueryContextFactory>().Create();

				linqExp = new ParameterExtractingExpressionVisitor(
					evaluatableExpressionFilter: evalExpFilter,
					parameterValues: queryContext,
					logger: dbContext.GetService<IDiagnosticsLogger<DbLoggerCategory.Query>>(),
					parameterize: true).ExtractParameters(linqExp);
			}

			var queryParser = new QueryParser(new ExpressionTreeParser(
				nodeTypeProvider: dbContext.GetService<INodeTypeProviderFactory>().Create(),
				processor: new CompoundExpressionTreeProcessor(new IExpressionTreeProcessor[]
				{
					new PartialEvaluatingExpressionTreeProcessor(evalExpFilter),
					new TransformingExpressionTreeProcessor(ExpressionTransformerRegistry.CreateDefault())
				})));

			var queryModel = queryParser.GetParsedQuery(linqExp);
			var resultType = queryModel.GetResultType();

			if (resultType.IsConstructedGenericType && resultType.IsAssignableTo(typeof(IQueryable<>)))
				resultType = resultType.GenericTypeArguments.Single();

			var compilationContext = dbContext.GetService<IQueryCompilationContextFactory>().Create(async: false);

			var queryModelVisitor = (RelationalQueryModelVisitor)compilationContext.CreateQueryModelVisitor();

			queryModelVisitor.GetType()
				.GetMethod(nameof(RelationalQueryModelVisitor.CreateQueryExecutor))
				.MakeGenericMethod(resultType)
				.Invoke(queryModelVisitor,new object[] { queryModel });

			var selectExp = queryModelVisitor.TryGetQuery(queryModel.MainFromClause);
			selectExp.QuerySource = queryModel.MainFromClause;
			return (selectExp, queryContext?.ParameterValues ?? new Dictionary<string,object>());
		}

		public static string GetWhereSql(this DbContext dbContext,SelectExpression selExp)
		{
			if (dbContext == null)
				throw new ArgumentNullException(nameof(dbContext));

			if (selExp == null)
				return "";

			var sql = selExp.ToString();
			var pos = sql.IndexOf("WHERE");

			if (pos < 0)
				return "";

			sql = sql.Substring(pos);

			var alias = selExp.ProjectStarTable?.Alias;

			if (string.IsNullOrEmpty(alias))
				return sql;

			var delimAlias = dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(alias);

			return sql.Replace(delimAlias+".","");
		}

		public static DbContext GetDbContext<TEntity>(this DbSet<TEntity> dbSet)
			where TEntity : class =>
			((ICurrentDbContext)((IInfrastructure<IServiceProvider>)(dbSet ?? throw new ArgumentNullException(nameof(dbSet))))
				.Instance.GetService(typeof(ICurrentDbContext)))
				.Context;

		public static DbConnection GetDbConnection(this DbContext dbCtx) =>
			(dbCtx ?? throw new ArgumentNullException(nameof(dbCtx))).Database.GetDbConnection();

		public static DbTransaction GetDbTransaction(this DbContext dbCtx) =>
			(dbCtx ?? throw new ArgumentNullException(nameof(dbCtx))).Database.CurrentTransaction?.GetDbTransaction();

		public static int? GetCommandTimeout(this DbContext dbCtx) =>
			(dbCtx ?? throw new ArgumentNullException(nameof(dbCtx))).Database.GetCommandTimeout();

		public static (DbConnection, DbTransaction, int?) GetDatabaseConfig(this DbContext dbCtx) =>
			((dbCtx ?? throw new ArgumentNullException(nameof(dbCtx))).Database.GetDbConnection(),
				dbCtx.Database.CurrentTransaction?.GetDbTransaction(),
				dbCtx.Database.GetCommandTimeout());

		public static string GetTableName(this DbContext dbCtx,Type type)
		{
			var annotations = (dbCtx ?? throw new ArgumentNullException(nameof(dbCtx)))
				.Model.FindEntityType(type
				?? throw new ArgumentNullException(nameof(type)))?.Relational()
				??	throw new InvalidOperationException("Annotations not found");

			var schema = annotations.Schema ?? "dbo";
			return "["+schema+"].["+annotations.TableName+"]";
		}
	}
}
