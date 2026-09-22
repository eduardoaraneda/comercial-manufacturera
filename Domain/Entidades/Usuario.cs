namespace Domain.Entidades;

// Perfil de negocio/auditoria. Las credenciales pertenecen a ASP.NET Core Identity.
public sealed class Usuario
{
    public int UsuarioId { get; set; }
    public string IdentidadId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
