using System.Data;
using Microsoft.Data.SqlClient;
using Obtenerarchivosdelamulta.Domain;

namespace Obtenerarchivosdelamulta.Data;

public sealed class ContractRepository
{
    private readonly string _cs;

    public ContractRepository(PipelineOptions opt)
    {
        _cs = opt.ConnectionString;
    }

    public async Task<ContractMatch?> QueryExactSingleAsync(
      string matricula, DateTime fecha, TimeSpan hora, CancellationToken ct = default)
    {
        const string procName = "dbo.GetActiveContractByMatricula";

        using (var cn = new SqlConnection(_cs))
        using (var cmd = new SqlCommand(procName, cn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 30;

            cmd.Parameters.Add(new SqlParameter("@mat", SqlDbType.NVarChar, 32) { Value = matricula });
            cmd.Parameters.Add(new SqlParameter("@fecha", SqlDbType.Date) { Value = fecha.Date });
            cmd.Parameters.Add(new SqlParameter("@hora", SqlDbType.Time) { Value = hora });

            await cn.OpenAsync(ct);

            using (var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct))
            {
                if (await r.ReadAsync(ct))
                {
                    string suc = r["Sucursal"]?.ToString() ?? "";
                    string codCliente = r["CodCliente"]?.ToString() ?? "";

                    // CodHOC y Nacionalidad pueden no existir en versiones antiguas del SP → lectura defensiva
                    string? codHoc = null;
                    try
                    {
                        var v = r["CodHOC"];
                        codHoc = v == DBNull.Value ? null : v?.ToString();
                    }
                    catch { /* columna no existe */ }

                    string? nacionalidad = null;
                    try
                    {
                        var v = r["Nacionalidad"];
                        nacionalidad = v == DBNull.Value ? null : v?.ToString();
                    }
                    catch { /* columna no existe */ }

                    return new ContractMatch(suc, codCliente, codHoc, nacionalidad);
                }
            }
        }

        return null; // no había coincidencia
    }
}

// AHORA incluye CodHOC (nullable) y Nacionalidad (nullable)
public readonly record struct ContractMatch(string Sucursal, string CodCliente, string? CodHOC, string? Nacionalidad);
