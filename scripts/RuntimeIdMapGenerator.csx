#r "Newtonsoft.Json.dll"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

// The RIDs supported by LibGit2 - these match the directory names (runtimes/{name}/native).
var availableRids = new[] 
{
    "alpine-x64",
    "debian.9-x64",
    "fedora-x64",
    "linux-x64",
    "osx",
    "rhel-x64",
    "win-x64",
    "win-x86",
};

// The path to runtime.json file retrieved from https://raw.githubusercontent.com/dotnet/corefx/master/pkg/Microsoft.NETCore.Platforms/runtime.json.
var runtimeJsonPath = Path.Combine(GetScriptDir(), "runtime.json");

var runtimes = BuildRuntimeGraph();

var map = new Dictionary<string, string>(StringComparer.Ordinal);

foreach (var entry in runtimes)
{
    var rid = entry.Key;
    var compatibleRids = GetCompatibleRuntimeIdentifiers(runtimes, rid);
    foreach (var availableRid in availableRids)
    {
        if (compatibleRids.Contains(availableRid))
        {
            // use the first rid, it is the most specific:
            if (!map.TryGetValue(rid, out var existing))
            {
                map.Add(rid, availableRid);
            }
        }
    }
}

var orderedMap = map.OrderBy(e => e.Key, StringComparer.Ordinal);

Console.WriteLine("private static readonly string[] s_rids = new[]");
Console.WriteLine("{");

foreach (var entry in orderedMap)
{
    Console.WriteLine($"    \"{entry.Key}\",");
}

Console.WriteLine("};");
Console.WriteLine();
Console.WriteLine("private static readonly string[] s_directories = new[]");
Console.WriteLine("{");

foreach (var entry in orderedMap)
{
    Console.WriteLine($"    \"{entry.Value}\",");
}

Console.WriteLine("};");

string GetScriptDir([CallerFilePath] string path = null) => Path.GetDirectoryName(path);

Dictionary<string, Runtime> BuildRuntimeGraph()
{
    var rids = new Dictionary<string, Runtime>();

    var json = JObject.Parse(File.ReadAllText(runtimeJsonPath));
    var runtimes = (JObject)json["runtimes"];

    foreach (var runtime in runtimes)
    {
        var imports = (JArray)((JObject)runtime.Value)["#import"];
        rids.Add(runtime.Key, new Runtime(runtime.Key, imports.Select(import => (string)import).ToArray()));
    }

    return rids;
}

struct Runtime
{
    public string RuntimeIdentifier { get; }
    public string[] ImportedRuntimeIdentifiers { get; }

    public Runtime(string runtimeIdentifier, string[] importedRuntimeIdentifiers)
    {
        RuntimeIdentifier = runtimeIdentifier;
        ImportedRuntimeIdentifiers = importedRuntimeIdentifiers;
    }
}

List<string> GetCompatibleRuntimeIdentifiers(Dictionary<string, Runtime> runtimes, string runtimeIdentifier)
{
    var result = new List<string>();

    if (runtimes.TryGetValue(runtimeIdentifier, out var initialRuntime))
    {
        var queue = new Queue<Runtime>();
        var hash = new HashSet<string>();

        hash.Add(runtimeIdentifier);
        queue.Enqueue(initialRuntime);

        while (queue.Count > 0)
        {
            var runtime = queue.Dequeue();
            result.Add(runtime.RuntimeIdentifier);

            foreach (var item in runtime.ImportedRuntimeIdentifiers)
            {
                if (hash.Add(item))
                {
                    queue.Enqueue(runtimes[item]);
                }
            }
        }
    }
    else
    {
        result.Add(runtimeIdentifier);
    }

    return result;
}
