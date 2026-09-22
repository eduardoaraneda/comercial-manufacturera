using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Application.DTO;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain;

namespace Infrastructure.Servicios;

public sealed class ArchivoInventario : IArchivoInventario
{
    public IReadOnlyList<FilaExcel> Leer(byte[] contenido)
    {
        if (contenido.Length is 0 or > 5_000_000) throw new ReglaNegocioException("El archivo debe ser XLSX y pesar como máximo 5 MB.");
        try
        {
            using var stream = new MemoryStream(contenido);
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                if (archive.Entries.Count > 1000 || archive.Entries.Sum(x => x.Length) > 25_000_000)
                    throw new ReglaNegocioException("El contenido del archivo es demasiado grande.");
            stream.Position = 0;
            using var book = new XLWorkbook(stream);
            if (book.Worksheets.Count != 1) throw new ReglaNegocioException("La plantilla debe tener una sola hoja.");
            var sheet = book.Worksheet(1);
            if (sheet.Cell(1, 1).GetString().Trim() != "SKU" || sheet.Cell(1, 2).GetString().Trim() != "Cantidad")
                throw new ReglaNegocioException("Los encabezados deben ser SKU y Cantidad. Descarga la plantilla.");
            var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (last > 1001) throw new ReglaNegocioException("Se permiten hasta 1000 filas por archivo.");
            var rows = new List<FilaExcel>();
            for (var row = 2; row <= last; row++)
            {
                var sku = sheet.Cell(row, 1);
                var qty = sheet.Cell(row, 2);
                if (sku.IsEmpty() && qty.IsEmpty()) continue;
                var item = new FilaExcel { NumeroFila = row };
                if (sku.HasFormula || qty.HasFormula)
                {
                    item.ErrorValidacion = "No se permiten fórmulas.";
                }
                else
                {
                    item.SKUOriginal = sku.GetString().Trim();
                    item.CantidadOriginal = qty.GetString().Trim();
                    if (item.SKUOriginal.Length is 0 or > 50) item.ErrorValidacion = "SKU vacío o demasiado largo.";
                    var raw = item.CantidadOriginal.Replace(',', '.');
                    if (!Regex.IsMatch(raw, @"^\d+(\.\d{1,3})?$")
                        || !decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity)
                        || quantity <= 0 || quantity > 1_000_000)
                        item.ErrorValidacion = "Cantidad inválida: usa un número positivo, hasta 3 decimales y sin separador de miles.";
                    else item.Cantidad = quantity;
                }
                item.SKUOriginal = item.SKUOriginal[..Math.Min(100, item.SKUOriginal.Length)];
                item.CantidadOriginal = item.CantidadOriginal[..Math.Min(100, item.CantidadOriginal.Length)];
                rows.Add(item);
            }
            if (rows.Count == 0) throw new ReglaNegocioException("El archivo no contiene productos.");
            foreach (var group in rows.Where(x => x.SKUOriginal.Length > 0).GroupBy(x => x.SKUOriginal, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                foreach (var row in group) row.ErrorValidacion = "SKU repetido en el archivo.";
            return rows;
        }
        catch (ReglaNegocioException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new ReglaNegocioException("No se pudo leer el Excel. Usa un archivo .xlsx válido basado en la plantilla."); }
    }

    public byte[] Plantilla() => Exportar("Ingreso", ["SKU", "Cantidad"], [new object?[] { "MUE-001", 10m }]);

    public byte[] Exportar(string hoja, string[] columnas, IEnumerable<object?[]> filas)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(hoja);
        for (var i = 0; i < columnas.Length; i++) sheet.Cell(1, i + 1).Value = columnas[i];
        var row = 2;
        foreach (var data in filas)
        {
            if (row > 10001) throw new ReglaNegocioException("La exportación supera 10000 filas. Reduce el período o filtra una bodega.");
            for (var i = 0; i < data.Length; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                switch (data[i])
                {
                    case decimal number: cell.Value = (double)number; break;
                    case int number: cell.Value = number; break;
                    case long number: cell.Value = number; break;
                    case DateTime date: cell.Value = date; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm"; break;
                    default: cell.Value = data[i]?.ToString() ?? ""; break; // Texto, nunca FormulaA1.
                }
            }
            row++;
        }
        sheet.Range(1, 1, Math.Max(1, row - 1), columnas.Length).SetAutoFilter();
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEDE4");
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().Width = 24;
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }
}
