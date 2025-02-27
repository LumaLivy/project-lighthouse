using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Kettu;
using LBPUnion.ProjectLighthouse.Logging;

namespace LBPUnion.ProjectLighthouse.Types.Settings
{
    [Serializable]
    public class ServerSettings
    {

        public const int CurrentConfigVersion = 13; // MUST BE INCREMENTED FOR EVERY CONFIG CHANGE!
        static ServerSettings()
        {
            if (ServerStatics.IsUnitTesting) return; // Unit testing, we don't want to read configurations here since the tests will provide their own

            if (File.Exists(ConfigFileName))
            {
                string configFile = File.ReadAllText(ConfigFileName);

                Instance = JsonSerializer.Deserialize<ServerSettings>(configFile) ?? throw new ArgumentNullException(nameof(ConfigFileName));

                if (Instance.ConfigVersion >= CurrentConfigVersion) return;

                Logger.Log($"Upgrading config file from version {Instance.ConfigVersion} to version {CurrentConfigVersion}", LoggerLevelConfig.Instance);
                Instance.ConfigVersion = CurrentConfigVersion;
                configFile = JsonSerializer.Serialize
                (
                    Instance,
                    typeof(ServerSettings),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                );

                File.WriteAllText(ConfigFileName, configFile);
            }
            else
            {
                string configFile = JsonSerializer.Serialize
                (
                    new ServerSettings(),
                    typeof(ServerSettings),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                );

                File.WriteAllText(ConfigFileName, configFile);

                Logger.Log
                (
                    "The configuration file was not found. " +
                    "A blank configuration file has been created for you at " +
                    $"{Path.Combine(Environment.CurrentDirectory, ConfigFileName)}",
                    LoggerLevelConfig.Instance
                );

                Environment.Exit(1);
            }
        }

        public bool InfluxEnabled { get; set; }
        public bool InfluxLoggingEnabled { get; set; }
        public string InfluxOrg { get; set; } = "lighthouse";
        public string InfluxBucket { get; set; } = "lighthouse";
        public string InfluxToken { get; set; } = "";
        public string InfluxUrl { get; set; } = "http://localhost:8086";

        public string EulaText { get; set; } = "";

        public string DbConnectionString { get; set; } = "server=127.0.0.1;uid=root;pwd=lighthouse;database=lighthouse";

        public string ExternalUrl { get; set; } = "http://localhost:10060";
        public string ServerDigestKey { get; set; }
        public bool UseExternalAuth { get; set; }

        public bool CheckForUnsafeFiles { get; set; } = true;

        public bool RegistrationEnabled { get; set; } = true;

        /// <summary>
        ///     The maximum amount of slots allowed on users' earth
        /// </summary>
        public int EntitledSlots { get; set; } = 50;

        public int ListsQuota { get; set; } = 50;

        public int PhotosQuota { get; set; } = 500;

        public bool GoogleAnalyticsEnabled { get; set; }

        public string GoogleAnalyticsId { get; set; } = "";

        public bool BlockDeniedUsers = true;

        #region Meta

        [NotNull]
        public static ServerSettings Instance;

        [JsonPropertyName("ConfigVersionDoNotModifyOrYouWillBeSlapped")]
        public int ConfigVersion { get; set; } = CurrentConfigVersion;

        public const string ConfigFileName = "lighthouse.config.json";

        #endregion Meta

    }
}