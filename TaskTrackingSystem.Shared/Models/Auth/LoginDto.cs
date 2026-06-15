using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Shared.Models.Auth
{
    public class LoginDto
    {
        [Required]
        public string UsernameOrEmail { get; set; } = string.Empty; // Username သို့မဟုတ် Email ကြိုက်တာနဲ့ login ဝင်လို့ရအောင် ပြုလုပ်ထားခြင်း

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}