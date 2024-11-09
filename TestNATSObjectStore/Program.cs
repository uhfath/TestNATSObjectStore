using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.ObjectStore;
using System.Diagnostics;

namespace TestNATSObjectStore
{
	internal static class Program
	{
		private static readonly ActivitySource CurrentActivitySource = new ActivitySource("NATS-debug");
		private static readonly byte[] TempBuffer = new byte[1024];

		private static async Task LoadObjectAsync(IServiceProvider serviceProvider)
		{
			using var activityListener = new ActivityListener
			{
				ShouldListenTo = _ => true,
				SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			};

			using var activity = CurrentActivitySource.StartActivity(ActivityKind.Client);
			ActivitySource.AddActivityListener(activityListener);

			var natsObjContext = serviceProvider.GetRequiredService<INatsObjContext>();
			var objectStore = await natsObjContext.CreateObjectStoreAsync("ASSETS");
			await objectStore.PutAsync(Guid.NewGuid().ToString(), TempBuffer);
		}

		private static async Task Main(string[] args)
		{
			var services = new ServiceCollection();

			services
				.AddLogging(options =>
				{
					options.SetMinimumLevel(LogLevel.Debug);

					options.Configure(builder =>
					{
						builder.ActivityTrackingOptions = ActivityTrackingOptions.None
							| ActivityTrackingOptions.SpanId
							| ActivityTrackingOptions.TraceId
							| ActivityTrackingOptions.ParentId
							| ActivityTrackingOptions.Tags
							| ActivityTrackingOptions.Baggage
						;
					});

					options.AddSimpleConsole(builder =>
					{
						builder.IncludeScopes = true;
					});
				})
				.AddSingleton<INatsConnection>(sp => new NatsConnection(new NatsOpts
				{
					Name = "NATS-debug",
					LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
				}))
				.AddTransient<INatsJSContext, NatsJSContext>()
				.AddTransient<INatsObjContext, NatsObjContext>()
			;

			await using var serviceProvider = services.BuildServiceProvider();

			await LoadObjectAsync(serviceProvider);
			await LoadObjectAsync(serviceProvider);
		}
	}
}
