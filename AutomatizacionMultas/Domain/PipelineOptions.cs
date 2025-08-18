namespace Obtenerarchivosdelamulta.Domain;

public sealed class PipelineOptions
{
    public string InputDir { get; }
    public string ConnectionString { get; }
    public string ContractsRoot { get; }
    public string OutputRoot { get; }

    public PipelineOptions(string inputDir, string connectionString, string contractsRoot, string outputRoot)
    {
        InputDir = inputDir ?? throw new ArgumentNullException(nameof(inputDir));
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        ContractsRoot = contractsRoot ?? throw new ArgumentNullException(nameof(contractsRoot));
        OutputRoot = outputRoot ?? throw new ArgumentNullException(nameof(outputRoot));
    }
}
