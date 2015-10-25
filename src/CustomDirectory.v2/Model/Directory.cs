using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class IPDirectory
    {
        public string Country { get; set; }
        public List<IPDirectoryEntry> DirectoryEntries { get; set; }
    }

    public class IPDirectoryEntry
    {
        public string Name { get; set; }
        public string Telephone { get; set; }
    }
}