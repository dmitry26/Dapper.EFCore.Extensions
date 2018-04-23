using System;
using System.Collections.Generic;

namespace Test.Models
{
    public partial class Blog
    {
        public Blog()
        {
            BlogPosts = new HashSet<BlogPost>();
        }

        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public DateTime DateCreate { get; set; }
        public string Description { get; set; }

        public User User { get; set; }
        public BlogSetting BlogSetting { get; set; }
        public ICollection<BlogPost> BlogPosts { get; set; }
    }
}
