﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

internal sealed class Arguments
{
    public bool Recursive { get; private set; }
    public string Path { get; private set; }
    public IReadOnlyList<(string MetadataPath, string ILPathOpt)> EncDeltas { get; private set; }
    public HashSet<int> SkipGenerations { get; private set; }
    public bool DisplayStatistics { get; private set; }
    public bool DisplayAssemblyReferences { get; private set; }
    public bool DisplayIL { get; private set; }
    public bool DisplayEmbeddedPdb { get; private set; }
    public bool DisplayMetadata { get; private set; }
    public bool DisplayEmbeddedSource { get; private set; }
    public string OutputPath { get; private set; }
    public ImmutableArray<string> FindRefs { get; private set; }

    public const string Help = @"
Parameters:
<path>                                    Path to a PE file, metadata blob, or a directory. 
                                          The target kind is auto-detected.
/g:<metadata-delta-path>;<il-delta-path>  Add generation delta blobs.    
/sg:<generation #>                        Suppress display of specified generation.
/stats[+|-]                               Display/hide misc statistics.
/assemblyRefs[+|-]                        Display/hide assembly references.
/il[+|-]                                  Display/hide IL of method bodies.
/md[+|-]                                  Display/hide metadata tables.
/embeddedSource[+|-]                      Display/hide embedded source.
/embeddedPdb[+|-]                         Display embedded PDB insted of the type system metadata.
/findRef:<MemberRefs>                     Displays all assemblies containing the specified MemberRefs: 
                                          a semicolon separated list of 
                                          <assembly display name>:<qualified-type-name>:<member-name>
/out:<path>                               Write the output to specified file.

If the target path is a directory displays information for all *.dll, *.exe, *.winmd, 
and *.netmodule files in the directory and all subdirectories.

If /g is specified the path must be baseline PE file (generation 0).
";

    public static Arguments TryParse(string[] args)
    {
        if (args.Length < 1)
        {
            return null;
        }

        var result = new Arguments();
        result.Path = args[0];
        result.Recursive = Directory.Exists(args[0]);

        result.EncDeltas =
            (from arg in args
             where arg.StartsWith("/g:", StringComparison.Ordinal)
             let value = arg.Substring("/g:".Length).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
             select (value.Length >= 1 && value.Length <= 2) ? (value[0], value.Length > 1 ? value[1] : null) : default((string, string))).
             ToArray();

        if (result.EncDeltas.Any(value => value.MetadataPath == null))
        {
            return null;
        }

        result.SkipGenerations = new HashSet<int>(args.Where(a => a.StartsWith("/sg:", StringComparison.OrdinalIgnoreCase)).Select(a => int.Parse(a.Substring("/sg:".Length))));

        if (result.Recursive && (result.EncDeltas.Any() || result.SkipGenerations.Any()))
        {
            return null;
        }

        result.FindRefs = ParseValueArg(args, "findref")?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        bool findRefs = result.FindRefs.Any();

        result.DisplayIL = ParseFlagArg(args, "il", defaultValue: !result.Recursive && !findRefs);
        result.DisplayMetadata = ParseFlagArg(args, "md", defaultValue: !result.Recursive && !findRefs);
        result.DisplayEmbeddedPdb = ParseFlagArg(args, "embeddedPdb", defaultValue: false);
        result.DisplayStatistics = ParseFlagArg(args, "stats", defaultValue: result.Recursive && !findRefs);
        result.DisplayAssemblyReferences = ParseFlagArg(args, "stats", defaultValue: !findRefs);
        result.DisplayEmbeddedSource = ParseFlagArg(args, "embeddedSource", defaultValue: false);
        result.OutputPath = ParseValueArg(args, "out");
       

        if (result.DisplayEmbeddedPdb && result.EncDeltas.Count > 0)
        {
            return null;
        }

        return result;
    }

    private static string ParseValueArg(string[] args, string name)
    {
        string prefix = "/" + name + ":";
        return args.Where(arg => arg.StartsWith(prefix, StringComparison.Ordinal)).Select(arg => arg.Substring(prefix.Length)).LastOrDefault();
    }

    private static bool ParseFlagArg(string[] args, string name, bool defaultValue)
    {
        string onStr1 = "/" + name;
        string onStr2 = "/" + name + "+";
        string offStr = "/" + name + "-";

        return args.Aggregate(defaultValue, (value, arg) =>
            (arg.Equals(onStr1, StringComparison.OrdinalIgnoreCase) || arg.Equals(onStr2, StringComparison.OrdinalIgnoreCase)) ? true :
            arg.Equals(offStr, StringComparison.OrdinalIgnoreCase) ? false :
            value);
    }
}

