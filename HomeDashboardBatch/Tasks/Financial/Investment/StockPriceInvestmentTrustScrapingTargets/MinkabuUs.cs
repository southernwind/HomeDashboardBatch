using Database.Tables;

using DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ScrapingLibrary;

namespace HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;
public class MinkabuUs : IScrapingServiceTarget {
	private readonly HttpClientWrapper _httpClient;
	private readonly HomeServerDbContext _dbContext;
	private readonly ILogger<MinkabuUs> _logger;
	public MinkabuUs(
		ILogger<MinkabuUs> logger,
		HomeServerDbContext dbContext) {
		this._httpClient = new HttpClientWrapper();
		this._dbContext = dbContext;
		this._logger = logger;
	}

	public async Task ExecuteAsync(int investmentProductId, string key) {
		await using var transaction = await this._dbContext.Database.BeginTransactionAsync();
		var url = $"https://us.minkabu.jp/stocks/{key}/historicals.json?page=1&per=100";
		this._logger.LogInformation($"{url}の情報を取得開始");
		var response = await this._httpClient.GetAsync(url);
		if (!response.IsSuccessStatusCode) {
			throw new BatchException($"HTTPステータスコード異常: {response.StatusCode}");
		}
		var json = await response.ToJsonAsync();

		var records = new List<InvestmentProductRate>();

		foreach (var record in json) {
			var rate = new InvestmentProductRate {
				InvestmentProductId = investmentProductId,
				Date = DateOnly.Parse(record["trade_date"]),
				Value = double.Parse(record["close_price"])
			};
			records.Add(rate);
		}

		if (!records.Any()) {
			throw new BatchException("取得件数0件");
		}

		var existing = (await this._dbContext
				.InvestmentProductRates
				.Where(x =>
					x.InvestmentProductId == investmentProductId)
				.ToArrayAsync())
			.Where(x =>
				x.Date <= records.Max(r => r.Date) &&
				x.Date >= records.Min(r => r.Date))
			.ToArray();

		this._dbContext.InvestmentProductRates.RemoveRange(existing);
		this._logger.LogInformation($"{existing.Length}件削除");
		await this._dbContext.InvestmentProductRates.AddRangeAsync(records);
		this._logger.LogInformation($"{records.Count}件登録");
		await this._dbContext.SaveChangesAsync();
		this._logger.LogInformation($"SaveChangesAsync");
		await transaction.CommitAsync();
		this._logger.LogInformation($"CommitAsync");
	}

	protected virtual void Dispose(bool disposing) {
	}

	void IDisposable.Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
}
