﻿using System;
using CommandLine;

namespace MigrationTools.CommandLine
{
    public class OptionsBase
    {
        [Option('c', "collection", Required = true, HelpText = "Collection that you want to connect to.")]
        public Uri CollectionURL { get; set; }
    }
}