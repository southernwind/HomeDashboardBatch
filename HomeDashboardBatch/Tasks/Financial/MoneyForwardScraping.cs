using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

using ConsoleAppFramework;

using Database.Tables;

using DataBase;

using HomeDashboardBatch.Configs.Parameters.Financial;

using HtmlAgilityPack.CssSelectors.NetCore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ScrapingLibrary;

namespace HomeDashboardBatch.Tasks.Financial;
public partial class MoneyForwardScraping(ILogger<MoneyForwardScraping> logger, HomeServerDbContext db, IOptions<ConfigMoneyForwardScraping> config) {
	private readonly ILogger _logger = logger;
	private readonly HomeServerDbContext _db = db;
	private readonly HttpClientWrapper _hcw = new();
	private readonly ConfigMoneyForwardScraping _config = config.Value;
	[GeneratedRegex(@"^.*gon\.authorizationParams=({.*?}).*$", RegexOptions.Singleline)]
	private partial Regex _loginCheckRegex();

	/// <summary>
	/// 支出・資産をMoneyForwardから取得して更新(日数指定)
	/// </summary>
	/// <param name="days">-d,取得日数</param>
	/// <returns>処理結果</returns>
	[Command("update-from-days")]
	public async Task<int> UpdateFromDays(
		int days) {
		this._logger.LogInformation("直近{days}日間のの財務データベース更新", days);
		var to = DateTime.Now.Date;
		var from = to.AddDays(-days);
		return await this.UpdateFromTerm(from, to);
	}

	/// <summary>
	/// 支出・資産をMoneyForwardから取得して更新(期間指定)
	/// </summary>
	/// <param name="from">-f,取得対象開始日</param>
	/// <param name="to">-t,取得対象終了日</param>
	/// <returns>処理結果</returns>
	[Command("update-from-term")]
	public async Task<int> UpdateFromTerm(
		DateTime from,
		DateTime to) {
		this._logger.LogInformation("{from}-{to}の財務データベース更新開始", from, to);

		await using var tran = await this._db.Database.BeginTransactionAsync();

		// 資産推移
		var maCount = 0;
		await foreach (var ma in this.GetAssets(from, to)) {
			var assets =
				ma.GroupBy(x => new { x.Date, x.Institution, x.Category })
					.Select(x => new MfAsset {
						Date = x.Key.Date,
						Institution = x.Key.Institution,
						Category = x.Key.Category,
						Amount = x.Sum(a => a.Amount),
						IsLocked = false
					}).ToArray();
			var existsRecords = this._db.MfAssets.Where(a => a.Date == assets.First().Date);
			var deleteAssetList = existsRecords.Where(x => !x.IsLocked);
			this._db.MfAssets.RemoveRange(deleteAssetList);

			await this._db.MfAssets.AddRangeAsync(assets.Where(x =>
				existsRecords
					.Where(er => er.IsLocked)
					.All(er => er.Institution != x.Institution || er.Category != x.Category)));
			this._logger.LogDebug("{date:yyyy/MM/dd}資産推移{length}件登録", ma.First().Date, assets.Length);
			maCount += assets.Length;
		}
		this._logger.LogInformation("資産推移 計{maCount}件登録",maCount);

		// 取引履歴
		var mtCount = 0;
		await foreach (var mt in this.GetTransactions(from, to)) {
			var ids = mt.Select(x => x.TransactionId).ToArray();
			var existsRecords = this._db.MfTransactions.Where(t => ids.Contains(t.TransactionId));
			var deleteTransactionList = existsRecords.Where(x => !x.IsLocked);
			this._db.MfTransactions.RemoveRange(deleteTransactionList);
			await this._db.MfTransactions.AddRangeAsync(
				mt.Where(x =>
						existsRecords
							.Where(er => er.IsLocked)
							.All(er => er.TransactionId != x.TransactionId)));
			this._logger.LogInformation("{date:yyyy/MM}取引履歴{length}件登録", mt.First()?.Date, mt.Length);
			mtCount += mt.Length;
		}
		this._logger.LogInformation("取引履歴 計{mtCount}件登録", mtCount);

		await this._db.SaveChangesAsync();
		this._logger.LogDebug("SaveChanges");
		await tran.CommitAsync();
		this._logger.LogDebug("Commit");

		this._logger.LogInformation("{from}-{to}の財務データベース更新正常終了", from, to);

		return 0;
	}

	/// <summary>
	/// 取引履歴の取得
	/// </summary>
	/// <returns></returns>
	private async IAsyncEnumerable<MfTransaction[]> GetTransactions(DateTime from, DateTime to) {
		await this.LoginAsync();
		for (var date = from; date <= to; date = new DateTime(date.AddMonths(1).Year, date.AddMonths(1).Month, 1)) {
			var year = date.Year;
			var month = date.Month;
			this._hcw.CookieContainer.Add(new Cookie("cf_last_fetch_from_date", $"{year}/{month:D2}/01", "/",
				"moneyforward.com"));
			var response = await this._hcw.GetAsync("https://moneyforward.com/cf");
			if (!response.IsSuccessStatusCode) {
				throw new BatchException($"HTTPリクエスト1 エラー statusCode={response.StatusCode} url={response.RequestMessage?.RequestUri}");
			};
			var htmlDoc = await response.ToHtmlDocumentAsync();

			var dateRange = htmlDoc.DocumentNode.QuerySelector(".date_range h2").InnerText;
			if (!dateRange.StartsWith($"{year}/{month:D2}")) {
				continue;
			}

			var list = htmlDoc
				.DocumentNode
				.QuerySelectorAll(@".list_body .transaction_list")
				.Select(tr => tr.QuerySelectorAll("td"))
				.Select(tdList => new MfTransaction {
					TransactionId =
						tdList[0].QuerySelector("input#user_asset_act_id").GetAttributeValue("value", null),
					IsCalculateTarget = tdList[0].QuerySelector("i.icon-check") != null,
					Date = new DateTime(year, month, int.Parse(tdList[1].InnerText.Trim().Substring(3, 2))),
					Content = tdList[2].InnerText.Trim(),
					Amount = int.Parse(tdList[3].QuerySelector("span").InnerText.Trim().Replace(",", "")),
					Institution = tdList[4].GetAttributeValue("title", null),
					LargeCategory = tdList[5].InnerText.Trim(),
					MiddleCategory = tdList[6].InnerText.Trim(),
					Memo = tdList[7].InnerText.Trim()
				});

			if (list.Any()) {
				yield return list.ToArray();
			}
		}
	}

