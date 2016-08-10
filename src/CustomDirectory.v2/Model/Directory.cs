using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class IPPhoneDirectory
    {
        public Cluster Cluster { get; set; }
        public int EntriesCount { get; set; }
        public List<IPPhoneDirectoryEntry> DirectoryEntries { get; set; }
    }

    public class IPPhoneDirectoryEntry
    {
        public string Name { get; set; }
        public string Telephone { get; set; }

        public string ToString()
        {
            return "<DirectoryEntry>" + Environment.NewLine +
                   "<Name>" + Name + "</Name>" + Environment.NewLine +
                   "<Telephone>" + Telephone + "</Telephone>" + Environment.NewLine +
                   "</DirectoryEntry>" + Environment.NewLine;
        }
    }

    public enum SoftKey
    {
        Cancel,
        Dial,
        EditDial,
        Exit,
        Next,
        Search
    }
}