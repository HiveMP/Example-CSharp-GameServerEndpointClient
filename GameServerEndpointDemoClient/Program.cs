using HiveMP.Lobby.Api;
using HiveMP.UserSession.Api;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GameServerEndpointDemoClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Enter the details for the user account you want to use to sign into HiveMP.");

            var emailAddress = ReadLine.Read("Email address: ");
            var password = ReadLine.ReadPassword("Password: ");

            var apiKey = Environment.GetEnvironmentVariable("API_KEY");

            Console.WriteLine("Logging in...");
            var sessionClient = new UserSessionClient(apiKey);
            var session = await sessionClient.AuthenticatePUTAsync(new AuthenticatePUTRequest
            {
                Authentication = new AuthenticationRequest
                {
                    EmailAddress = emailAddress,
                    MarketingPreferenceOptIn = false,
                    Metered = true,
                    PasswordHash = HashPassword(password),
                    ProjectId = null,
                    PromptForProject = null,
                    RequestedRole = null,
                    Tokens = null,
                    TwoFactor = null
                }
            });

            if (session.AuthenticatedSession == null)
            {
                Console.Error.WriteLine("Unable to authenticate with HiveMP!");
                return;
            }

            Console.WriteLine("Creating a game lobby...");
            var lobbyClient = new LobbyClient(session.AuthenticatedSession.ApiKey);
            var lobby = await lobbyClient.LobbyPUTAsync(new LobbyPUTRequest
            {
                Name = "Test Lobby",
                MaxSessions = 10
            });

            Console.WriteLine("Joining lobby...");
            var connectedSession = await lobbyClient.SessionPUTAsync(new HiveMP.Lobby.Api.SessionPUTRequest
            { 
                LobbyId = lobby.Id,
                SessionId = session.AuthenticatedSession.Id,
            });

            Console.WriteLine("Hitting Google Cloud Functions endpoint...");
            using (var client = new HttpClient())
            {
                var response = await client.PutAsync(
                    Environment.GetEnvironmentVariable("GCF_ENDPOINT"),
                    new StringContent(JsonConvert.SerializeObject(new
                    {
                        apiKey = session.AuthenticatedSession.ApiKey,
                        lobbyId = lobby.Id,
                    }), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Unable to provision game server:");
                    Console.Error.WriteLine(await response.Content.ReadAsStringAsync());
                    return;
                }

                response.EnsureSuccessStatusCode();
                var result = JsonConvert.DeserializeObject<GameServerResponse>(await response.Content.ReadAsStringAsync());

                Console.WriteLine("Game server is being provisioned: " + result.GameServerId);
            }
        }

        private class GameServerResponse
        {
            [JsonProperty("gameServerId")]
            public string GameServerId { get; set; }
        }

        private static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("HiveMPv1" + password))).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
