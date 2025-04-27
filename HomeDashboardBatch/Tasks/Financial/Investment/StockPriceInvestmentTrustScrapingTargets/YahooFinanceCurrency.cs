using Database.Tables;

using DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;
public class YahooFinanceCurrency(
	ILogger<YahooFinanceCurrency> logger,
	HomeServerDbContext dbContext) : YahooFinanceBase<YahooFinanceCurrency>(logger) {
	private readonly HomeServerDbContext _dbContext = dbContext;

	public override async Task ExecuteAsync(int id, string key) {
		await using var transaction = await this._dbContext.Database.BeginTransactionAsync();
		var csv = await this.GetRecords(key);
		var records = csv.Where(x => x.AdjClose != null).Select(cr => new InvestmentCurrencyRate {
			InvestmentCurrencyUnitId = id,
			Date = cr.Date,
			Value = cr.AdjClose ?? 0
		}).GroupBy(x => x.Date)
		.Select(x => x.Last())
		.ToArray();

		if (records.Length == 0) {
			throw new BatchException("取得件数0件");
		}

		var existing = (await this._dbContext
				.InvestmentCurrencyRates
				.Where(x =>
					x.InvestmentCurrencyUnitId == id)
				.ToArrayAsync())
			.Where(x =>
				x.Date <= records.Max(r => r.Date) &&
				x.Date >= records.Min(r => r.Date))
			.ToArray();

		this._dbContext.InvestmentCurrencyRates.RemoveRange(existing);
		this._logger.LogInformation("{length}件削除", existing.Length);
		await this._dbContext.InvestmentCurrencyRates.AddRangeAsync(records);
		this._logger.LogInformation("{length}件登録", records.Length);
		await this._dbContext.SaveChangesAsync();
		this._logger.LogInformation($"SaveChangesAsync");
		await transaction.CommitAsync();
		this._logger.LogInformation($"CommitAsync");
	}
}
