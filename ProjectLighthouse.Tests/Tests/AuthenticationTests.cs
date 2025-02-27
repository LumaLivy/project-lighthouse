using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Settings;
using Xunit;

namespace LBPUnion.ProjectLighthouse.Tests
{
    public class AuthenticationTests : LighthouseTest
    {
        [Fact]
        public async Task ShouldReturnErrorOnNoPostData()
        {
            HttpResponseMessage response = await this.Client.PostAsync("/LITTLEBIGPLANETPS3_XML/login", null!);
            Assert.False(response.IsSuccessStatusCode);
            #if NET6_0_OR_GREATER
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest);
            #else
            Assert.True(response.StatusCode == HttpStatusCode.NotAcceptable);
            #endif
        }

        [DatabaseFact]
        public async Task ShouldReturnWithValidData()
        {
            HttpResponseMessage response = await this.AuthenticateResponse();
            Assert.True(response.IsSuccessStatusCode);
            string responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("MM_AUTH=", responseContent);
            Assert.Contains(ServerStatics.ServerName, responseContent);
        }

        [DatabaseFact]
        public async Task CanSerializeBack()
        {
            LoginResult loginResult = await this.Authenticate();

            Assert.NotNull(loginResult);
            Assert.NotNull(loginResult.AuthTicket);
            Assert.NotNull(loginResult.LbpEnvVer);

            Assert.Contains("MM_AUTH=", loginResult.AuthTicket);
            Assert.Equal(ServerStatics.ServerName, loginResult.LbpEnvVer);
        }

        [DatabaseFact]
        public async Task CanUseToken()
        {
            LoginResult loginResult = await this.Authenticate();

            HttpResponseMessage response = await this.AuthenticatedRequest("/LITTLEBIGPLANETPS3_XML/enterLevel/1", loginResult.AuthTicket);
            string responseContent = await response.Content.ReadAsStringAsync();

            Assert.False(response.StatusCode == HttpStatusCode.Forbidden);
        }

        [DatabaseFact]
        public async Task ShouldReturnForbiddenWhenNotAuthenticated()
        {
            HttpResponseMessage response = await this.Client.GetAsync("/LITTLEBIGPLANETPS3_XML/announce");
            Assert.False(response.IsSuccessStatusCode);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden);
        }
    }
}