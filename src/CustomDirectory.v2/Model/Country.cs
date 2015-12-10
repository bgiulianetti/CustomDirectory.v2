﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CustomDirectory.v2.Model
{
    public class Country
    {
        public const string INVALID_NAME = "Invalid name";
        public const string INVALID_CODE = "Invalid code";
        public const string INVALID_CLUSTER = "Invalid cluster";

        public Country(string name, string code, List<string> internalPrefix, string externalPrefix, string cluster)
        {
            if (string.IsNullOrEmpty(name))
                throw new Exception(INVALID_NAME);

            if (string.IsNullOrEmpty(code))
                throw new Exception(INVALID_CLUSTER);

            if (string.IsNullOrEmpty(cluster))
                throw new Exception(INVALID_CODE);

            Name = name;
            Code = code;
            InternalPrefix = internalPrefix;
            ExternalPrefix = externalPrefix;
            Cluster = cluster;

        }

        public string Name { get; set; }
        public string Code { get; set; }
        public List<string> InternalPrefix { get; set; }
        public string ExternalPrefix { get; set; }
        public string Cluster { get; set; }
    }
}