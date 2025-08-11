//Program.cs

using System;
using System.IO;
using AutomatizacionMultas.classes;
using AutomatizacionMultas.classes.bots;
using AutomatizacionMultas.classes.configs;
using Microsoft.Extensions.Configuration;

internal class Program
{
    static async Task Main(string[] args)
    {
        // ---------- Carga JSON ----------
        var configRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())      // o AppContext.BaseDirectory
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var descargaCfg = configRoot.GetSection("DescargaDeMultas")
                                    .Get<DescargaDeMultasConfig>()
            ?? throw new InvalidOperationException("No se encontró la sección DescargaDeMultas.");

        // ---------- Ejecutar ----------
        await new DescargaDeMultas(descargaCfg).RunAsync();
    }
}