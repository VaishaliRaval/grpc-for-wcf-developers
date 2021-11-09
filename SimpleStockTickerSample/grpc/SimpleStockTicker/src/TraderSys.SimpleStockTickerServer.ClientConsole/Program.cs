using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TraderSys.SimpleStockTickerServer.Protos;

namespace TraderSys.SimpleStockTickerServer.ClientConsole;
class Program
{
    static async Task Main(string[] args)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", true);
        //using var channel = GrpcChannel.ForAddress("https://localhost:5001");
        using var channel =CreateChannel();
          var client = new SimpleStockTicker.SimpleStockTickerClient(channel);

        var request = new SubscribeRequest();
        request.Symbols.AddRange(args);
        using var stream = client.Subscribe(request);

        var tokenSource = new CancellationTokenSource();
        var task = DisplayAsync(stream.ResponseStream, tokenSource.Token);

        WaitForExitKey();

        tokenSource.Cancel();
        await task;
    }

    static async Task DisplayAsync(IAsyncStreamReader<StockTickerUpdate> stream, CancellationToken token)
    {
        try
        {
            await foreach (var update in stream.ReadAllAsync(token))
            {
                try
                {
                    Console.WriteLine($"{update.Symbol}: {update.Price}");
                }
                catch (RpcException e)
                {
                    #region Print failure
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Status.Detail);
                    Console.ResetColor();
                    #endregion
                }
            }
        }
        catch (RpcException e)
        {
            if (e.StatusCode == StatusCode.Cancelled)
            {
                return;
            }

        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Finished.");
        }
    }

    static void WaitForExitKey()
    {
        Console.WriteLine("Press E to exit...");

        char ch = ' ';

        while (ch != 'e')
        {
            ch = char.ToLowerInvariant(Console.ReadKey().KeyChar);
        }
    }

    private static GrpcChannel CreateChannel()
    {
        var httpHandler = new HttpClientHandler();
        // Return `true` to allow certificates that are untrusted/invalid
        httpHandler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        var methodConfig = new MethodConfig
        {
            Names = { MethodName.Default },
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 10,
                InitialBackoff = TimeSpan.FromSeconds(0.5),
                MaxBackoff = TimeSpan.FromSeconds(0.5),
                BackoffMultiplier = 1,
                RetryableStatusCodes = { StatusCode.Unavailable }
            }
        };

        return GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            ServiceConfig = new ServiceConfig { MethodConfigs = { methodConfig } },
            HttpHandler = httpHandler
        });
    }

    private static async Task<string> GetRetryCount(Task<Metadata> responseHeadersTask)
    {
        var headers = await responseHeadersTask;
        var previousAttemptCount = headers.GetValue("grpc-previous-rpc-attempts");
        return previousAttemptCount != null ? $"(retry count: {previousAttemptCount})" : string.Empty;
    }
}