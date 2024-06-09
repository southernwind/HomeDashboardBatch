using Database.Tables;

using DataBase;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HomeDashboardBatch.Tasks.Financial.Investment; 
public class YahooFinanceCurrency : YahooFinanceBase<YahooFinanceCurrency> {
	private readonly HomeServerDbContext _dbContext;

	public YahooFinanceCurrency(
		ILogger<YahooFinanceCurrency> logger,
		HomeServerDbContext dbContext) :base(logger){
		this._dbContext = dbContext;
	}

	public override async Task ExecuteAsync(int id, string key) {
		await using var transaction = await this._dbContext.Database.BeginTransactionAsync();
		this._dbContext.Database.ExecuteSqlRaw("SET sql_mode=''");
		var csv = await this.GetRecords(key);
		var records = csv.Where(x => x.AdjClose != null).Select(cr => new InvestmentCurrencyRate {
			InvestmentCurrencyUnitId = id,
			Date = cr.Date,
			Value = cr.AdjClose ?? 0
		}).GroupBy(x => x.Date)
		.Select(x => x.Last())
		.ToArray();

		if (!records.Any()) {
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
		this._logger.LogInformation($"{existing.Length}件削除");
		await this._dbContext.InvestmentCurrencyRates.AddRangeAsync(records);
		this._logger.LogInformation($"{records.Length}件登録");
		await this._dbContext.SaveChangesAsync();
		this._logger.LogInformation($"SaveChangesAsync");
		await transaction.CommitAsync();
		this._logger.LogInformation($"CommitAsync");
	}
}
