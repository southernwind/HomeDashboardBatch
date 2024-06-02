using DataBase;

using HomeDashboardBatch.Configs.Parameters.Financial;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


var app = ConsoleApp.CreateBuilder(args)
	.ConfigureServices((hostContext, services) => {
		var connectionString = hostContext.Configuration.GetConnectionString("Database");
		services.AddDbContext<HomeServerDbContext>(options =>
			options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
		services.Configure<ConfigMoneyForwardScraping>(hostContext.Configuration.GetRequiredSection("Parameters:Financial:MoneyForwardScraping"));
	})
	.Build();

app.AddAllCommandType();
app.Run();
