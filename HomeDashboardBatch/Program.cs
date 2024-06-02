using DataBase;

using HomeDashboardBatch.Configs.Parameters.Financial;
using HomeDashboardBatch.Tasks.Financial.Investment;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;


var app = ConsoleApp.CreateBuilder(args)
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
	})
	.Build();

app.AddAllCommandType();
app.Run();
