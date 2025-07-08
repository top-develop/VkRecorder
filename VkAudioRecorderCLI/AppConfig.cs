using Microsoft.Extensions.Configuration;

namespace VkAudioRecorderCLI
{
    /// <summary>
    /// Stellt zentralen Zugriff auf die Anwendungskonfiguration bereit.
    /// Lädt Einstellungen aus appsettings.json (falls vorhanden) und bietet stark typisierte Eigenschaften für gängige Pfade.
    /// </summary>
    public static class AppConfig
    {
        /// <summary>
        /// Die Wurzel-Konfigurationsinstanz, geladen aus appsettings.json und Umgebung.
        /// </summary>
        public static IConfigurationRoot Config { get; }

        /// <summary>
        /// Statischer Konstruktor initialisiert das Konfigurationssystem.
        /// Setzt das Basisverzeichnis auf das Anwendungsverzeichnis und lädt appsettings.json, falls vorhanden.
        /// </summary>
        static AppConfig()
        {
            Config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("MyConfiguration.json", optional: true, reloadOnChange: true)
                .Build();
        }

        /// <summary>
        /// Gibt das Verzeichnis für gespeicherte Tracks zurück.
        /// Wird aus "Paths:TracksDirectory" in appsettings.json gelesen, Standard ist "Tracks".
        /// </summary>
        public static string TracksDirectory => Config["Paths:TracksDirectory"] ?? "Tracks";

        /// <summary>
        /// Gibt das Verzeichnis für Logdateien zurück.
        /// Wird aus "Paths:LogFilePath" in appsettings.json gelesen, Standard ist "Logs".
        /// </summary>
        public static string LogDirectory => Config["Paths:LogFilePath"] ?? "Logs";

        /// <summary>
        /// Gibt den vollständigen Pfad zur Logdatei zurück.
        /// Kombiniert LogDirectory mit "log.txt".
        /// </summary>
        public static string LogFilePath => Path.Combine(LogDirectory, "log.txt");
    }
}

