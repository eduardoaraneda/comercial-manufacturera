using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo."), EmailAddress(ErrorMessage = "Ingresa un correo válido."), StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingresa tu contraseña."), StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public bool Recordar { get; set; }
    public bool SesionExpirada { get; set; }
    public string? ReturnUrl { get; set; }
    public bool MostrarConfiguracion { get; set; }
    public bool BaseDisponible { get; set; } = true;
}
