using System.Globalization;

using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using HtmlAgilityPack.CssSelectors.NetCore;

using Microsoft.Extensions.Logging;

using ScrapingLibrary;

namespace HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;
public abstract class YahooFinanceBase<T> : IScrapingServiceTarget {
	private readonly HttpClientWrapper _httpClient;
	protected readonly ILogger<T> _logger;
	public YahooFinanceBase(ILogger<T> logger) {
		this._httpClient = new HttpClientWrapper();
		this._logger = logger;
	}

	protected async Task<List<YahooFinanceRecord>> GetRecords(string key) {
		var url = $"https://finance.yahoo.com/quote/{key}/history/";
		this._logger.LogInformation($"{url}の情報を取得開始");
		var response = await this._httpClient.GetAsync(url);
		if (!response.IsSuccessStatusCode) {
			throw new BatchException($"HTTPステータスコード異常: {response.StatusCode}");
		}
		var html = await response.ToHtmlDocumentAsync();
		var records = html.DocumentNode.QuerySelectorAll("div.container[data-testid=history-table] div.table-container table tr")
			.Select(x => {
				var tds = x.QuerySelectorAll("td").Select(x => x.InnerText).ToArray();
				if (tds.Length != 7) {
					return null;
				}
				return tds;
			})
			.Where(x => x != null)
			.Select(x => {
			return new YahooFinanceRecord() {
				Date = DateOnly.Parse(x![0]),
				Open = double.Parse(x[1]),
				High = double.Parse(x[2]),
				Low = double.Parse(x[3]),
				Close = double.Parse(x[4]),
				AdjClose = double.Parse(x[5]),
				Volume = double.Parse(x[6] == "-" ? "0" : x[6])
			};
		}).ToList();
		return records;
	}


	protected virtual void Dispose(bool disposing) {
	}

	void IDisposable.Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}

	public abstract Task ExecuteAsync(int investmentProductId, string key);
}

public class YahooFinanceRecord {
	public DateOnly Date {
		get;
		set;
	}
	public double? Open {
		get;
		set;
	}
	public double? High {
		get;
		set;
	}
	public double? Low {
		get;
		set;
	}
	public double? Close {
		get;
		set;
	}

	public double? AdjClose {
		get;
		set;
	}
	public double? Volume {
		get;
		set;
	}

}
