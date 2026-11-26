// OSB version metadata validation (C#/Cake). Replaces validate-version.py.
#addin nuget:?package=Newtonsoft.Json&version=13.0.3

using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

var versionJson = Argument<string>("versionJson", null);
if (string.IsNullOrWhiteSpace(versionJson)) { Error("missing --versionJson"); return 1; }
if (!System.IO.File.Exists(versionJson)) { Error($"version.json not found: {versionJson}"); return 1; }

JObject data;
try { data = JObject.Parse(System.IO.File.ReadAllText(versionJson)); }
catch (Exception e) { Error($"invalid JSON: {e.Message}"); return 1; }

var component = data["component"]?.Value<string>();
var version = data["version"]?.Value<string>();
var generation = data["generation"]?.Value<int?>();
var codename = data["codename"]?.Type == JTokenType.Null ? null : data["codename"]?.Value<string>();
var release = data["release"]?.Value<bool>() ?? false;

if (string.IsNullOrWhiteSpace(component)) { Error("missing 'component'"); return 1; }
if (string.IsNullOrWhiteSpace(version)) { Error("missing 'version'"); return 1; }

var semver = new Regex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z\-]+(?:\.[0-9A-Za-z\-]+)*))?(?:\+([0-9A-Za-z\-]+(?:\.[0-9A-Za-z\-]+)*))?$");
if (!semver.IsMatch(version)) { Error($"'{version}' is not valid SemVer"); return 1; }

var expectGeneration = Argument<int?>("expectGeneration", null);
if (expectGeneration.HasValue && generation != expectGeneration.Value) { Error($"generation mismatch: file={generation}, expected={expectGeneration}"); return 1; }

var expectCodename = Argument<string>("expectCodename", null);
if (expectCodename != null && codename != expectCodename) { Error($"codename mismatch: file={codename}, expected={expectCodename}"); return 1; }

var strictRelease = HasArgument("strictRelease");
if (strictRelease) {
    if (!release) { Error("strict-release requires release=true"); return 1; }
    if (codename == null) { Error("strict-release requires resolved codename"); return 1; }
}

var branch = Argument<string>("onBranch", null);
if (!string.IsNullOrWhiteSpace(branch) && branch.StartsWith("release/")) {
    var bv = branch.Substring("release/".Length);
    if (bv != version) { Error($"release branch {branch} version {bv} != {version}"); return 1; }
}

// Generation codename registry: generation 0 -> Olorun
var registry = new System.Collections.Generic.Dictionary<int,string?> { {0,"Olorun"} };
if (!generation.HasValue || !registry.ContainsKey(generation.Value)) { Error($"no codename registry entry for generation {generation}"); return 1; }
if (codename != null && codename != registry[generation.Value]) { Error($"codename {codename} != registry {registry[generation.Value]}"); return 1; }

Information($"OK: {component} {version} (generation {generation}, codename={codename}, release={release})");
return 0;
