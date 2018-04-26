using System;
using System.Diagnostics;
using Test.Models;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Dapper.Extensions;
using System.Linq;
using System.Collections.Generic;
using Dmo.Hosting.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Serilog;
using Serilog.Extensions.Logging;

namespace Sample
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				ConfigApp();
				Tests();
				TestsEF();
			}
			catch (Exception x)
			{
				Console.WriteLine(x);
			}

			Console.WriteLine();
			Console.WriteLine("Done!");

			Console.ReadLine();
		}

		private static readonly LoggerFactory _efLoggerFactory
			= new LoggerFactory(new[] { new SerilogLoggerProvider(Log.Logger,false) });

		private static Stopwatch _watch = new Stopwatch();

		private static DbContextOptions<BloggingContext> _dbCtxOpts;

		private static Microsoft.Extensions.Logging.ILogger _operLogger;
		private static Microsoft.Extensions.Logging.ILogger _genLogger;


		private static void ConfigApp()
		{
			var appSettings = HostBuilderExts.GetAppSettings();

			Log.Logger = new LoggerConfiguration()
				.ReadFrom.Configuration(appSettings)
				.CreateLogger();

			var logFactory = new LoggerFactory();
			logFactory.AddSerilog(Log.Logger);
			DapperEFCoreEnv.Logger = logFactory.CreateLogger("Dapper.EFCoreExt");
			_operLogger = logFactory.CreateLogger("Program");
			_genLogger = logFactory.CreateLogger("");

			var conStr = appSettings.GetConnectionString("BloggingDB");
			var optionBuilder = new DbContextOptionsBuilder<BloggingContext>();
			_dbCtxOpts = optionBuilder.UseNpgsql(conStr)
				 //.UseLoggerFactory(_efLoggerFactory)
				 .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
				 .Options;
		}

		public static void Tests()
		{
			LogNewLine();
			LogMsg("##### EXTENSIONS");
			LogNewLine();
			Insert();
			Insert();
			InsertAsync().GetAwaiter().GetResult();
			Update();
			UpdateAsync().GetAwaiter().GetResult();
			UpdateAsync().GetAwaiter().GetResult();
			Delete();
			DeleteAsync().GetAwaiter().GetResult();
			Select();
			Select();
			SelectSync();
			SelectAsync().GetAwaiter().GetResult();
			SelectAsync().GetAwaiter().GetResult();
		}

		#region Tests Extensions
		public static void Insert()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","Insert"))
			{
				var batchSW = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					var operSW = new Stopwatch();

					using (_operLogger.BeginScope("{OperId}",".Insert1"))
					{
						LogStarted(operSW);

						var user = new User
						{
							Name1 = "User Insert 123",
							DateCreate = DateTime.Now,
							Gender = Gender.Female,
							Id = 3
						};

						var res = ctx.Insert<User>(user);

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert2 (with anonymous)"))
					{
						LogStarted(operSW);

						var obj = new
						{
							Name1 = "User Insert 123",
							DateCreate = DateTime.Now,
							Gender = Gender.Female
						};

						var res = ctx.Insert<User>(obj);
						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert3"))
					{
						LogStarted(operSW);

						var user = new User()
						{
							Name1 = "User Insert 345",
							DateCreate = DateTime.Now,
							Gender = Gender.Male
						};

						var res = ctx.Insert<User>(user);
						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert4 (without return identity)"))
					{
						LogStarted(operSW);

						var user = new User()
						{
							Name1 = "User Insert 345",
							DateCreate = DateTime.Now,
							Gender = Gender.Male
						};

						var res = ctx.Insert<User>(user,returnIdentity: false);

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert5 (without return identity)"))
					{
						LogStarted(operSW);

						var user = new User()
						{
							Name1 = "User Insert 345",
							DateCreate = DateTime.Now,
							Gender = Gender.Male
						};

						var res = ctx.Insert<User>(user,propertyKey: "User_Id");

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert6 (with class)"))
					{
						LogStarted(operSW);

						var blog = new Blog
						{
							Name = "Blog1",
							UserId = 2,
							Description = "Blog1 desc",
							DateCreate = DateTime.Now
						};

						var res = ctx.Insert<Blog>(blog);
						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert7 (with anonymous and DbSet)"))
					{
						LogStarted(operSW);

						var obj = new
						{
							Name1 = "User Insert 123",
							DateCreate = DateTime.Now,
							Gender = Gender.Female
						};

						var res = ctx.Users.Insert(obj);
						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Insert8 (bulk)"))
					{
						LogStarted(operSW);

						var dt = DateTime.Now;

						var objs = new[]
						{
							new {
								Name1 = "User Insert 123",
								  DateCreate = dt,
								  Gender = Gender.Female
							},
							  new {
								Name1 = "User Insert 234",
								  DateCreate = dt,
								  Gender = Gender.Male
							},
							  new {
								Name1 = "User Insert 345",
								  DateCreate = dt,
								  Gender = Gender.Female
							},
						};

						var res = ctx.Insert<User>(objs,2);
						LogElapsed(operSW,"ended",$"res = {res}");
					}
				}

				LogElapsed(batchSW,"ended");
			}
		}

		public static async Task InsertAsync()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","InsertAsync"))
			{
				var tasks = new List<Task>();

				var batchSw = LogStarted();
				var operSw = Stopwatch.StartNew();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.SetDapperMapping<User>();
				}

				LogElapsed(operSw,"set mapping");

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Insert1"))
					{
						var sw = LogStarted();
						decimal? res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var u = new User
							{
								Name1 = "User Insert 123",
								DateCreate = DateTime.Now,
								Gender = Gender.Female,
								Id = 3
							};

							res = await ctx.InsertAsync<User>(u).ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Insert2"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var dt = DateTime.Now;

							var objs = new[]
							{
								new {
									Name1 = "User Insert 123",
									  DateCreate = dt,
									  Gender = Gender.Female
								},
								  new {
									Name1 = "User Insert 234",
									  DateCreate = dt,
									  Gender = Gender.Male
								},
								  new {
									Name1 = "User Insert 345",
									  DateCreate = dt,
									  Gender = Gender.Female
								},
							};

							res = await ctx.InsertAsync<User>(objs,2).ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				await Task.WhenAll(tasks).ConfigureAwait(false);
				LogElapsed(batchSw,"ended");
			}
		}

		public static void Update()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","Update"))
			{
				var batchSW = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					var operSW = new Stopwatch();

					using (_operLogger.BeginScope("{OperId}",".Update1 (with anonymous)"))
					{
						LogStarted(operSW);

						var obj = new
						{
							Gender = Gender.Male,
							Id = 2,
							Name1 = "test2"
						};

						var id = 3;
						var res = ctx.Update<User>(obj,x => x.Id == id && x.Name1 == "teste");

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update2 (with anonymous)"))
					{
						LogStarted(operSW);

						var obj = new
						{
							Gender = Gender.Male,
							Id = 2
						};

						var res = ctx.Update<User>(obj);
						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update3 (with query)"))
					{
						LogStarted(operSW);

						var res = ctx.Update<User>(new
						{
							Gender = Gender.Female
						},o => o.Id == 1);

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update4 (with complex query)"))
					{
						LogStarted(operSW);

						var name = "teste";

						var res = ctx.Update<User>(new
						{
							Gender = Gender.Female
						},o => o.Id == 1 && (o.Name1 == name || o.DateCreate > DateTime.Now));

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update5 (with complex query)"))
					{
						LogStarted(operSW);

						var name = "teste";

						var res = ctx.Update<User>(new
						{
							Gender = Gender.Female
						},o => o.Id == 1 && (o.Name1 == name || o.DateCreate > DateTime.Now));

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update6 (with DbSet)"))
					{
						LogStarted(operSW);

						var name = "teste";

						var res = ctx.Users.Update(new
						{
							Gender = Gender.Female
						},o => o.Id == 1 && (o.Name1 == name || o.DateCreate > DateTime.Now));

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Update7 (bulk)"))
					{
						LogStarted(operSW);

						var objs = new[]
						{
							new
							{
								Gender = Gender.Male,
								  Id = 8,
								  Name1 = "User Insert8"
							},
							  new
							{
								Gender = Gender.Female,
								  Id = 9,
								  Name1 = "User Insert9"
							},
							  new
							{
								Gender = Gender.Female,
								  Id = 10,
								  Name1 = "User Insert10"
							}
						};

						var res = ctx.Update<User>(objs);

						LogElapsed(operSW,"ended",$"res = {res}");
					}
				}

				LogElapsed(batchSW,"ended");
			}
		}

		public static async Task UpdateAsync()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","UpdateAsync"))
			{
				var tasks = new List<Task>();

				var batchSw = LogStarted();
				var operSw = Stopwatch.StartNew();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.SetDapperMapping<User>();
				}

				LogElapsed(operSw,"set mapping");

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Update1"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var obj = new
							{
								Gender = Gender.Male,
								Id = 2,
								Name1 = "test2"
							};

							var id = 3;
							var name = "teste";
							res = await ctx.UpdateAsync<User>(obj,x => x.Id == id && x.Name1 == name).ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Update2"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var obj = new
							{
								Gender = Gender.Male,
								Id = 2,
								Name1 = "teste"
							};

							var id = 3;
							var name = "test2";
							res = await ctx.UpdateAsync<User>(obj,x => x.Id == id && x.Name1 == name).ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Update3 (bulk)"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var objs = new[]
							{
								new
								{
									Gender = Gender.Male,
									  Id = 8,
									  Name1 = "User Insert83"
								},
								  new
								{
									Gender = Gender.Female,
									  Id = 9,
									  Name1 = "User Insert93"
								},
								  new
								{
									Gender = Gender.Female,
									  Id = 10,
									  Name1 = "User Insert103"
								}
							};

							res = await ctx.UpdateAsync<User>(objs,2);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				await Task.WhenAll(tasks).ConfigureAwait(false);
				LogElapsed(batchSw,"ended");
			}
		}

		public static void Delete()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","Delete"))
			{
				var batchSW = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					var operSW = new Stopwatch();

					using (_operLogger.BeginScope("{OperId}",".Delete1 (all)"))
					{
						LogStarted(operSW);

						var res = ctx.DeleteAll<BlogPost>();

						LogElapsed(operSW,"ended",$"res = {res}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Delete2 (with query)"))
					{
						LogStarted(operSW);

						var id = 20;
						var res = ctx.Delete<User>(o => o.Id >= id);

						LogElapsed(operSW,"ended",$"res = {res}");
					}
				}

				LogElapsed(batchSW,"ended");
			}
		}

		public static async Task DeleteAsync()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","DeleteAsync"))
			{
				var tasks = new List<Task>();

				var batchSw = LogStarted();
				var operSw = Stopwatch.StartNew();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.SetDapperMapping<User>();
				}

				LogElapsed(operSw,"set mapping");

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Delete1"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var id = 320;
							res = await ctx.DeleteAsync<User>(o => o.Id >= id).ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Delete2 (all)"))
					{
						var sw = LogStarted();
						int res;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							res = await ctx.DeleteAllAsync<BlogPost>().ConfigureAwait(false);
						}

						LogElapsed(sw,"ended",$"res = {res}");
					}
				}));

				await Task.WhenAll(tasks).ConfigureAwait(false);
				LogElapsed(batchSw,"ended");
			}
		}

		public static void Select()
		{
			var q1 = EF.CompileQuery(
				(BloggingContext ctx) =>
					ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2")
					.OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2));

			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","Select"))
			{
				var batchSW = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.SetDapperMapping<User>();
					var operSW = new Stopwatch();

					using (_operLogger.BeginScope("{OperId}",".Select1a (compiled)"))
					{
						LogStarted(operSW);

						var res = q1(ctx);

						LogElapsed(operSW,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select1"))
					{
						LogStarted(operSW);

						var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2")
							.OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
						var res = ctx.Query(q);

						LogElapsed(operSW,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select2 (with selector)"))
					{
						LogStarted(operSW);

						var q = ctx.Users.Where(o => o.Id > 1).OrderBy(x => x.Id).ThenByDescending(x => x.Name1)
							.Take(2).Select(x => new { x.Id,Name = x.Name1 });
						var res = ctx.Query(q);

						LogElapsed(operSW,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select3"))
					{
						LogStarted(operSW);

						var q = ctx.Blogs.Where(b => b.Name == "Blog2").Join(ctx.Users,o => o.UserId,i => i.Id,
							(o,i) => new { Blog = o,User = i });

						var res = ctx.Query(q,(Blog b,User u) =>
						{
							b.User = u;
							return new { Blog = b,User = u };
						});

						LogElapsed(operSW,"ended",$"count = {res.Count()}");
					}

					var stateSw = new Stopwatch();

					using (_operLogger.BeginScope("{OperId}",".Select4"))
					{
						LogNewLine();
						LogStarted(operSW);

						stateSw.Restart();

						var q = from u in ctx.Users
								join b in ctx.Blogs on u.Id equals b.UserId into bg
								from b in bg.DefaultIfEmpty()
								join p in ctx.BlogPosts on b.Id equals p.BlogId into pg
								from p in pg.DefaultIfEmpty()
								where u.Id == 2
								select new
								{
									User = u,
									Blog = b,
									Post = p
								};

						LogElapsed(stateSw,"created query");

						var res = ctx.Query(q,(User u,Blog b,BlogPost p) =>
						{
							b.User = u;

							if (p != null)
								p.Blog = b;

							return new
							{
								User = u,
								Blog = b,
								Post = p
							};
						});

						stateSw.Restart();

						var users = res.GroupBy(ug => ug.User.Id,(key,uItems) =>
						{
							var item = uItems.First();
							item.User.Blogs = uItems.GroupBy(bg => bg.Blog?.Id,(bid,bItems) =>
							{
								var bitem = bItems.FirstOrDefault();
								if (bitem == null)
									return null;
								bitem.Blog.BlogPosts = bItems.Select(x => x.Post).
									Where(x => x != null).ToArray();
								return bitem.Blog;
							}).Where(x => x != null).ToArray();
							return item.User;
						}).ToArray();

						LogElapsed(stateSw,"processed result");
						LogElapsed(operSW,"ended",$"count = {res.Count()}");
					}
				}

				LogElapsed(batchSW,"ended");
			}
		}

		public static void SelectSync()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","SelectSync"))
			{
				var batchSw = LogStarted();
				var operSw = new Stopwatch();

				using (_operLogger.BeginScope("{OperId}",".Warmup"))
				{
					LogStarted(operSw);

					//warmup
					using (var ctx = new BloggingContext(_dbCtxOpts))
					{
						ctx.SetDapperMapping<User>();
						var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2").OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
						var result = ctx.Query(q);
					}

					LogElapsed(operSw,"ended","ignore timing");
				}

				LogNewLine();
				batchSw.Restart();
				operSw.Restart();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					LogElapsed(operSw,"created context");

					using (_operLogger.BeginScope("{OperId}",".Select1"))
					{
						LogStarted(operSw);

						var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2").OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
						var res = ctx.Query(q);

						LogElapsed(operSw,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select12 (with selector)"))
					{
						LogStarted(operSw);

						var q = ctx.Users.Where(o => o.Id > 1).OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2).Select(x => new { x.Id,Name = x.Name1 });
						var res = ctx.Query(q);

						LogElapsed(operSw,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select13"))
					{
						LogStarted(operSw);

						var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2").OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
						var res = ctx.Query(q);

						LogElapsed(operSw,"ended",$"count = {res.Count()}");
					}

					LogNewLine();

					using (_operLogger.BeginScope("{OperId}",".Select14 (with selector)"))
					{
						LogStarted(operSw);

						var q = ctx.Users.Where(o => o.Id > 1).OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2).Select(x => new { x.Id,Name = x.Name1 });
						var res = ctx.Query(q);

						LogElapsed(operSw,"ended",$"count = {res.Count()}");
					}

					operSw.Restart();
				}

				LogElapsed(operSw,"disposed context");
				LogElapsed(batchSw,"ended");
			}
		}

		public static async Task SelectAsync()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","SelectAsync"))
			{
				var tasks = new List<Task>();

				var batchSw = LogStarted();
				var operSw = Stopwatch.StartNew();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.SetDapperMapping<User>();
				}

				LogElapsed(operSw,"set mapping");

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Select1"))
					{
						var sw = LogStarted();
						int count = 0;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var stateSw = Stopwatch.StartNew();
							var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2").OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
							LogElapsed(stateSw,"created query");
							count = (await ctx.QueryAsync(q).ConfigureAwait(false)).Count();
						}

						LogElapsed(sw,"ended",$"count = {count}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Select2 (with selector)"))
					{
						var sw = LogStarted();
						int count = 0;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var stateSw = Stopwatch.StartNew();
							var q = ctx.Users.Where(o => o.Id > 1).OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2).Select(x => new { x.Id,Name = x.Name1 });
							LogElapsed(stateSw,"created query");
							count = (await ctx.QueryAsync(q).ConfigureAwait(false)).Count();
						}

						LogElapsed(sw,"ended",$"count = {count}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Select3"))
					{
						var sw = LogStarted();
						int count = 0;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var stateSw = Stopwatch.StartNew();
							var q = ctx.Users.Where(o => o.Id > 1 && o.Name1 == "User Insert2").OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2);
							LogElapsed(stateSw,"created query");
							count = (await ctx.QueryAsync(q).ConfigureAwait(false)).Count();
						}

						LogElapsed(sw,"ended",$"count = {count}");
					}
				}));

				tasks.Add(Task.Run(async () =>
				{
					using (_operLogger.BeginScope("{OperId}",".Select4 (with selector)"))
					{
						var sw = LogStarted();
						int count = 0;

						using (var ctx = new BloggingContext(_dbCtxOpts))
						{
							var stateSw = Stopwatch.StartNew();
							var q = ctx.Users.Where(o => o.Id > 1).OrderBy(x => x.Id).ThenByDescending(x => x.Name1).Take(2).Select(x => new { x.Id,Name = x.Name1 });
							LogElapsed(stateSw,"created query");
							var res = (await ctx.QueryAsync(q).ConfigureAwait(false)).Count();
						}

						LogElapsed(sw,"ended",$"count = {count}");
					}
				}));

				await Task.WhenAll(tasks).ConfigureAwait(false);
				LogElapsed(batchSw,"ended");
			}
		}

		#endregion

		private static Stopwatch LogStarted()
		{
			_operLogger.LogDebug("[started]");
			return Stopwatch.StartNew();
		}

		private static void LogStarted(Stopwatch sw)
		{
			_operLogger.LogDebug("[started]");
			sw.Restart();
		}

		private static void LogElapsed(Stopwatch sw,string oper,bool restart = false)
		{
			_operLogger.LogDebug($"<{sw.ElapsedMilliseconds}ms> [{oper}]");

			if (restart) sw.Restart();
		}

		private static void LogElapsed(Stopwatch sw,string oper,string msg,bool restart = false)
		{
			_operLogger.LogDebug($"<{sw.ElapsedMilliseconds}ms> [{oper}] {msg}");

			if (restart) sw.Restart();
		}

		private static void LogNewLine() => _genLogger.LogDebug("");

		private static void LogMsg(string msg) => _genLogger.LogDebug(msg);

		public static void TestsEF()
		{
			LogNewLine();
			LogMsg("##### ENTITYFRAMEWORK");
			WarmupEF();
			InsertEF();
			InsertEF();
			UpdateEF();
		}

		#region Tests EntityFramework

		public static void WarmupEF()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","WarmupEF"))
			{
				var batchSw = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					ctx.Users.Load();
				}

				LogElapsed(batchSw,"ended");
			}
		}

		public static void InsertEF()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","InsertEF"))
			{
				var batchSw = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					using (_operLogger.BeginScope("{OperId}",".Insert1"))
					{
						var operSw = LogStarted();

						ctx.Users.Add(new User() { Name1 = "User Insert",DateCreate = DateTime.Now,Gender = Gender.Female });
						var count = ctx.SaveChanges();

						LogElapsed(operSw,"ended",$"count = {count}");
					}
				}

				LogElapsed(batchSw,"ended");
			}
		}

		public static void UpdateEF()
		{
			LogNewLine();

			using (_operLogger.BeginScope("{BatchId}","UpdateEF"))
			{
				var batchSw = LogStarted();

				using (var ctx = new BloggingContext(_dbCtxOpts))
				{
					using (_operLogger.BeginScope("{OperId}",".Update1"))
					{
						var operSw = LogStarted();

						User user = new User
						{
							Gender = Gender.Male,
							Id = 1,
							Name1 = "teste",
							DateCreate = DateTime.Now
						};

						ctx.Entry(user).State = EntityState.Modified;
						var count = ctx.SaveChanges();

						LogElapsed(operSw,"ended",$"count = {count}");
					}
				}

				LogElapsed(batchSw,"ended");
			}
		}
		#endregion
	}
}
