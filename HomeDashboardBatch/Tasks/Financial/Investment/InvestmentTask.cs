using DataBase;

using ConsoleAppFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;


namespace HomeDashboardBatch.Tasks.Financial;
public class InvestmentTask(
	ILogger<InvestmentTask> logger,
	HomeServerDbContext db,
	IServiceProvider serviceProvider) {
	private readonly ILogger _logger = logger;
	private readonly HomeServerDbContext _db = db;
	private readonly IServiceProvider _serviceProvider = serviceProvider;


	/// <summary>
	/// 証券サイト等から情報を取得し、株価データの更新を行う。	
	/// </summary>
	/// <returns>実行結果</returns>
	[Command("update-stock-price")]
	public async Task<int> UpdateStockPrice() {
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

	/// <summary>
	/// デイリー資産残高の更新を行う。
	/// </summary>
	/// <returns>実行結果</returns>
	[Command("update-daily-asset-progress")]
	public async Task<int> UpdateDailyAssetProgress() {
		await using var transaction = await this._db.Database.BeginTransactionAsync();
		var deletedRowCount = await this._db.DailyAssetProgresses.ExecuteDeleteAsync();
		this._logger.LogInformation("{deletedRowCount}件削除", deletedRowCount);
		var addedRowCount = await this._db.Database.ExecuteSqlRawAsync(
			@"
insert into `DailyAssetProgresses`
(`Date`, `InvestmentProductId`, `Rate`, `Amount`, `AverageRate`, `CurrencyRate`)
with recursive dates(Date) as (
	select
		(select min(`Date`) from `InvestmentProductRates`)
	union all
	select adddate(`Date`,1)
from dates where `Date` < (select max(`Date`) from `InvestmentProductRates`) )
select
	`sub1`.`Date` AS `Date`,
    `sub1`.`InvestmentProductId` AS `InvestmentProductId`,
    `sub1`.`Rate` AS `Rate`,
    `sub1`.`Amount` AS `Amount`,
    `sub1`.`AverageRate` AS `AverageRate`,
    `sub1`.`CurrencyRate` AS `CurrencyRate`
from
    (select
        `ip`.`InvestmentProductId` AS `InvestmentProductId`,
        `ip`.`Name` AS `ProductName`,
        `ip`.`Category` AS `Category`,
        `ip`.`Type` AS `Type`,
        `ip`.`InvestmentCurrencyUnitId` AS `InvestmentCurrencyUnitId`,
        `icu`.`Name` AS `CurrencyName`,
        `dates`.`Date` AS `Date`,
        (
        select
            sum(`ipa`.`Amount`)
        from
            `InvestmentProductAmounts` `ipa`
        where
            `ip`.`InvestmentProductId` = `ipa`.`InvestmentProductId`
            and `ipa`.`Date` <= `dates`.`Date`) AS `Amount`,
        (
        select
        	case when sum(`ipa`.`Amount`) = 0 then 0
        	else sum(`ipa`.`Amount` * `ipa`.`Price`) / sum(`ipa`.`Amount`) end
        from
            `InvestmentProductAmounts` `ipa`
        where
            `ip`.`InvestmentProductId` = `ipa`.`InvestmentProductId`
            and `ipa`.`Date` <= `dates`.`Date`) AS `AverageRate`,
       ifnull(`ipr`.`Value`,
        (select
        	`Value`
        from
        	`InvestmentProductRates`
        where 
        	`InvestmentProductId` = `ip`.`InvestmentProductId`
        	and `Date` = (select MAX(`Date`) from `InvestmentProductRates` where `InvestmentProductId` = `ip`.`InvestmentProductId` and `Date` <= `dates`.`Date`)
		)) AS `Rate`,
         ifnull((select
        	 `icr`.`Value`
        from
        	`InvestmentCurrencyRates` `icr`
        where 
        	`icr`.`InvestmentCurrencyUnitId` = `icu`.`Id`
        	and `Date` = (select MAX(`Date`) from `InvestmentCurrencyRates` where `InvestmentCurrencyUnitId` = `icr`.`InvestmentCurrencyUnitId` and `Date` <= `dates`.`Date`)
		),1) as `CurrencyRate`
    from
    	`dates`
    inner join `InvestmentProducts` `ip` on 1=1
    left join `InvestmentProductRates` `ipr` on
        `ip`.`InvestmentProductId` = `ipr`.`InvestmentProductId`
        and `dates`.`Date` = `ipr`.`Date`
    inner join `InvestmentCurrencyUnits` `icu` on
        `ip`.`InvestmentCurrencyUnitId` = `icu`.`Id`) `sub1`
where exists(
select 1
    from
        `InvestmentProductAmounts` `ipa`
    where
        `sub1`.`InvestmentProductId` = `ipa`.`InvestmentProductId`
        and `ipa`.`Date` <= `sub1`.`Date`)
"
		);
		this._logger.LogInformation("{addedRowCount}件登録", addedRowCount);
		await this._db.SaveChangesAsync();
		this._logger.LogInformation($"SaveChangesAsync");
		await transaction.CommitAsync();
		this._logger.LogInformation($"CommitAsync");
		return 0;
	}
}