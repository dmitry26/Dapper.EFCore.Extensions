// Copyright (c) DMO Consulting LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Dapper.Internal;

namespace Dapper.Extensions
{
	public static partial class DapperEFCoreExts
	{
		/// <summary>
		/// Inserts a row into the DB.
		/// </summary>
		/// <typeparam name="TEntity">The type of entity to insert.</typeparam>
		/// <param name="dbSet">The <see cref="DbSet{T}"/>.</param>
		/// <param name="entity">The entity instance.</param>
		/// <param name="returnIdentity">Retrieve identity.</param>
		/// <param name="propertyKey">The name of the identity property.</param>
		/// <returns>The Id of the inserted row or null.</returns>
		public static decimal? Insert<TEntity>(this DbSet<TEntity> dbSet,object entity,bool returnIdentity = true,string propertyKey = "Id")
			where TEntity : class, new() =>
			(dbSet ?? throw new ArgumentNullException(nameof(dbSet)))
				.GetDbContext().Insert<TEntity>(entity,returnIdentity,propertyKey);

		/// <summary>
		/// Inserts a row into the DB.
		/// </summary>
		/// <typeparam name="TEntity">The type of entity to insert.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="entity">The entity instance.</param>
		/// <param name="returnIdentity">Retrieve identity.</param>
		/// <param name="propertyKey">The name of the identity property.</param>
		/// <returns>The Id of the inserted row or null.</returns>
		public static decimal? Insert<TEntity>(this DbContext dbContext,object entity,bool returnIdentity = true,string propertyKey = "Id")
			where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
			if (entity == null) throw new ArgumentNullException(nameof(entity));

			var sw = LogStart();

			var (sql, sqlParams) = dbContext.CompileInsertOper<TEntity>(entity);

			if (sw != null) LogElapsed(sw,"compiled");

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();

			var result = con.ExecuteScalar<object>(sql,sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,"executed");

			if (returnIdentity && result != null)
			{
				var propInfo = entity.GetType().GetProperty(propertyKey);

				if (propInfo != null && propInfo.CanWrite)
					propInfo.SetValue(entity,Convert.ChangeType(result,propInfo.PropertyType));
			}

			if (sw != null) LogElapsed(sw,"retrieved identity");

			return (decimal?)((result == null) ? result : Convert.ChangeType(result,typeof(decimal)));
		}

		private static (string, DynamicParameters) CompileInsertOper<TEntity>(this DbContext dbContext,object entity)
		  where TEntity : class, new()
		{
			var entry = dbContext.CreateEntry(new TEntity(),EntityState.Added);
			var entityAnnotations = entry.EntityType.Relational();
			var cmd = new CustomModifCommand(entityAnnotations.TableName,entityAnnotations.Schema,entry,entity,EntityState.Added);
			var sqlGen = dbContext.GetService<IUpdateSqlGenerator>();
			var sb = new StringBuilder();
			sqlGen.AppendInsertOperation(sb,cmd,0);

			var parameters = new DynamicParameters();
			AddParameters(cmd,parameters);

			return (sb.ToString(), parameters);
		}

		/// <summary>
		///  Inserts multiple rows (in a single trip) asynchronously.
		/// </summary>
		/// <typeparam name="TEntity">The type of entity to insert.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="entities">The entity instances.</param>
		/// <param name="batchSize">Number of rows in each batch. </param>
		/// <param name="cancelToken">A cancellation token that should be used to cancel the work.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Insert<TEntity>(this DbContext dbContext,IEnumerable<object> entities,int batchSize = 2000,CancellationToken cancelToken = default)
		  where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
			if (entities == null) throw new ArgumentNullException(nameof(entities));

			var sw = LogStart();
			int res = 0;

			foreach (var (batch, idx) in entities.GetPartitionsBySize(batchSize).Select((x,idx) => (x.AsEnumerable(), idx)))
			{
				cancelToken.ThrowIfCancellationRequested();
				res += dbContext.InsertIntern<TEntity>(batch,idx);
			}

			if (sw != null) LogElapsed(sw,"executed");

