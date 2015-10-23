using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class Directory
    {
        public string Country { get; set; }
        public List<DirectoryEntry> DirectoryEntries { get; set; }
    }

    public class DirectoryEntry
    {
        public string Name { get; set; }
        public string Telephone { get; set; }
    }
}