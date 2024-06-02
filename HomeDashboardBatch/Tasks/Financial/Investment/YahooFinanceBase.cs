using System.Globalization;

using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using Microsoft.Extensions.Logging;

using ScrapingLibrary;

namespace HomeDashboardBatch.Tasks.Financial.Investment; 
public abstract class YahooFinanceBase<T> : IScrapingServiceTarget {
	private readonly HttpClientWrapper _httpClient;
	protected readonly ILogger<T> _logger;
	public YahooFinanceBase(ILogger<T> logger) {
		this._httpClient = new HttpClientWrapper();
		this._logger = logger;
	}

	protected async Task<List<YahooFinanceRecord>> GetRecords(string key) {
		var unixTime = (int)DateTime.UtcNow.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		var days30 = 120 * 24 * 60 * 60;
		var url = $"https://query1.finance.yahoo.com/v7/finance/download/{key}?period1={unixTime - days30}&period2={unixTime}&interval=1d&events=history&includeAdjustedClose=true";
		this._logger.LogInformation($"{url}の情報を取得開始");
		var response = await this._httpClient.GetAsync(url);
		if (!response.IsSuccessStatusCode) {
			throw new BatchException($"HTTPステータスコード異常: {response.StatusCode}");
		}
		return await response.ToCsvRecordAsync<YahooFinanceRecord>(new CsvConfiguration(CultureInfo.CurrentCulture) { HasHeaderRecord = true });
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
	public DateTime Date {
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
	[Name("Adj Close")]
	public double? AdjClose {
		get;
		set;
	}
	public double? Volume {
		get;
		set;
	}

}
