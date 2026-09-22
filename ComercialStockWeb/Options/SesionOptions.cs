namespace Web.Options;

public sealed class SesionOptions
{
    public const string SectionName = "Autenticacion";
    public int DuracionMinutos { get; set; } = 30;
}
