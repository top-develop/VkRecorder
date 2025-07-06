using Microsoft.Extensions.Configuration;

namespace VkAudioRecorderCLI
{
    /// <summary>
    /// Stellt statische Hilfsmethoden zum Zugriff auf Konfigurationswerte aus appsettings.json bereit.
    /// Nutzt Microsoft.Extensions.Configuration für flexibles Konfigurationsmanagement.
    /// </summary>
    public static class ConfigHelper
    {
        /// <summary>
        /// Interne Instanz der geladenen Konfiguration.
        /// </summary>
        private static readonly IConfigurationRoot _config;

        /// <summary>
        /// Statischer Konstruktor lädt die Konfiguration aus appsettings.json.
        /// Setzt das Basisverzeichnis auf das Anwendungsverzeichnis und ermöglicht automatisches Neuladen bei Änderungen.
        /// </summary>
        static ConfigHelper()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }

        /// <summary>
        /// Gibt den Wert für einen Konfigurationsschlüssel zurück.
        /// </summary>
        /// <param name="key">Der Schlüssel (z.B. "Paths:TracksDirectory").</param>
        /// <returns>Der Wert als String, oder ein leerer String, falls nicht gefunden.</returns>
        public static string Get(string key)
        {
            return _config[key] ?? string.Empty;
        }
    }
}

