using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class Country
    {
        public const string INVALID_NAME = "Invalid name";
        public const string INVALID_CODE = "Invalid code";
        public const string INVALID_PREFIX = "Invalid prefix";

        public Country(string name, string code, string prefix)
        {
            if (string.IsNullOrEmpty(name))
                throw new Exception(INVALID_NAME);

            if (string.IsNullOrEmpty(code))
                throw new Exception(INVALID_CODE);

            if (string.IsNullOrEmpty(prefix))
                throw new Exception(INVALID_PREFIX);

            Name = name;
            Code = code;
            Prefix = prefix;
        }

        public string Name { get; set; }
        public string Code { get; set; }
        public string Prefix { get; set; }
    }
}