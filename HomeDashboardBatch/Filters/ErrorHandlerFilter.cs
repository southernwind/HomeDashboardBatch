using ConsoleAppFramework;

using HomeDashboardBatch.Tasks.Financial;

using Microsoft.Extensions.Logging;

namespace HomeDashboardBatch.Filters;
internal class ErrorHandlerFilter(ErrorHandlerFilter next,
		ILogger<StockPriceInvestmentTrustScraping> logger) :ConsoleAppFilter(next){
	public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken) {
		try {
			await next.InvokeAsync(context, cancellationToken);
		} catch (BatchException ex){
			logger.LogError(ex, "{message}", ex.Message);
		}
	}
}
