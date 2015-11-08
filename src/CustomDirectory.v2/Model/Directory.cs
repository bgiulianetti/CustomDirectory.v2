using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class IPPhoneDirectory
    {
        public Country Country { get; set; }
        public int EntriesCount { get; set; }
        public List<IPPhoneDirectoryEntry> DirectoryEntries { get; set; }
    }

    public class IPPhoneDirectoryEntry
    {
        public string Name { get; set; }
        public string Telephone { get; set; }
    }
}