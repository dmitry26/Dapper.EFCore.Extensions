using System;
using System.Collections.Generic;

namespace Test.Models
{
    public partial class User
    {
        public User()
        {
            Blogs = new HashSet<Blog>();
        }

        public int Id { get; set; }
        public string Name1 { get; set; }
        public DateTime DateCreate { get; set; }
        public Gender Gender { get; set; }

        public ICollection<Blog> Blogs { get; set; }
    }
}