	/// <summary>
	/// 資産推移の取得
	/// </summary>
	/// <returns></returns>
	private async IAsyncEnumerable<MfAsset[]> GetAssets(DateTime from, DateTime to) {
		await this.LoginAsync();
		for (var date = from; date <= to.AddDays(1); date = date.AddDays(1)) {
			var response = await this._hcw.GetAsync($"https://moneyforward.com/bs/history/list/{date:yyyy-MM-dd}");
			if (!response.IsSuccessStatusCode) {
				throw new BatchException($"HTTPリクエスト1 エラー statusCode={response.StatusCode} url={response.RequestMessage?.RequestUri}");
			};
			var htmlDoc = await response.ToHtmlDocumentAsync();
			var list = htmlDoc
				.DocumentNode
				.QuerySelectorAll(@"#history-list tbody tr")
				.Select(tr => tr.QuerySelectorAll("td"))
				.Select(tdList => new MfAsset {
					Date = date,
					Institution = tdList[0].InnerText.Trim(),
					Category = tdList[1].InnerText.Trim(),
					Amount = int.Parse(tdList[2].InnerText.Trim().Replace("円", "").Replace(",", ""))
				});
			if (list.Any()) {
				yield return list.ToArray();
			}
		}
	}
	/// <summary>
	/// ログインする。
	/// </summary>
	private async Task LoginAsync() {
		var response1 = await this._hcw.GetAsync("https://moneyforward.com/cf");
		if (!response1.IsSuccessStatusCode) {
			throw new BatchException($"HTTPリクエスト1 エラー statusCode={response1.StatusCode} url={response1.RequestMessage?.RequestUri}");
		}
		var htmlDoc1 = await response1.ToHtmlDocumentAsync();
		if (!this._loginCheckRegex().IsMatch(htmlDoc1.Text)) {
			// ログイン済みとみなす
			return;
		}
		var json1 = this._loginCheckRegex().Replace(htmlDoc1.Text, "$1");
		var urlParams1 = JsonSerializer.Deserialize<UrlParams>(json1) ?? throw new BatchException("json1パラメータ異常");
		var csrfParam = htmlDoc1.DocumentNode.QuerySelector(@"meta[name='csrf-param']").GetAttributeValue("content", null) ?? throw new BatchException("csrf-param取得エラー");
		var csrfToken = htmlDoc1.DocumentNode.QuerySelector(@"meta[name='csrf-token']").GetAttributeValue("content", null) ?? throw new BatchException("csrf-token取得エラー");

		// パスワード入力画面
		var content = new FormUrlEncodedContent(new Dictionary<string, string> {
				{ csrfParam, csrfToken},
				{ "_method","post" },
				{ "clientId", urlParams1.ClientId},
				{ "redirectUri", urlParams1.RedirectUri},
				{ "responseType", urlParams1.ResponseType},
				{ "scope", urlParams1.Scope},
				{ "state", urlParams1.State},
				{ "nonce", urlParams1.Nonce},
				{ "selectAccount", urlParams1.SelectAccount},
				{ "mfid_user[email]", this._config.Id},
				{ "mfid_user[password]",this._config.Password }
			});
		var response2 = await this._hcw.PostAsync("https://id.moneyforward.com/sign_in/email", content);
		if (!response2.IsSuccessStatusCode) {
			throw new BatchException($"HTTPリクエスト3 エラー statusCode={response2.StatusCode} url={response2.RequestMessage?.RequestUri}");
		}
		if (response2.RequestMessage?.RequestUri?.AbsoluteUri != "https://moneyforward.com/cf") {
			throw new BatchException("ログイン失敗");
		}
	}

	private class UrlParams {
		[JsonPropertyName("clientId")]
		public string ClientId {
			get;
			set;
		} = null!;
		[JsonPropertyName("redirectUri")]
		public string RedirectUri {
			get;
			set;
		} = null!;
		[JsonPropertyName("responseType")]
		public string ResponseType {
			get;
			set;
		} = null!;
		[JsonPropertyName("scope")]
		public string Scope {
			get;
			set;
		} = null!;
		[JsonPropertyName("state")]
		public string State {
			get;
			set;
		} = null!;
		[JsonPropertyName("nonce")]
		public string Nonce {
			get;
			set;
		} = null!;
		[JsonPropertyName("selectAccount")]
		public string SelectAccount {
			get;
			set;
		} = null!;
	}
}
