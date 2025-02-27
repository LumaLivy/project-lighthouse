#nullable enable
using System.Threading.Tasks;
using JetBrains.Annotations;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Pages.Layouts;
using LBPUnion.ProjectLighthouse.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Pages
{
    public class LoginForm : BaseLayout
    {
        public LoginForm(Database database) : base(database)
        {}

        public string Error { get; private set; }

        public bool WasLoginRequest { get; private set; }

        [UsedImplicitly]
        public async Task<IActionResult> OnPost(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                this.Error = "The username field is required.";
                return this.Page();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                this.Error = "The password field is required.";
                return this.Page();
            }

            User? user = await this.Database.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                this.Error = "The username or password you entered is invalid.";
                return this.Page();
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                this.Error = "The username or password you entered is invalid.";
                return this.Page();
            }

            WebToken webToken = new()
            {
                UserId = user.UserId,
                UserToken = HashHelper.GenerateAuthToken(),
            };

            this.Database.WebTokens.Add(webToken);
            await this.Database.SaveChangesAsync();

            this.Response.Cookies.Append("LighthouseToken", webToken.UserToken);

            if (user.PasswordResetRequired) return this.Redirect("~/passwordResetRequired");

            return this.RedirectToPage(nameof(LandingPage));
        }

        [UsedImplicitly]
        public async Task<IActionResult> OnGet()
        {
            this.Error = string.Empty;
            return this.Page();
        }
    }
}