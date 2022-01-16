using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Parser;

namespace GetIpoInfo
{
    public class GetIpoInfo
    {
        const int DIGIT_YEAR = 4;

        struct IpoShedule
        {
            public DateTime applyStart;
            public DateTime applyExit;
            public DateTime winningAnnounce;
        }

        private static void Main()
        {
            WriteHeader();
            Task task = WriteContents("****");
            task.Wait();
        }

        private static void WriteHeader()
        {
            Console.WriteLine("会社名,銘柄コード,抽選申込開始,抽選申込終了,当選発表");
        }

        private static async Task WriteContents(string strSourceUrl)
        {
            string[] strIpoUrls = SearchIpoUrls(ParseHtmlInUrl(strSourceUrl));
            foreach (string strIpoUrl in strIpoUrls)
            {
                WriteIpoInfo(strIpoUrl);
                await Task.Delay(1000);
            }
            return;
        }

        private static void WriteIpoInfo(string strIpoUrl)
        {
            AngleSharp.Html.Dom.IHtmlDocument doc = ParseHtmlInUrl(strIpoUrl);

            string? companyName = GetCompanyName(doc);
            string? stockCode = GetStockCode(doc);
            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(stockCode)) return;

            IpoShedule ipoShedule = GetIpoSchedule(doc);

            Console.WriteLine(
                companyName + "," +
                stockCode + "," +
                ipoShedule.applyStart.ToShortDateString() + "," +
                ipoShedule.applyExit.ToShortDateString() + "," +
                ipoShedule.winningAnnounce.ToShortDateString()
                );
        }

        private static AngleSharp.Html.Dom.IHtmlDocument ParseHtmlInUrl(string strUrl)
        {
            Task<string> task = GetStrHtml(strUrl);
            var parser = new HtmlParser();
            var doc = parser.ParseDocument(task.Result);
            return doc;
        }

        private static async Task<string> GetStrHtml(string strUrl)
        {
            var config = Configuration.Default.WithDefaultLoader().WithDefaultCookies();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(strUrl);
            return document.ToHtml();
        }

        // IPO銘柄詳細のリンク先取得
        private static string[] SearchIpoUrls(AngleSharp.Html.Dom.IHtmlDocument doc)
        {
            // hrefを参照して"/company/数字4桁"なら取得する
            const string headerUrl = "https://www.ipokiso.com";

            List<string> ipoUrlList = new();
            foreach (var item in doc.QuerySelectorAll("a"))
            {
                string? strHref = item.GetAttribute("href");
                if (string.IsNullOrEmpty(strHref)) continue;
                if (Regex.IsMatch(strHref, "^/company/[0-9]{4}/")) ipoUrlList.Add(headerUrl + strHref);
            }

            return ipoUrlList.ToArray();
        }

        //企業名
        private static string? GetCompanyName(AngleSharp.Html.Dom.IHtmlDocument doc)
        {
            var h1Nodes = doc.QuerySelectorAll("h1");
            if (h1Nodes.Length < 2) return null;

            string strH1 = h1Nodes[1].TextContent;
            if (string.IsNullOrEmpty(strH1)) return null;

            return strH1[..strH1.IndexOf("(")];
        }

        //銘柄コード
        private static string? GetStockCode(AngleSharp.Html.Dom.IHtmlDocument doc)
        {
            var h1Nodes = doc.QuerySelectorAll("h1");
            string strH1 = h1Nodes[1].TextContent;
            if (string.IsNullOrEmpty(strH1)) return null;
            else return strH1.Substring(strH1.IndexOf("(") + 1, strH1.IndexOf(")") - strH1.IndexOf("(") - 1);
        }

        // IPOスケジュールを取得
        private static IpoShedule GetIpoSchedule(AngleSharp.Html.Dom.IHtmlDocument doc)
        {
            IpoShedule ipoShedule = InitializeIpoSchedule();

            int intYear = GetIpoYear(doc);
            if (intYear==0) return ipoShedule;

            var table = doc.GetElementsByClassName("kobetudate03");
            if (!table.Any()) return ipoShedule;

            var trs = table[0].GetElementsByTagName("tr");
            if (trs.Length <= 1 ) return ipoShedule;

            List<string> listDates = new();
            foreach (var tr in trs)
            {
                var tds = tr.GetElementsByTagName("td");
                listDates.Add(tds[0].TextContent);
            }

            string strApplyStart = listDates[0].Substring(0, listDates[0].IndexOf("～"));
            string strApplyExit = listDates[0].Substring(listDates[0].IndexOf("～") + 1, listDates[0].Length - (listDates[0].IndexOf("～") + 1));
            string strWinningAnnounce = listDates[1];
            ipoShedule.applyStart = new DateTime(intYear,FindMonth(strApplyStart),FindDay(strApplyStart));   // 抽選申込み開始日
            ipoShedule.applyExit = new DateTime(intYear,FindMonth(strApplyExit),FindDay(strApplyExit));    // 抽選申込み終了日
            ipoShedule.winningAnnounce = new DateTime(intYear, FindMonth(strWinningAnnounce), FindDay(strWinningAnnounce));  // 当選発表日
            return ipoShedule;

            static int GetIpoYear(AngleSharp.Html.Dom.IHtmlDocument doc)
            {
                string? ogUrl = doc.QuerySelector("meta[property='og:url']")?.GetAttribute("content");
                if (string.IsNullOrEmpty(ogUrl))
                {
                    return 0;
                }
                else
                {
                    string strIndex = "company/";
                    return Convert.ToInt32( ogUrl.Substring(ogUrl.IndexOf(strIndex) + strIndex.Length, DIGIT_YEAR));
                }
            }

            static int FindMonth(string strDate)
            {
                return Convert.ToInt32( strDate.Substring(0, strDate.IndexOf("月")));
            }

            static int FindDay(string strDate)
            {
                return Convert.ToInt32( strDate.Substring(strDate.IndexOf("月") + 1, strDate.IndexOf("日") - (strDate.IndexOf("月") + 1)));
            }

            static IpoShedule InitializeIpoSchedule()
            {
                IpoShedule ipoShedule = new();
                ipoShedule.applyStart = DateTime.MinValue;
                ipoShedule.applyExit = DateTime.MinValue;
                ipoShedule.winningAnnounce = DateTime.MinValue;
                return ipoShedule;
            }

        }

    }
}