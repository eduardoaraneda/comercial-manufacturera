namespace Application.DTO;

public sealed record AccesoResultado(bool Exitoso, string? Error = null);

public sealed record ResumenInicio(int Productos, int Bodegas, int Cotizaciones, int Ventas);
