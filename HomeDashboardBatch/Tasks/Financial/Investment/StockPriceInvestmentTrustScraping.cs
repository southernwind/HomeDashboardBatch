using DataBase;

using ConsoleAppFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;


namespace HomeDashboardBatch.Tasks.Financial;
public class StockPriceInvestmentTrustScraping(
	ILogger<StockPriceInvestmentTrustScraping> logger,
	HomeServerDbContext db,
	IServiceProvider serviceProvider) {
	private readonly ILogger _logger = logger;
	private readonly HomeServerDbContext _db = db;
	private readonly IServiceProvider _serviceProvider = serviceProvider;


	/// <summary>
	/// 証券サイト等から情報を取得し、株価データの更新を行う。	
	/// </summary>
	/// <returns>実行結果</returns>
	[Command("update")]
	public async Task<int> Update() {
		var ipList = await this._db.InvestmentProducts.Where(x => x.Enable).ToArrayAsync();
		var icuList = await this._db.InvestmentCurrencyUnits.Where(x => x.Key != null).ToArrayAsync();
		var list = ipList.Select(x => new { Id = x.InvestmentProductId, x.Key, x.Type }).ToList();
		var targets = this._serviceProvider.GetServices<IScrapingServiceTarget>();

		list.AddRange(icuList.Select(x => new { x.Id, x.Key, Type = typeof(YahooFinanceCurrency).FullName }).ToArray()!);
		foreach (var item in list) {
			this._logger.LogInformation("ID:{id} 取得元:{type} 取得開始。",item.Id,item.Type);
			await targets.Single(x => x.GetType().FullName == item.Type)
				.ExecuteAsync(item.Id, item.Key);
			this._logger.LogInformation("ID:{id} 取得完了。", item.Id);
			await Task.Delay(5000);
		}
		return 0;
	}
}