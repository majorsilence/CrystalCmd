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
    /// cells on the first page, which a blank render scores 0 on by construction), after
    /// RptEngine's page is moved up to 12pt to line up with Crystal's, since Crystal's moves
    /// with the host printer's margins. Each scenario has a recorded baseline; a drop of more than the tolerance on either
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

        // Ink agreement, percent, Crystal against RptEngine, first page, aligned, as last
        // measured. Raise a value when a run reports an improvement; never lower one without
        // saying what was given up. Measured on Majorsilence.Crystal 0.2.1 (0.1.0 scored 15.9,
        // 0, 0 and 0 unaligned) on a host whose default printer holds A4, without
        // Worker__PrinterPaper: the dataset report, which prints on its printer's paper, is A4
        // from Crystal and Letter from RptEngine there. With Worker__PrinterPaper=A4 set in the
        // environment both pages are A4 and it scores 94.3. The subreport scenario draws its
        // subreport only once the engine draws a subreport whose dataset has no rows
        // (majorsilence/Reporting#345).
        private static readonly Dictionary<string, double> Baseline = new()
        {
            ["dataset-report"] = 96.8,
            ["plain-report"] = 95.0,
            ["parameters"] = 95.7,
            ["subreport-parameters"] = 5.9,
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

            var render = new PDFtoImage.RenderOptions(Dpi: Dpi);
            using var reference = PDFtoImage.Conversion.ToImage(new MemoryStream(crystal), page: new Index(0), options: render);
            using var ours = PDFtoImage.Conversion.ToImage(new MemoryStream(rptEngine), page: new Index(0), options: render);
            int crystalPages = PDFtoImage.Conversion.GetPageCount(new MemoryStream(crystal));
            int rptEnginePages = PDFtoImage.Conversion.GetPageCount(new MemoryStream(rptEngine));

            var (dx, dy) = InkOffset(reference, ours, MaxShiftPoints * Dpi / 72);
            double unaligned = InkAgreementPercent(reference, ours, 0, 0);
            double agreement = InkAgreementPercent(reference, ours, dx, dy);
            string diffPath = Path.Combine(dir, "diff.png");
            WriteDiff(reference, ours, dx, dy, diffPath);
            TestContext.AddTestAttachment(diffPath);
            TestContext.Out.WriteLine($"{scenario.Name}: ink agreement {agreement:F1}% (pages: Crystal {crystalPages}, RptEngine {rptEnginePages}); "
                + $"RptEngine's page moved {dx * 72.0 / Dpi:F1}pt right and {dy * 72.0 / Dpi:F1}pt down to line up, {unaligned:F1}% before");

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

        // Ink on a page at the same scale for both backends, padded to a shared canvas: a
        // cell beyond a page's own edge simply has no ink. Pages are aligned at the top left
        // and never stretched onto each other, because the two can legitimately differ in
        // paper size (a template that follows the printer gets the Crystal host's printer's
        // paper), and stretching a Letter page onto an A4 one would move every line on it.
        // The scale both pages are rasterized at.
        private const int Dpi = 300;

        // How far RptEngine's page may be moved to line up with Crystal's before scoring. Crystal
        // formats a report against the host's printer, and a template that keeps the printer's
        // margins moves with them: against Microsoft Print to PDF the same template prints 6pt
        // right of and below where it prints against a laser printer, which alone took a page
        // holding one line from 88% to 7%. RptEngine has no printer, so that offset belongs to
        // the host, not to either backend. A layout error is larger than this, and still shows.
        private const int MaxShiftPoints = 12;

        // The move, in pixels, that best lines RptEngine's ink up with Crystal's: each axis on
        // its own, from how much ink each row and column holds.
        private static (int Dx, int Dy) InkOffset(SKBitmap reference, SKBitmap ours, int maxShift)
        {
            var (refRows, refCols) = InkProfiles(reference);
            var (ourRows, ourCols) = InkProfiles(ours);
            return (BestShift(refCols, ourCols, maxShift), BestShift(refRows, ourRows, maxShift));
        }

        private static (int[] Rows, int[] Cols) InkProfiles(SKBitmap page)
        {
            using SKBitmap bgra = page.Copy(SKColorType.Bgra8888);
            byte[] bytes = bgra.Bytes;
            var rows = new int[bgra.Height];
            var cols = new int[bgra.Width];
            for (int y = 0; y < bgra.Height; y++)
            {
                for (int x = 0; x < bgra.Width; x++)
                {
                    int o = (y * bgra.Width + x) * 4;
                    if (765 - (bytes[o] + bytes[o + 1] + bytes[o + 2]) > 90)
                    {
                        rows[y]++;
                        cols[x]++;
                    }
                }
            }
            return (rows, cols);
        }

        // The shift s, within plus or minus max, under which ours[i] best overlaps reference[i + s];
        // the smallest such shift on a tie, so pages that already line up are left where they are.
        private static int BestShift(int[] reference, int[] ours, int max)
        {
            int best = 0;
            long bestScore = -1;
            for (int s = -max; s <= max; s++)
            {
                long score = 0;
                for (int i = 0; i < ours.Length; i++)
                {
                    int j = i + s;
                    if (j >= 0 && j < reference.Length)
                        score += Math.Min(ours[i], reference[j]);
                }
                if (score > bestScore || (score == bestScore && Math.Abs(s) < Math.Abs(best)))
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        private static bool[] InkCells(SKBitmap page, int cols, int rows, int cell, int dx = 0, int dy = 0)
        {
            using SKBitmap bgra = page.Copy(SKColorType.Bgra8888);
            byte[] bytes = bgra.Bytes;
            var ink = new bool[cols * rows];
            for (int y = 0; y < bgra.Height; y++)
            {
                for (int x = 0; x < bgra.Width; x++)
                {
                    int o = (y * bgra.Width + x) * 4;
                    if (765 - (bytes[o] + bytes[o + 1] + bytes[o + 2]) > 90)
                    {
                        int cx = (x + dx) / cell, cy = (y + dy) / cell;
                        if (x + dx >= 0 && y + dy >= 0 && cx < cols && cy < rows)
                            ink[cy * cols + cx] = true;
                    }
                }
            }
            return ink;
        }

        // Same measure as majorsilence.crystal's visual suite, the fraction of inked 8px
        // cells the two pages share over the union of their inked cells, but on the shared
        // canvas above rather than with one page resized to the other.
        private static double InkAgreementPercent(SKBitmap reference, SKBitmap ours, int dx, int dy, int cell = 8)
        {
            int cols = (Math.Max(reference.Width, ours.Width) + cell - 1) / cell;
            int rows = (Math.Max(reference.Height, ours.Height) + cell - 1) / cell;
            var aInk = InkCells(reference, cols, rows, cell);
            var bInk = InkCells(ours, cols, rows, cell, dx, dy);

            int intersection = 0, union = 0;
            for (int i = 0; i < aInk.Length; i++)
            {
                if (aInk[i] && bInk[i]) intersection++;
                if (aInk[i] || bInk[i]) union++;
            }
            return union == 0 ? 100.0 : 100.0 * intersection / union;
        }

        // Where the two differ, on the same shared canvas: Crystal-only ink in red,
        // RptEngine-only in blue, shared in grey.
        private static void WriteDiff(SKBitmap reference, SKBitmap ours, int dx, int dy, string path)
        {
            int width = Math.Max(reference.Width, ours.Width), height = Math.Max(reference.Height, ours.Height);
            var aInk = InkCells(reference, width, height, 1);
            var bInk = InkCells(ours, width, height, 1, dx, dy);
            using var diff = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888));
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inkA = aInk[y * width + x], inkB = bInk[y * width + x];
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
