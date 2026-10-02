#if NET10_0_OR_GREATER
using Majorsilence.CrystalCmd.Common;
using SkiaSharp;
using System.Data;
using System.Text;

namespace Majorsilence.CrystalCmd.ClientTests
{
    /// <summary>
    /// The acceptance corpus: every serviceable end-to-end scenario rendered through both
    /// backends by way of the real server, and the two PDFs compared by ink agreement, the
    /// measure majorsilence.crystal's visual suite uses (the Jaccard index of inked 8px
    /// cells on the first page, which a blank render scores 0 on by construction). Each
    /// scenario has a recorded baseline; a drop of more than the tolerance on either
    /// backend fails, a rise says to raise the baseline. Both PDFs and a diff image are
    /// attached to every result for triage.
    ///
    /// Run on demand with:
    ///   dotnet test Majorsilence.CrystalCmd.ClientTests -f net10.0 --filter Category=Acceptance
    /// </summary>
    [TestFixture]
    [Category("Acceptance")]
    public class AcceptanceTests
    {
        private const string Username = "user";
        private const string Password = "password";
        private const double Tolerance = 2.0;

        // Ink agreement, percent, Crystal against RptEngine, first page, as last measured.
        // Raise a value when a run reports an improvement; never lower one without saying
        // what was given up. The first measurement (Majorsilence.Crystal 0.1.0) found three
        // things, all on that engine's side and raised in its tracker: a Details section
        // with no database fields is dropped entirely (Crystal prints it once), so the
        // plain and parameter reports render blank; the templates' paper size is read as
        // Letter where Crystal has A4; and a section's static background colour is not
        // carried, so the grey title band is missing (majorsilence.crystal #32, #33, #34).
        // The dataset report's content is otherwise the same; its score is the paper size
        // and the band.
        private static readonly Dictionary<string, double> Baseline = new()
        {
            ["dataset-report"] = 9.3,
            ["plain-report"] = 0.0,
            ["parameters"] = 0.0,
            ["subreport-parameters"] = 0.0,
        };

        public sealed record Scenario(string Name, string Template, Func<Data> MakeData);

        private static DataTable Employees()
        {
            var table = new DataTable();
            table.Columns.Add("EMPLOYEE_ID", typeof(int));
            table.Columns.Add("LAST_NAME", typeof(string));
            table.Columns.Add("FIRST_NAME", typeof(string));
            table.Columns.Add("BIRTH_DATE", typeof(DateTime));
            table.Rows.Add(25, "Indocin, Hi there", "David", new DateTime(2020, 1, 15));
            table.Rows.Add(50, "Enebrel", "Sam", new DateTime(2020, 2, 16));
            table.Rows.Add(10, "Hydralazine", "Christoff", new DateTime(2020, 3, 17));
            table.Rows.Add(21, "Combivent", "Janet", new DateTime(2020, 4, 18));
            table.Rows.Add(100, "Dilantin", "Melanie", new DateTime(2020, 5, 19));
            return table;
        }

        private static Data WithParameters(Data data)
        {
            data.Parameters.Add("MyParameter", "My First Parameter");
            data.Parameters.Add("MyParameter2", true);
            return data;
        }

        // The serviceable scenarios of Test_ConnectToServerWritePdfAsync: one table or none,
        // no subreport with its own data. The others cannot go to RptEngine yet.
        private static readonly Scenario[] Scenarios =
        [
            new("dataset-report", "the_dotnet_dataset_report.rpt", () => { var d = new Data(); d.AddData("EMPLOYEE", Employees()); return d; }),
            new("plain-report", "thereport.rpt", () => new Data()),
            new("parameters", "thereport_wth_parameters.rpt", () => WithParameters(new Data())),
            new("subreport-parameters", "thereport_with_subreport_with_parameters.rpt", () =>
            {
                var d = new Data();
                d.SubReportParameters.Add(new SubReportParameters
                {
                    ReportName = "thereport_wth_parameters.rpt",
                    Parameters = WithParameters(new Data()).Parameters
                });
                return d;
            }),
        ];

        private static IEnumerable<TestCaseData> Cases() =>
            Scenarios.Select(s => new TestCaseData(s).SetName($"Acceptance_{s.Name}"));

        [SetUp]
        public void RequireServer()
        {
            if (!UnitTestSetup.IsServerAvailable)
                Assert.Ignore($"Server not available: {UnitTestSetup.ServerUnavailableReason}");
        }

        [TestCaseSource(nameof(Cases))]
        public async Task BothBackends_AgreeOnTheFirstPage(Scenario scenario)
        {
            byte[] crystal = await Render(scenario, RenderBackend.Crystal);
            byte[] rptEngine = await Render(scenario, RenderBackend.RptEngine);

            string dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "acceptance", scenario.Name);
            Directory.CreateDirectory(dir);
            string crystalPath = Path.Combine(dir, "crystal.pdf");
            string rptEnginePath = Path.Combine(dir, "rptengine.pdf");
            File.WriteAllBytes(crystalPath, crystal);
            File.WriteAllBytes(rptEnginePath, rptEngine);
            TestContext.AddTestAttachment(crystalPath);
            TestContext.AddTestAttachment(rptEnginePath);

