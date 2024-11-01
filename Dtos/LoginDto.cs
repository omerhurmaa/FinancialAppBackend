using System.ComponentModel.DataAnnotations;

namespace MyBackendApp.Dtos
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre gereklidir.")]
        public string Password { get; set; } = string.Empty;
    }
}
