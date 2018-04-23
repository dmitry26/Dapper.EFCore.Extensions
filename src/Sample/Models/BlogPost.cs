using System;
using System.Collections.Generic;

namespace Test.Models
{
    public partial class BlogPost
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public string Body { get; set; }
        public DateTime DatePublication { get; set; }

        public Blog Blog { get; set; }
    }
}
