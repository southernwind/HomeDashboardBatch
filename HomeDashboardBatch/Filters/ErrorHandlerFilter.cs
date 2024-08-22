using ConsoleAppFramework;

using HomeDashboardBatch.Tasks.Financial;

using Microsoft.Extensions.Logging;

namespace HomeDashboardBatch.Filters;
internal class ErrorHandlerFilter(ConsoleAppFilter next,
#pragma warning disable CS9107 // パラメーターは外側の型の状態にキャプチャされ、その値も基底コンストラクターに渡されます。この値は、基底クラスでもキャプチャされる可能性があります。
		ILogger<InvestmentTask> logger) :ConsoleAppFilter(next){
#pragma warning restore CS9107 // パラメーターは外側の型の状態にキャプチャされ、その値も基底コンストラクターに渡されます。この値は、基底クラスでもキャプチャされる可能性があります。
	public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken) {
		try {
			await next.InvokeAsync(context, cancellationToken);
		} catch (Exception ex){
			logger.LogError(ex, "ErrorHandling : {message}", ex.Message);
			throw;
		}
	}
}
