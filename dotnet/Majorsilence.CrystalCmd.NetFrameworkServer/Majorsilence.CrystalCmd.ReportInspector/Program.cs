using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrystalDecisions.CrystalReports.Engine;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Majorsilence.CrystalCmd.ReportInspector
{
    /// <summary>
    /// Dumps every table field, parameter field, and formula field (with source text) from an
    /// .rpt file, including subreports, then flags any names that differ only by casing, and
    /// optionally diffs a queue-item JSON payload's "Parameters" keys against what the report
    /// actually defines.
    /// </summary>
    internal class Program
    {
        private class FieldInfo
        {
            public string Kind; // Parameter, Formula, Table, TableField
            public string Source; // Main or Main > Subreport:X
            public string Name;
            public string Detail;
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ReportInspector.exe <path-to-rpt> [path-to-json-payload] [--export]");
                Console.WriteLine("  --export  also run the full CrystalCmd export pipeline (Exporter.exportReportToStream)");
                Console.WriteLine("            with the given payload and report, and write the result beside the json.");
                return 1;
            }

            string rptPath = args[0];
            if (!File.Exists(rptPath))
            {
                Console.WriteLine($"File not found: {rptPath}");
                return 1;
            }

            var allFields = new List<FieldInfo>();

            using (var rpt = new ReportDocument())
            {
                rpt.Load(rptPath);
                Console.WriteLine($"Loaded: {rptPath}");
                Dump(rpt, "Main", allFields);
            }

            PrintCaseCollisions(allFields);

            if (args.Length >= 2)
            {
                DiffAgainstPayload(args[1], allFields);

                if (args.Contains("--export"))
                {
                    return RunExport(rptPath, args[1]);
                }

                int stressIdx = Array.IndexOf(args, "--stress");
                if (stressIdx >= 0)
                {
                    int parallel = stressIdx + 1 < args.Length && int.TryParse(args[stressIdx + 1], out int p) ? p : Environment.ProcessorCount;
                    return RunStress(rptPath, args[1], parallel);
                }
            }

            return 0;
        }

        /// <summary>
        /// Mirrors the queue worker: N concurrent exports of the same report+payload inside one
        /// process, the way ExportQueue runs one thread per core. Surfaces Crystal engine
        /// concurrency failures (formula errors, load failures) that never occur single-threaded.
        /// </summary>
        private static int RunStress(string rptPath, string jsonPath, int parallel)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"STRESS TEST: {parallel} concurrent exports in one process");
            Console.WriteLine(new string('=', 70));

            string workingFolder = global::Majorsilence.CrystalCmd.Server.Common.WorkingFolder.GetMajorsilenceTempFolder();
            Directory.CreateDirectory(workingFolder);

            string json = File.ReadAllText(jsonPath);
            int ok = 0, failed = 0;
            var errors = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

            using (var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { }))
            {
                var logger = loggerFactory.CreateLogger("ReportInspector");
                var tasks = new List<System.Threading.Tasks.Task>();
                for (int i = 0; i < parallel; i++)
                {
                    tasks.Add(System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<global::Majorsilence.CrystalCmd.Common.Data>(json);
                            var exporter = new global::Majorsilence.CrystalCmd.Server.Common.Exporter(logger);
                            var result = exporter.exportReportToStream(rptPath, data);
                            System.Threading.Interlocked.Increment(ref ok);
                        }
                        catch (Exception ex)
                        {
                            System.Threading.Interlocked.Increment(ref failed);
                            string key = ex.GetType().Name + ": " + new string(ex.Message.Take(200).ToArray());
                            errors.AddOrUpdate(key, 1, (_, c) => c + 1);
                        }
                    }));
                }
                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            }

            Console.WriteLine($"succeeded: {ok}, failed: {failed}");
            foreach (var kv in errors.OrderByDescending(k => k.Value))
            {
                Console.WriteLine($"  x{kv.Value}  {kv.Key}");
            }
            return failed == 0 ? 0 : 3;
        }

        private static int RunExport(string rptPath, string jsonPath)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine("EXPORT TEST (full CrystalCmd pipeline on this machine)");
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}, UI: {System.Globalization.CultureInfo.CurrentUICulture.Name}, 64-bit: {Environment.Is64BitProcess}");

            // The service (Program.cs in the Console project) creates this folder at startup;
            // Exporter assumes it exists. Mirror that here, and report its state so a missing
            // folder on a server is visible instead of surfacing as a downstream export error.
            string workingFolder = global::Majorsilence.CrystalCmd.Server.Common.WorkingFolder.GetMajorsilenceTempFolder();
            Console.WriteLine($"Working folder: {workingFolder} (existed before run: {Directory.Exists(workingFolder)})");
            Directory.CreateDirectory(workingFolder);

            try
            {
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<global::Majorsilence.CrystalCmd.Common.Data>(File.ReadAllText(jsonPath));
                using (var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole()))
                {
                    var logger = loggerFactory.CreateLogger("ReportInspector");
                    var exporter = new global::Majorsilence.CrystalCmd.Server.Common.Exporter(logger);
                    var result = exporter.exportReportToStream(rptPath, data);
                    string outPath = Path.ChangeExtension(jsonPath, "exported." + result.Item2);
                    File.WriteAllBytes(outPath, result.Item1);
                    Console.WriteLine($"SUCCESS: exported {result.Item1.Length} bytes ({result.Item3}) -> {outPath}");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXPORT FAILED:");
                Console.WriteLine(ex.ToString());
                return 2;
            }
        }

        private static void Dump(ReportDocument rpt, string source, List<FieldInfo> allFields)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine(source);
            Console.WriteLine(new string('=', 70));

            try
            {
                Console.WriteLine("Tables:");
                foreach (Table table in rpt.Database.Tables)
                {
                    Console.WriteLine($"  [Table] {table.Name}");
                    allFields.Add(new FieldInfo { Kind = "Table", Source = source, Name = table.Name });
                    foreach (FieldDefinition field in table.Fields)
                    {
                        Console.WriteLine($"      - {field.Name}");
                        allFields.Add(new FieldInfo { Kind = "TableField", Source = source, Name = field.Name });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  <error reading tables: {ex.Message}>");
            }

            try
            {
                Console.WriteLine("Parameter Fields:");
                foreach (dynamic p in rpt.ParameterFields)
                {
                    Console.WriteLine($"  [Parameter] {p.Name}  ({p.ParameterValueType})");
                    allFields.Add(new FieldInfo { Kind = "Parameter", Source = source, Name = p.Name, Detail = p.ParameterValueType.ToString() });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  <error reading parameter fields: {ex.Message}>");
            }

            try
            {
                Console.WriteLine("Formula Fields:");
                foreach (FormulaFieldDefinition f in rpt.DataDefinition.FormulaFields)
                {
                    string text;
                    try { text = f.Text ?? string.Empty; }
                    catch { text = "<unavailable>"; }

                    string flatText = text.Replace("\r\n", " \\n ").Replace("\n", " \\n ");
                    Console.WriteLine($"  [Formula] {f.Name}");
                    Console.WriteLine($"      Text: {flatText}");
                    allFields.Add(new FieldInfo { Kind = "Formula", Source = source, Name = f.Name, Detail = text });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  <error reading formula fields: {ex.Message}>");
            }

            try
            {
                if (!string.IsNullOrEmpty(rpt.RecordSelectionFormula))
                {
                    Console.WriteLine($"Record Selection Formula: {rpt.RecordSelectionFormula}");
                }
            }
            catch
            {
                // not all subreports expose this cleanly, ignore
            }

            int subCount = 0;
            try { subCount = rpt.Subreports.Count; } catch { }

            for (int i = 0; i < subCount; i++)
            {
                try
                {
                    ReportDocument sub = rpt.Subreports[i];
                    Dump(sub, $"{source} > Subreport:{sub.Name}", allFields);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  <error opening subreport #{i}: {ex.Message}>");
                }
            }
        }

        private static void PrintCaseCollisions(List<FieldInfo> allFields)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine("CASE-COLLISION CHECK (same name, different casing, within the report itself)");
            Console.WriteLine(new string('=', 70));

            var collisions = allFields
                .Where(f => f.Kind == "Parameter" || f.Kind == "Formula")
                .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() > 1)
                .ToList();

            if (collisions.Count == 0)
            {
                Console.WriteLine("None found.");
                return;
            }

            foreach (var g in collisions)
            {
                var variants = g.Select(f => $"{f.Name} ({f.Kind}, {f.Source})").Distinct();
                Console.WriteLine($"- {string.Join("  vs  ", variants)}");
            }
        }

        private static void DiffAgainstPayload(string jsonPath, List<FieldInfo> allFields)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"DIFF AGAINST PAYLOAD: {jsonPath}");
            Console.WriteLine(new string('=', 70));

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"JSON payload not found: {jsonPath}");
                return;
            }

            JObject json = JObject.Parse(File.ReadAllText(jsonPath));
            JObject parametersToken = json["Parameters"] as JObject;
            if (parametersToken == null)
            {
                Console.WriteLine("No \"Parameters\" object found in JSON payload.");
                return;
            }

            var paramNames = allFields.Where(f => f.Kind == "Parameter").Select(f => f.Name).Distinct().ToList();
            var formulaNames = allFields.Where(f => f.Kind == "Formula").Select(f => f.Name).Distinct().ToList();

            foreach (var prop in parametersToken.Properties())
            {
                string key = prop.Name;

                if (paramNames.Contains(key, StringComparer.Ordinal))
                {
                    Console.WriteLine($"OK             {key}");
                    continue;
                }

                string caseInsensitiveParamMatch = paramNames.FirstOrDefault(n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase));
                if (caseInsensitiveParamMatch != null)
                {
                    Console.WriteLine($"CASE MISMATCH  json '{key}'  vs report parameter '{caseInsensitiveParamMatch}'");
                    continue;
                }

                string formulaMatch = formulaNames.FirstOrDefault(n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase));
                if (formulaMatch != null)
                {
                    Console.WriteLine($"WRONG KIND     json '{key}' matches FORMULA field '{formulaMatch}', not a Parameter Field -- setting it via Parameters will not bind");
                    continue;
                }

                Console.WriteLine($"NOT FOUND      json '{key}' does not match any Parameter or Formula field in the report");
            }
        }
    }
}
