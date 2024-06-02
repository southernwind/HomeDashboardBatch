namespace HomeDashboardBatch.Utilities;

internal class BatchException:Exception {
	public BatchException(string message) : base(message) {
	}
	public BatchException(string message, Exception innerException) : base(message,innerException) { }
}
