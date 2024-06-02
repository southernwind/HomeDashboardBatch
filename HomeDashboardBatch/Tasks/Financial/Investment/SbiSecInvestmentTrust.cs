using Database.Tables;

using DataBase;
using HtmlAgilityPack.CssSelectors.NetCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ScrapingLibrary;

namespace HomeDashboardBatch.Tasks.Financial.Investment; 
public class SbiSecInvestmentTrust : IScrapingServiceTarget {
	private readonly HttpClientWrapper _httpClient;
	private readonly HomeServerDbContext _dbContext;
	private readonly ILogger<SbiSecInvestmentTrust> _logger;

	public SbiSecInvestmentTrust(
		ILogger<SbiSecInvestmentTrust> logger,
		HomeServerDbContext dbContext) {
		this._httpClient = new HttpClientWrapper();
		this._dbContext = dbContext;
		this._logger = logger;
	}

	public async Task ExecuteAsync(int investmentProductId, string key) {
		await using var transaction = await this._dbContext.Database.BeginTransactionAsync();
		this._dbContext.Database.ExecuteSqlRaw("SET sql_mode=''");
		var url = $"https://site0.sbisec.co.jp/marble/fund/history/standardprice.do?fund_sec_code={key}";
		this._logger.LogInformation($"{url}の情報を取得開始");
		var response = await this._httpClient.GetAsync(url);
		if (!response.IsSuccessStatusCode) {
			throw new BatchException($"HTTPステータスコード異常: {response.StatusCode}");
		}
		var htmlDoc = await response.ToHtmlDocumentAsync();
		var trs = htmlDoc.DocumentNode.QuerySelectorAll("#main .mgt10 .accTbl01 table tbody tr");
		var records = trs.Select(tr => new InvestmentProductRate {
			InvestmentProductId = investmentProductId,
			Date = DateTime.Parse(tr.QuerySelector("th").InnerText),
			Value = int.Parse(tr.QuerySelectorAll("td").First().InnerText.Replace("円", "").Replace(",", ""))
		}).ToArray();
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
		this._logger.LogInformation($"{records.Length}件登録");
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