			return res;
		}

		private static int InsertIntern<TEntity>(this DbContext dbContext,IEnumerable<object> entities,int batchIdx)
		  where TEntity : class, new()
		{
			var sw = LogStart();

			var (sql, sqlParams) = dbContext.CompileInsertOper<TEntity>(entities);

			if (sw != null) LogElapsed(sw,$"compiled, batch #{batchIdx}");

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();

			var result = con.Execute(sql,sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,$"executed, batch #{batchIdx}");

			return result;
		}

		private static (string, DynamicParameters) CompileInsertOper<TEntity>(this DbContext dbContext,IEnumerable<object> entities)
		  where TEntity : class, new()
		{
			var sb = new StringBuilder();
			var sqlGen = dbContext.GetService<IUpdateSqlGenerator>();
			var parameters = new DynamicParameters();
			Func<string> paramNameGen = new ParameterNameGenerator().GenerateNext;
			var idx = 0;
			var entityAnnotations = dbContext.Model.FindEntityType(typeof(TEntity)).Relational();

			foreach (var entity in entities)
			{
				var entry = dbContext.CreateEntry(new TEntity(),EntityState.Added);
				var cmd = new CustomModifCommand(entityAnnotations.TableName,entityAnnotations.Schema,entry,entity,EntityState.Added,paramNameGen,true);
				sqlGen.AppendInsertOperation(sb,cmd,idx++);
				AddParameters(cmd,parameters);
			}

			return (sb.ToString(), parameters);
		}

		/// <summary>
		/// Updates entities that match the given predicate.
		/// </summary>
		/// <typeparam name="TEntity">The type of entities to update.</typeparam>
		/// <param name="dbSet">The <see cref="DbSet{T}"/>.</param>
		/// <param name="entity">The entity instance.</param>
		/// <param name="predicate">The predicate expression for the condition in WHERE clause.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Update<TEntity>(this DbSet<TEntity> dbSet,object entity,Expression<Func<TEntity,bool>> predicate = null)
			where TEntity : class, new() =>
			(dbSet ?? throw new ArgumentNullException(nameof(dbSet)))
				.GetDbContext().Update(entity,predicate);

		/// <summary>
		/// Updates entities that match the given predicate.
		/// </summary>
		/// <typeparam name="TEntity">The type of entities to update.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="entity">The entity instance.</param>
		/// <param name="predicate">The predicate expression for the condition in WHERE clause.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Update<TEntity>(this DbContext dbContext,object entity,Expression<Func<TEntity,bool>> predicate = null)
			where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
			if (entity == null) throw new ArgumentNullException(nameof(entity));

			var sw = LogStart();

			var (sb, sqlParams) = dbContext.CompileUpdateOper<TEntity>(entity);

			if (sw != null) LogElapsed(sw,"compiled");

			if (predicate != null)
			{
				var whereSql = GenerateWhereSql(dbContext,sqlParams,predicate);
				AppendOrReplaceWhereClause(dbContext,sb,whereSql,"UPDATE ");

				if (sw != null) LogElapsed(sw,"processed WHERE");
			}

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();

			var result = con.Execute(sb.ToString(),sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,"executed");

			return result;
		}

		private static (StringBuilder, DynamicParameters) CompileUpdateOper<TEntity>(this DbContext dbContext,object entity)
		  where TEntity : class, new()
		{
			var entry = dbContext.CreateEntry(new TEntity(),EntityState.Detached);
			var entityAnnotations = entry.EntityType.Relational();
			var cmd = new CustomModifCommand(entityAnnotations.TableName,entityAnnotations.Schema,entry,entity,EntityState.Modified);
			var sqlGen = dbContext.GetService<IUpdateSqlGenerator>();
			var sb = new StringBuilder();
			sqlGen.AppendUpdateOperation(sb,cmd,0);
			var parameters = new DynamicParameters();
			AddParameters(cmd,parameters);

			return (sb, parameters);
		}

		/// <summary>
		///  Updates multiple rows (in a single trip).
		/// </summary>
		/// <typeparam name="TEntity">The type of entity to insert.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="entities">The entity instances.</param>
		/// <param name="batchSize">Number of rows in each batch. </param>
		/// <param name="cancelToken">A cancellation token that should be used to cancel the work.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Update<TEntity>(this DbContext dbContext,IEnumerable<object> entities,int batchSize = 2000,CancellationToken cancelToken = default)
		  where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
			if (entities == null) throw new ArgumentNullException(nameof(entities));

			var sw = LogStart();
			int res = 0;

			foreach (var (batch, idx) in entities.GetPartitionsBySize(batchSize).Select((x,idx) => (x.AsEnumerable(), idx)))
			{
				cancelToken.ThrowIfCancellationRequested();
				res += dbContext.UpdateIntern<TEntity>(batch,idx);
			}

			if (sw != null) LogElapsed(sw,"executed");

			return res;
		}

		private static int UpdateIntern<TEntity>(this DbContext dbContext,IEnumerable<object> entities,int batchIdx)
		  where TEntity : class, new()
		{
			var sw = LogStart();

			var (sql, sqlParams) = dbContext.CompileUpdateOper<TEntity>(entities);

			if (sw != null) LogElapsed(sw,$"compiled, batch #{batchIdx}");

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();

			var result = con.Execute(sql,sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,$"executed, batch #{batchIdx}");

			return result;
		}

		private static (string, DynamicParameters) CompileUpdateOper<TEntity>(this DbContext dbContext,IEnumerable<object> entities)
		  where TEntity : class, new()
		{
			var sb = new StringBuilder();
			var sqlGen = dbContext.GetService<IUpdateSqlGenerator>();
			var parameters = new DynamicParameters();
			Func<string> paramNameGen = new ParameterNameGenerator().GenerateNext;
			var idx = 0;
			var entityAnnotations = dbContext.Model.FindEntityType(typeof(TEntity)).Relational();

			foreach (var entity in entities)
			{
				var entry = dbContext.CreateEntry(new TEntity(),EntityState.Detached);
				var cmd = new CustomModifCommand(entityAnnotations.TableName,entityAnnotations.Schema,entry,entity,EntityState.Modified,paramNameGen,true);
				sqlGen.AppendUpdateOperation(sb,cmd,idx++);
				AddParameters(cmd,parameters);
			}

			return (sb.ToString(), parameters);
		}

		/// <summary>
		/// Deletes entities that match the given predicate.
		/// </summary>
		/// <typeparam name="TEntity">The type of entity to delete.</typeparam>
		/// <param name="dbSet">The <see cref="DbSet{T}"/>.</param>
		/// <param name="predicate">The predicate expression for the condition in WHERE clause.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Delete<TEntity>(this DbSet<TEntity> dbSet,Expression<Func<TEntity,bool>> predicate = null)
			where TEntity : class, new() =>
			(dbSet ?? throw new ArgumentNullException(nameof(dbSet)))
				.GetDbContext().Delete(predicate);

		/// <summary>
		///  Deletes entities that match the given predicate.
		/// </summary>
		/// <typeparam name="TEntity">The type of entities to delete.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="predicate">The predicate expression for the condition in WHERE clause.</param>
		/// <returns>The number of rows affected.</returns>
		public static int Delete<TEntity>(this DbContext dbContext,Expression<Func<TEntity,bool>> predicate = null)
			where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));

			var sw = LogStart();

			var (sb, sqlParams) = dbContext.CompileDeleteOper<TEntity>();

			if (sw != null) LogElapsed(sw,"compiled");

			if (predicate != null)
			{
				var whereSql = GenerateWhereSql(dbContext,sqlParams,predicate);
				AppendOrReplaceWhereClause(dbContext,sb,whereSql,"DELETE ");

				if (sw != null) LogElapsed(sw,"processed WHERE");
			}

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();
			var result = con.Execute(sb.ToString(),sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,"executed");

			return result;
		}

		/// <summary>
		/// Deletes all entities
		/// </summary>
		/// <typeparam name="TEntity">The type of entities to delete.</typeparam>
		/// <param name="dbSet">The <see cref="DbSet{T}"/>.</param>
		/// <returns></returns>
		public static int DeleteAll<TEntity>(this DbSet<TEntity> dbSet)
			where TEntity : class, new() =>
			(dbSet ?? throw new ArgumentNullException(nameof(dbSet)))
				.GetDbContext().DeleteAll<TEntity>();

		/// <summary>
		/// Deletes all entities
		/// </summary>
		/// <typeparam name="TEntity">The type of entities to delete.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <returns></returns>
		public static int DeleteAll<TEntity>(this DbContext dbContext)
			where TEntity : class, new()
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));

			var sw = LogStart();

			var (sb, sqlParams) = dbContext.CompileDeleteOper<TEntity>();

			if (sw != null) LogElapsed(sw,"compiled");

			RemoveWhereClause(dbContext,sb,"DELETE ");

			if (sw != null) LogElapsed(sw,"removed WHERE");

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();
			var result = con.Execute(sb.ToString(),sqlParams,transaction: trans,commandTimeout: timeout);

			if (sw != null) LogElapsed(sw,"executed");

			return result;
		}

		private static (StringBuilder, DynamicParameters) CompileDeleteOper<TEntity>(this DbContext dbContext)
		  where TEntity : class, new()
		{
			var entry = dbContext.CreateEntry(new TEntity(),EntityState.Deleted);
			var entityAnnotations = entry.EntityType.Relational();
			var cmd = new CustomModifCommand(entityAnnotations.TableName,entityAnnotations.Schema,entry,null,EntityState.Deleted);
			var sqlGen = dbContext.GetService<IUpdateSqlGenerator>();
			var sb = new StringBuilder();
			sqlGen.AppendDeleteOperation(sb,cmd,0);
			var parameters = new DynamicParameters();
			AddParameters(cmd,parameters);

			return (sb, parameters);
		}

		/// <summary>
		/// Executes a Query that returns TResult type Objects
		/// </summary>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TResult>(this DbContext dbContext,IQueryable<TResult> query) =>
			QueryIntern<object,object,object,object,object,object,object,TResult>(dbContext,query,null);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TResult>
			(this DbContext dbContext,IQueryable<TResult> query,Func<TEntity1,TEntity2,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,object,object,object,object,object,TResult>(dbContext,query,selector);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity3">The third type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TEntity3, TResult>
			(this DbContext dbContext,IQueryable<TResult> query,Func<TEntity1,TEntity2,TEntity3,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,TEntity3,object,object,object,object,TResult>(dbContext,query,selector);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity3">The third type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity4">The fourth type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TEntity3, TEntity4, TResult>
			(this DbContext dbContext,IQueryable<TResult> query
				,Func<TEntity1,TEntity2,TEntity3,TEntity4,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,TEntity3,TEntity4,object,object,object,TResult>(dbContext,query,selector);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity3">The third type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity4">The fourth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity5">The fifth type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, TResult>
			(this DbContext dbContext,IQueryable<TResult> query
				,Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,object,object,TResult>(dbContext,query,selector);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity3">The third type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity4">The fourth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity5">The fifth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity6">The sixth type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, TEntity6, TResult>
			(this DbContext dbContext,IQueryable<TResult> query
				,Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,object,TResult>(dbContext,query,selector);

		/// <summary>
		/// Executes a Query that returns multiple entity types per row
		/// </summary>
		/// <typeparam name="TEntity1">The first type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity2">The second type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity3">The third type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity4">The fourth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity5">The fifth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity6">The sixth type of entity in the recordset.</typeparam>
		/// <typeparam name="TEntity7">The seventh type of entity in the recordset.</typeparam>
		/// <typeparam name="TResult">The result type.</typeparam>
		/// <param name="dbContext">The <see cref="DbContext"/>.</param>
		/// <param name="query">An <see cref="IQueryable{T}"/> to use as the base of the raw SQL query.</param>
		/// <param name="selector">A transform function to create a result value from the entities.</param>
		/// <returns>Returns a collection of TResult type Objects.</returns>
		public static IEnumerable<TResult> Query<TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, TEntity6, TEntity7, TResult>
			(this DbContext dbContext,IQueryable<TResult> query
				,Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,TEntity7,TResult> selector) =>
			QueryIntern<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,TEntity7,TResult>(dbContext,query,selector);

		private static IEnumerable<TResult> QueryIntern<TEntity1, TEntity2, TEntity3, TEntity4, TEntity5, TEntity6, TEntity7, TResult>
			(DbContext dbContext,IQueryable<TResult> query,Delegate del)
		{
			_ = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			_ = query ?? throw new ArgumentNullException(nameof(query));

			var sw = LogStart();

			var (selectExp, expParams) = dbContext.Compile(query.Expression);
			var sql = selectExp.ToString();

			if (sw != null) LogElapsed(sw,"compiled");

			var (con, trans, timeout) = dbContext.GetDatabaseConfig();

			IEnumerable<TResult> result = null;

			switch (del)
			{
				case null:
					result = con.Query<TResult>(sql,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TEntity3,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TEntity3,TEntity4,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				case Func<TEntity1,TEntity2,TEntity3,TEntity4,TEntity5,TEntity6,TEntity7,TResult> selector:
					result = con.Query(sql,selector,expParams,transaction: trans,commandTimeout: timeout);
					break;
				default:
					throw new ArgumentException("Invalid Type",nameof(del));
			}

			if (sw != null) LogElapsed(sw,"executed");

			return result;
		}

		private static InternalEntityEntry CreateEntry(this DbContext dbContext,object entity,EntityState entityState)
		{
			var entry = dbContext.GetService<IStateManager>().GetOrCreateEntry(entity);
			entry.SetEntityState(entityState);
			return entry;
		}

		private static InternalEntityEntry CreateEntry(this IStateManager stateManager,object entity,EntityState entityState)
		{
			var entry = stateManager.GetOrCreateEntry(entity);
			entry.SetEntityState(entityState);
			return entry;
		}

		private static string GenerateWhereSql<TEntity>(this DbContext dbContext,DynamicParameters parameters,Expression<Func<TEntity,bool>> predicate)
			where TEntity : class
		{
			var query = dbContext.Set<TEntity>().Where(predicate);
			var (selExp, expPars) = dbContext.Compile(query.Expression);
			parameters.AddDynamicParams(expPars);
			return dbContext.GetWhereSql(selExp);
		}

		private static Stopwatch LogStart() => DapperEFCoreEnv.IsLogEnabled() ? Stopwatch.StartNew() : null;

		private static void LogElapsed(Stopwatch sw,string oper,string message = null)
		{
			var sb = new StringBuilder(128);

			sb.Append("<").Append(sw.ElapsedMilliseconds).Append("ms> ")
				.Append("[").Append(oper).Append("]");

			if (!string.IsNullOrEmpty(message)) sb.Append(message);

			DapperEFCoreEnv.Logger.LogDebug(sb.ToString());
			sw.Restart();
		}

		private static void AddParameters(ModificationCommand modCmd,DynamicParameters parameters)
		{
			foreach (var col in modCmd.ColumnModifications)
			{
				if (col.UseOriginalValueParameter)
					parameters.Add(col.OriginalParameterName,col.OriginalValue);
				else if (col.UseCurrentValueParameter)
					parameters.Add(col.ParameterName,col.Value);
			}
		}

		private static StringBuilder AppendOrReplaceWhereClause(DbContext dbCtx,StringBuilder sb,string whereSql,string key)
		{
			var genHelper = dbCtx.GetService<ISqlGenerationHelper>();
			var terminator = genHelper.StatementTerminator;
			var sql = sb.ToString();
			sb.Clear();
			var statements = sql.Split(new string[] { terminator },StringSplitOptions.RemoveEmptyEntries);
			var found = false;
			var appendTerminator = false;

			foreach (var statement in statements)
			{
				if (appendTerminator)
					sb.Append(terminator);
				else
					appendTerminator = true;

				if (!found && statement.IndexOf(key) >= 0)
				{
					found = true;
					var idx = statement.LastIndexOf("WHERE",StringComparison.InvariantCultureIgnoreCase);

					if (idx > 0)
						sb.Append(statement,0,idx);
					else
						sb.Append(statement).AppendLine();

					sb.Append(whereSql);
				}
				else
					sb.Append(statement);
			}

			if (!found)
				throw new ArgumentNullException("Invalid statement");

			return sb;
		}

		private static StringBuilder RemoveWhereClause(DbContext dbCtx,StringBuilder sb,string key)
		{
			var genHelper = dbCtx.GetService<ISqlGenerationHelper>();
			var terminator = genHelper.StatementTerminator;
			var sql = sb.ToString();
			sb.Clear();
			var statements = sql.Split(new string[] { terminator },StringSplitOptions.RemoveEmptyEntries);
			var found = false;
			var appendTerminator = false;

			foreach (var statement in statements)
			{
				if (appendTerminator)
					sb.Append(terminator);
				else
					appendTerminator = true;

				if (!found && statement.IndexOf(key) >= 0)
				{
					found = true;
					var idx = statement.LastIndexOf("WHERE",StringComparison.InvariantCultureIgnoreCase);

					if (idx > 0)
						sb.Append(statement,0,idx);
					else
						sb.Append(statement).AppendLine();
				}
				else
					sb.Append(statement);
			}

			if (!found)
				throw new ArgumentNullException("Invalid statement");

			return sb;
		}

		public static void SetDapperMapping<TEntity>(this DbContext dbCtx)
		{
			dbCtx.SetDapperMapping(typeof(TEntity));
		}

		public static void SetDapperMapping(this DbContext dbCtx,Type type)
		{
			if (dbCtx == null)
				throw new ArgumentNullException(nameof(dbCtx));

			if (type == null)
				throw new ArgumentNullException(nameof(type));

			lock (typeof(ColumnTypeMapper))
			{
				if (!(SqlMapper.GetTypeMap(type) is ColumnTypeMapper))
					SqlMapper.SetTypeMap(type,new ColumnTypeMapper(dbCtx,type));
			}
		}
	}
}
