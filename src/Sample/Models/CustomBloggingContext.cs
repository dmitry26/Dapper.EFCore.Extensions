using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Test.Models
{
	public partial class BloggingContext : DbContext
	{
		public BloggingContext(DbContextOptions<BloggingContext> options) : base(options)
		{
		}
	}
}
