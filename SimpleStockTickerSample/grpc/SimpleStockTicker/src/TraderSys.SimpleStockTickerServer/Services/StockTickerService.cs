
namespace TraderSys.SimpleStockTickerServer.Services;
    public class StockTickerService : Protos.SimpleStockTicker.SimpleStockTickerBase
    {
        private readonly IStockPriceSubscriberFactory _subscriberFactory;
        private readonly ILogger<StockTickerService> _logger;
     private readonly Random _random = new Random();

    public StockTickerService(IStockPriceSubscriberFactory subscriberFactory, ILogger<StockTickerService> logger)
        {
            _subscriberFactory = subscriberFactory;
            _logger = logger;
        }

        public override async Task Subscribe(SubscribeRequest request,
            IServerStreamWriter<StockTickerUpdate> responseStream, ServerCallContext context)
        {
        try
        {
            var subscriber = _subscriberFactory.GetSubscriber(request.Symbols.ToArray());

            subscriber.Update += async (sender, args) =>
                await WriteUpdateAsync(responseStream, args.Symbol, args.Price);

            _logger.LogInformation("Subscription started.");

            await AwaitCancellation(context.CancellationToken);

            subscriber.Dispose();

            _logger.LogInformation("Subscription finished.");
        }
        catch(RpcException ex)
        {
            throw;
        }
        }

        private async Task WriteUpdateAsync(IServerStreamWriter<StockTickerUpdate> stream, string symbol, decimal price)
        {
            try
            {
            if (_random.NextDouble() > 0.5)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"Unavailable"));
            }
            await stream.WriteAsync(new StockTickerUpdate
                {
                    Symbol = symbol,
                    Price = Convert.ToDouble(price),
                    Time = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to write message: {e.Message}");
            throw;
            }
        }

        private static Task AwaitCancellation(CancellationToken token)
        {
            var completion = new TaskCompletionSource<object>();
            token.Register(() => completion.SetResult(null));
            return completion.Task;
        }
    }