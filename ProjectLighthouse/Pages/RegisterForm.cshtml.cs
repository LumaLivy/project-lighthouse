using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using JetBrains.Annotations;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Pages.Layouts;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse.Pages
{
    public class RegisterForm : BaseLayout
    {
        public RegisterForm(Database database) : base(database)
        {}

        public string Error { get; private set; }
        public bool WasRegisterRequest { get; private set; }

        [UsedImplicitly]
        [SuppressMessage("ReSharper", "SpecifyStringComparison")]
        public async Task<IActionResult> OnPost(string username, string password, string confirmPassword)
        {
            if (!ServerSettings.Instance.RegistrationEnabled) return this.NotFound();

            if (string.IsNullOrWhiteSpace(username))
            {
                this.Error = "The username field is blank.";
                return this.Page();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                this.Error = "Password field is required.";
                return this.Page();
            }

            if (password != confirmPassword)
            {
                this.Error = "Passwords do not match!";
                return this.Page();
            }

            bool userExists = await this.Database.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower()) != null;
            if (userExists)
            {
                this.Error = "The username you've chosen is already taken.";
                return this.Page();
            }

            User user = await this.Database.CreateUser(username, HashHelper.BCryptHash(password));

            WebToken webToken = new()
            {
                UserId = user.UserId,
                UserToken = HashHelper.GenerateAuthToken(),
            };

            this.Database.WebTokens.Add(webToken);
            await this.Database.SaveChangesAsync();

            this.Response.Cookies.Append("LighthouseToken", webToken.UserToken);

            return this.RedirectToPage(nameof(LandingPage));
        }

        [UsedImplicitly]
        [SuppressMessage("ReSharper", "SpecifyStringComparison")]
        public IActionResult OnGet()
        {
            this.Error = string.Empty;
            if (!ServerSettings.Instance.RegistrationEnabled) return this.NotFound();

            return this.Page();
        }
    }
}