            using var reference = PDFtoImage.Conversion.ToImage(new MemoryStream(crystal), page: new Index(0));
            using var ours = PDFtoImage.Conversion.ToImage(new MemoryStream(rptEngine), page: new Index(0));
            int crystalPages = PDFtoImage.Conversion.GetPageCount(new MemoryStream(crystal));
            int rptEnginePages = PDFtoImage.Conversion.GetPageCount(new MemoryStream(rptEngine));

            double agreement = InkAgreementPercent(reference, ours);
            string diffPath = Path.Combine(dir, "diff.png");
            WriteDiff(reference, ours, diffPath);
            TestContext.AddTestAttachment(diffPath);
            TestContext.Out.WriteLine($"{scenario.Name}: ink agreement {agreement:F1}% (pages: Crystal {crystalPages}, RptEngine {rptEnginePages})");

            Assert.That(rptEnginePages, Is.EqualTo(crystalPages), "page counts differ");
            if (!Baseline.TryGetValue(scenario.Name, out double baseline))
                Assert.Inconclusive($"No baseline recorded for {scenario.Name}; measured {agreement:F1}%. Record it in AcceptanceTests.Baseline.");

            Assert.That(agreement, Is.GreaterThanOrEqualTo(baseline - Tolerance),
                $"{scenario.Name}: ink agreement fell from a recorded {baseline:F1}% to {agreement:F1}%");
            if (agreement > baseline + Tolerance)
                Assert.Warn($"{scenario.Name}: ink agreement IMPROVED from {baseline:F1}% to {agreement:F1}%; raise the recorded baseline.");
        }

        private static async Task<byte[]> Render(Scenario scenario, RenderBackend backend)
        {
            var data = scenario.MakeData();
            data.Backend = backend;
            using var template = new FileStream(scenario.Template, FileMode.Open, FileAccess.Read);
            var report = new Client.Report(UnitTestSetup.TestServerBaseUrl, username: Username, password: Password);
            using var stream = await report.GenerateAsync(data, template);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            byte[] bytes = buffer.ToArray();
            Assert.That(Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length)), Is.EqualTo("%PDF-"), $"{backend} did not return a PDF");
            return bytes;
        }

        // Same measure as majorsilence.crystal's visual suite: the fraction of inked 8px
        // cells the two pages share, over the union, after aligning dimensions.
        private static double InkAgreementPercent(SKBitmap reference, SKBitmap ours, int cell = 8)
        {
            using SKBitmap oursResized = (reference.Width == ours.Width && reference.Height == ours.Height)
                ? ours.Copy()
                : ours.Resize(new SKImageInfo(reference.Width, reference.Height), SKFilterQuality.Medium);
            using SKBitmap a = reference.Copy(SKColorType.Bgra8888);
            using SKBitmap b = oursResized.Copy(SKColorType.Bgra8888);
            byte[] aBytes = a.Bytes;
            byte[] bBytes = b.Bytes;

            int cols = (a.Width + cell - 1) / cell;
            int rows = (a.Height + cell - 1) / cell;
            var aInk = new bool[cols * rows];
            var bInk = new bool[cols * rows];
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    int o = (y * a.Width + x) * 4;
                    int cellIndex = (y / cell) * cols + (x / cell);
                    if (765 - (aBytes[o] + aBytes[o + 1] + aBytes[o + 2]) > 90) aInk[cellIndex] = true;
                    if (765 - (bBytes[o] + bBytes[o + 1] + bBytes[o + 2]) > 90) bInk[cellIndex] = true;
                }
            }

            int intersection = 0, union = 0;
            for (int i = 0; i < aInk.Length; i++)
            {
                if (aInk[i] && bInk[i]) intersection++;
                if (aInk[i] || bInk[i]) union++;
            }
            return union == 0 ? 100.0 : 100.0 * intersection / union;
        }

        // Where the two differ: Crystal-only ink in red, RptEngine-only in blue, shared in grey.
        private static void WriteDiff(SKBitmap reference, SKBitmap ours, string path)
        {
            using SKBitmap oursResized = (reference.Width == ours.Width && reference.Height == ours.Height)
                ? ours.Copy()
                : ours.Resize(new SKImageInfo(reference.Width, reference.Height), SKFilterQuality.Medium);
            using SKBitmap a = reference.Copy(SKColorType.Bgra8888);
            using SKBitmap b = oursResized.Copy(SKColorType.Bgra8888);
            using var diff = new SKBitmap(new SKImageInfo(a.Width, a.Height, SKColorType.Bgra8888));
            byte[] aBytes = a.Bytes, bBytes = b.Bytes;
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    int o = (y * a.Width + x) * 4;
                    bool inkA = 765 - (aBytes[o] + aBytes[o + 1] + aBytes[o + 2]) > 90;
                    bool inkB = 765 - (bBytes[o] + bBytes[o + 1] + bBytes[o + 2]) > 90;
                    diff.SetPixel(x, y, inkA && inkB ? new SKColor(120, 120, 120)
                        : inkA ? new SKColor(220, 40, 40)
                        : inkB ? new SKColor(40, 80, 220)
                        : SKColors.White);
                }
            }
            using var image = SKImage.FromBitmap(diff);
            using var png = image.Encode(SKEncodedImageFormat.Png, 90);
            using var file = File.OpenWrite(path);
            png.SaveTo(file);
        }
    }
}
#endif
