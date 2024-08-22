using ConsoleAppFramework;

using DataBase;

using HomeDashboardBatch.Configs.Parameters.Financial;
using HomeDashboardBatch.Filters;
using HomeDashboardBatch.Tasks.Financial;
using HomeDashboardBatch.Tasks.Financial.Investment.StockPriceInvestmentTrustScrapingTargets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host.CreateDefaultBuilder()
	.ConfigureServices((hostContext, services) => {
		var connectionString = hostContext.Configuration.GetConnectionString("Database");
		services.AddDbContext<HomeServerDbContext>(options =>
			options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)))
			.Configure<ConfigMoneyForwardScraping>(hostContext.Configuration.GetRequiredSection("Parameters:Financial:MoneyForwardScraping"))
			.AddScoped<IScrapingServiceTarget, SbiSecInvestmentTrust>()
			.AddScoped<IScrapingServiceTarget, YahooFinance>()
			.AddScoped<IScrapingServiceTarget, Minkabu>()
			.AddScoped<IScrapingServiceTarget, YahooFinanceCurrency>()
			.BuildServiceProvider();
	}).Build();
ConsoleApp.ServiceProvider = host.Services;

var app = ConsoleApp.Create();

app.Add<MoneyForwardScraping>("money-forward-scraping");
app.Add<InvestmentTask>("investment");
app.UseFilter<ErrorHandlerFilter>();
app.Run(args);
