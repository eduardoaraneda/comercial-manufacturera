using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed class ConfiguracionViewModel
{
    [Required(ErrorMessage = "Ingresa tu nombre."), StringLength(150), RegularExpression(@".*\S.*", ErrorMessage = "Ingresa un nombre válido.")]
    public string Nombre { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingresa tu correo."), EmailAddress(ErrorMessage = "Ingresa un correo válido."), StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingresa una contraseña."), StringLength(128, MinimumLength = 12, ErrorMessage = "Usa entre 12 y 128 caracteres."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Confirma tu contraseña."), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password)]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
