using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace InterestCalculator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("el-GR", false);
            const string greekEncoding = "windows-1253";
            Console.InputEncoding = Encoding.GetEncoding(greekEncoding);
            Console.OutputEncoding = Encoding.GetEncoding(greekEncoding);

            Console.Write("Ημερομηνία από (dd/MM/yyyy): ");
            var fromDate = DateTime.ParseExact(Console.ReadLine(), "dd/MM/yyyy", CultureInfo.InvariantCulture);

            Console.Write("Ημερομηνία έως (dd/MM/yyyy): ");
            var toDate = DateTime.ParseExact(Console.ReadLine(), "dd/MM/yyyy", CultureInfo.InvariantCulture);

            Console.Write("Ποσό (€)(Τελεία για χιλιάδες, κόμμα για δεκαδικά): ");
            decimal amount = decimal.Parse(Console.ReadLine());

            var rates = GetInterestRates();
            var interestResults = CalculateInterest(fromDate, toDate, amount, rates);

            Console.WriteLine($"\nΑποτελέσματα για το ποσό των {amount:C}:\n");
            Console.WriteLine($"Τόκοι για το χρονικό διάστημα από {fromDate:dd/MM/yyyy} έως {toDate:dd/MM/yyyy} (Ημερολογιακό έτος)\n");

            Console.WriteLine("Από        Έως           Μέρες   Δικαιοπρακτικό     Ποσό (€)   Υπερημερίας      Ποσό (€)");
            Console.WriteLine("---------------------------------------------------------------------------------------------");

            foreach (var res in interestResults)
            {
                Console.WriteLine($"{res.From:dd/MM/yyyy}  {res.To:dd/MM/yyyy}  {res.Days,5}   " +
                                  $"{res.DikaiopraktikosRate,8:P2}     {res.DikaiopraktikosInterest,13:C}   " +
                                  $"{res.IperimeriasRate,8:P2}     {res.IperimeriasInterest,13:C}");
            }

            var dikaiopraktikosInterestSum = interestResults.Sum(x => x.DikaiopraktikosInterest);
            var iperimeriasInterestSum = interestResults.Sum(x => x.IperimeriasInterest);

            Console.WriteLine("\nΣύνοψη:");
            Console.WriteLine("---------------------------------------------------------------------------------------------");
            Console.WriteLine($"Αρχικό κεφάλαιο:                               {amount,13:C}               {amount,13:C}");
            Console.WriteLine($"Τόκος:                                        {dikaiopraktikosInterestSum,13:C}                {iperimeriasInterestSum,13:C}");
            Console.WriteLine($"Σύνολο:                                        {amount + dikaiopraktikosInterestSum,13:C}               {amount + iperimeriasInterestSum,13:C}");

            Console.WriteLine("\nΠάτησε οποιοδήποτε πλήκτρο για έξοδο...");
            Console.ReadKey();
        }

        static List<InterestRatePeriod> GetInterestRates()
        {
            var url = "https://www.bankofgreece.gr/statistika/xrhmatopistwtikes-agores/ekswtrapezika-epitokia";
            var web = new HtmlWeb();
            var doc = web.Load(url);

            var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'insuranceCompany__table')]");
            var rows = table.SelectNodes(".//tr");

            var result = new List<InterestRatePeriod>();
            foreach (var row in rows.Skip(1)) // skip header
            {
                var cells = row.SelectNodes("td");
                if (cells == null || cells.Count < 5) continue;

                if (DateTime.TryParseExact(cells[0].InnerText.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fromDate)
                    && DateTime.TryParseExact(cells[1].InnerText.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime toDate)
                    && decimal.TryParse(cells[4].InnerText.Replace("%", "").Trim(), NumberStyles.Any, CultureInfo.CreateSpecificCulture("el-GR"), out decimal dikaioprakitkos)
                    && decimal.TryParse(cells[5].InnerText.Replace("%", "").Trim(), NumberStyles.Any, CultureInfo.CreateSpecificCulture("el-GR"), out decimal iperimerias))
                {
                    result.Add(new InterestRatePeriod
                    {
                        From = fromDate,
                        To = toDate,
                        DioikitikiPraksi = cells[2].InnerText.Trim(),
                        AFek = cells[3].InnerText.Trim(),
                        Dikaioprakitkos = dikaioprakitkos / 100,
                        Iperimerias = iperimerias /100
                    });
                }
            }

            return result.OrderBy(r => r.From).ToList();
        }

        static List<InterestResult> CalculateInterest(DateTime from, DateTime to, decimal amount, List<InterestRatePeriod> rates)
        {
            var results = new List<InterestResult>();

            for (int i = 0; i < rates.Count; i++)
            {
                var rateStart = rates[i].From;
                var dikaiopraktikos = rates[i].Dikaioprakitkos;
                var iperimerias = rates[i].Iperimerias;
                var rateEnd = (i < rates.Count - 1) ? rates[i + 1].From.AddDays(-1) : DateTime.MaxValue;

                if (rateStart > to) break;
                if (rateEnd < from) continue;

                var periodStart = (rateStart < from) ? from : rateStart;
                var periodEnd = (rateEnd > to) ? to : rateEnd;

                if (periodEnd < periodStart) continue;

                decimal totalDikaiopraktikos = 0m;
                decimal totalIperimerias = 0m;

                var current = periodStart;
                while (current <= periodEnd)
                {
                    var year = current.Year;
                    var yearEnd = new DateTime(year, 12, 31);
                    var segmentEnd = (periodEnd < yearEnd) ? periodEnd : yearEnd;

                    var days = (segmentEnd - current).Days + 1;
                    var yearDays = DateTime.IsLeapYear(year) ? 366m : 365m;

                    var dp = amount * (decimal)dikaiopraktikos * days / yearDays;
                    var ip = amount * (decimal)iperimerias * days / yearDays;

                    totalDikaiopraktikos += dp;
                    totalIperimerias += ip;

                    current = segmentEnd.AddDays(1);
                }

                results.Add(new InterestResult
                {
                    From = periodStart,
                    To = periodEnd,
                    DikaiopraktikosRate = dikaiopraktikos,
                    IperimeriasRate = iperimerias,
                    DikaiopraktikosInterest = Math.Round(totalDikaiopraktikos, 2),
                    IperimeriasInterest = Math.Round(totalIperimerias, 2)
                });
            }

            return results;
        }

    }
}
