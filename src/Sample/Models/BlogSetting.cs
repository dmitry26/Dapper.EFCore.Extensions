using System;
using System.Collections.Generic;

namespace Test.Models
{
    public partial class BlogSetting
    {
        public int BlogId { get; set; }
        public bool AutoSave { get; set; }
        public bool AutoPost { get; set; }

        public Blog Blog { get; set; }
    }
}
