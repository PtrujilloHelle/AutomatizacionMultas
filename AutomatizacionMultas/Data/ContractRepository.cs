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

    public IReadOnlyList<ContractMatch> QueryExact(string matricula, DateTime fecha, TimeSpan hora)
    {
        const string sql = @"
SET DATEFORMAT dmy;
SELECT 
      h.[Nº sucursal]  AS Sucursal,
      h.[Cód_ cliente] AS CodCliente
FROM [HELLE HOLLIS].[dbo].[HELLE AUTO, S_A_U_$Hist_ contrato] AS h
WHERE h.[Matrícula] = @mat
  AND h.[Fecha salida]       <= @fechaTxt
  AND h.[Fecha entrada real] >= @fechaTxt
  AND h.[Hora salida]        <= @horaTxt
  AND h.[Hora entrada real]  <= @horaTxt
ORDER BY h.[Fecha salida] DESC, h.[Hora salida] DESC;";

        var list = new List<ContractMatch>();

        using var cn = new SqlConnection(_cs);
        using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add(new SqlParameter("@mat", SqlDbType.NVarChar, 32) { Value = matricula });
        cmd.Parameters.Add(new SqlParameter("@fechaTxt", SqlDbType.VarChar, 10) { Value = fecha.ToString("dd/MM/yyyy") });
        cmd.Parameters.Add(new SqlParameter("@horaTxt", SqlDbType.VarChar, 5) { Value = hora.ToString(@"hh\:mm") });

        cn.Open();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ContractMatch(
                r["Sucursal"]?.ToString() ?? "",
                r["CodCliente"]?.ToString() ?? ""
            ));
        }
        return list;
    }
}

public readonly record struct ContractMatch(string Sucursal, string CodCliente);
