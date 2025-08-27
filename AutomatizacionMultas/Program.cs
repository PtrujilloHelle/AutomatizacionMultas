using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AutomatizacionMultas.classes.bots;
using AutomatizacionMultas.classes.configs;
using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obtenerarchivosdelamulta.Data;
using Obtenerarchivosdelamulta.Services;
using Obtenerarchivosdelamulta.Extraction;
using Obtenerarchivosdelamulta.Domain;

internal class Program
{
    static async Task Main(string[] args)
    {
        // ---------- Config ----------
        var configRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var descargaCfg = configRoot.GetSection("DescargaDeMultas")
                                    .Get<DescargaDeMultasConfig>()
            ?? throw new InvalidOperationException("No se encontró la sección DescargaDeMultas.");

        // ---------- 1) Descarga ----------
        await new DescargaDeMultas(descargaCfg).RunAsync();

        // ---------- 2) Post-proceso ----------
        var post = configRoot.GetSection("PostProcess");
        var contractsRoot = post["ContractsRoot"] ?? throw new InvalidOperationException("PostProcess.ContractsRoot faltante");
        var outputRoot = post["OutputRoot"] ?? throw new InvalidOperationException("PostProcess.OutputRoot faltante");
        var connString = post.GetSection("ConnectionStrings")["Db"]
                            ?? throw new InvalidOperationException("PostProcess.ConnectionStrings:Db faltante");

        string fechaFija = AutomatizacionMultas.classes.utils.ParseFixedDateHelper.ParseFixedDate(descargaCfg.SaveOptions.FixedDate);
        string fechaFolder = fechaFija.Replace('/', '-'); // ej. "12-08-2025"
        string inputDir = Path.Combine(outputRoot, fechaFolder);

        Console.WriteLine($"➡️ Iniciando post-proceso sobre: {inputDir}");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var opt = new PipelineOptions(
                    inputDir: inputDir,
                    connectionString: connString,
                    contractsRoot: contractsRoot,
                    outputRoot: Path.Combine(outputRoot, fechaFolder)
                );

                services.AddSingleton(opt);
                services.AddSingleton<PdfTextReader>();
                services.AddSingleton<ContractRepository>();
                services.AddSingleton<ContractFileService>();
                services.AddSingleton<IMultaExtractor, DgtExtractor>();
                services.AddSingleton<IMultaExtractor, MalagaExtractor>();
                services.AddSingleton<IMultaExtractor, FuengirolaExtractor>();
                services.AddSingleton<IMultaExtractor, BenalmadenaExtractor>();
                services.AddSingleton<MultaPipeline>();
            })
            .ConfigureLogging(b => b.ClearProviders().AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            }))
            .Build();

        using var scope = host.Services.CreateScope();
        var pipe = scope.ServiceProvider.GetRequiredService<MultaPipeline>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await pipe.RunAsync(cts.Token);

        Console.WriteLine("🏁 Flujo completo terminado.");
    }
}
