namespace HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;
public interface IScrapingServiceTarget : IDisposable {
	/// <summary>
	/// スクレイピング実行
	/// </summary>
	public Task ExecuteAsync(int investmentProductId, string key);
}
