using System;
using System.IO;
using System.Threading;
using Serilog;

namespace VkAudioRecorderCLI
{
    /// <summary>
    /// Einstiegspunkt der Anwendung. Steuert die Hauptlogik für das Aufzeichnen, Segmentieren und Exportieren von Audio-Tracks.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Hauptmethode. Initialisiert Logging, startet die Metadaten-Erfassung und steuert die Track-Aufzeichnung.
        /// </summary>
        static void Main(string[] args)
        {
            // Setzt die Konsolenausgabe auf UTF-8 für korrekte Zeichendarstellung
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Lese Log- und Track-Verzeichnis aus der Konfiguration
            var logPath = Environment.ExpandEnvironmentVariables(ConfigHelper.Get("Paths:LogFilePath"));
            var outputDir = Environment.ExpandEnvironmentVariables(ConfigHelper.Get("Paths:TracksDirectory"));
            var vkMusicSiteUrl = Environment.ExpandEnvironmentVariables(ConfigHelper.Get("MusicPageUrl"));
            Directory.CreateDirectory(logPath);

            // Pfad zur Logdatei
            var logFile = Path.Combine(logPath, "log.txt");

            // Serilog-Konfiguration: Konsole und Datei, täglicher Wechsel, max. 30 Dateien
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    logFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30
                )
                .CreateLogger();

            Log.Information("Anwendung gestartet.");
            // Startet den Metadaten-Fetcher (z.B. öffnet einen Browser)
            VkMetadataFetcher.StartBrowser();

            // warten 5 Sekunden, um sicherzustellen, dass die Seite geladen ist
            Log.Information("Warte 5 Sekunden auf das Laden der VK Music Seite...");
            Thread.Sleep(5000);

            // Navigiert zur VK Music Seite
            Log.Information("Navigiere zur VK Music Seite: {VkMusicSiteUrl}", vkMusicSiteUrl);
            VkMetadataFetcher.NavigateToMusicPage(vkMusicSiteUrl);

            //warten 10 Sekunden, um sicherzustellen, dass die Seite vollständig geladen ist
            Log.Information("Warte 15 Sekunden, um sicherzustellen, dass die Seite vollständig geladen ist...");
            Thread.Sleep(10000);

            // Play-Button klicken, um die Wiedergabe zu starten
            Log.Information("Klicke auf den Play-Button, um die Wiedergabe zu starten.");
            VkMetadataFetcher.TryClickPlayButton();

            // Initialisiert den RingBufferRecorder für die Audioaufnahme
            using var ringBufferRecorder = new RingBufferRecorder();

            // Variablen für Track- und Statusverwaltung
            string lastTrackTime = "";
            string artist = "Artist";
            string title = "Title";
            string lastArtist = "";
            string lastTitle = "";
            int unchangedTimeCount = 0;
            int millisecondsSinceStart = 0;
            int currentTrackDuration = 0;
            bool waitingForTrackStart = false;
            bool trackJustStarted = false;
            DateTime trackStartTime = DateTime.UtcNow;

            // Hauptloop: prüft und verarbeitet Metadaten alle 100 ms
            while (true)
            {
                // Metadaten abfragen
                string trackTime = VkMetadataFetcher.GetTrackTime();
                artist = VkMetadataFetcher.GetArtist();
                title = VkMetadataFetcher.GetTitle();
                currentTrackDuration += 100;

                // Loggt Metadaten einmal pro Sekunde
                if (millisecondsSinceStart % 1000 == 0)
                {
                    Log.Information("Metadaten prüfen... Zeit: {TrackTime}, Artist: {Artist}, Titel: {Title}", trackTime, artist, title);
                }

                if (!waitingForTrackStart)
                {
                    // Vor dem Vergleich: alten Zustand merken
                    string prevArtist = lastArtist;
                    string prevTitle = lastTitle;

                    // Trackwechsel erkennen und ggf. Segment exportieren
                    if (artist != lastArtist || title != lastTitle)
                    {
                        millisecondsSinceStart = 0;
                        unchangedTimeCount = 0;

                        var duration = (DateTime.UtcNow - trackStartTime).TotalMilliseconds;
                        Log.Information("Trackdauer (ms): {Duration}", duration);

                        string safeArtist = MakeFilenameSafe(prevArtist);
                        string safeTitle = MakeFilenameSafe(prevTitle);
                        string filename = $"{safeArtist} - {safeTitle}.mp3";
                        string mp3Path = Path.Combine(outputDir, filename);

                        // Prüft, ob MP3 bereits existiert
                        if (!string.IsNullOrEmpty(safeArtist) || !string.IsNullOrEmpty(safeTitle))
                        {
                            if (File.Exists(mp3Path))
                            {
                                Log.Information("MP3 existiert bereits: {Mp3File}. Export wird komplett übersprungen.", mp3Path);
                            }
                            // Exportiert nur, wenn Track lang genug
                            else if (duration >= 15000)
                            {
                                ringBufferRecorder.ExportMarkedSegment(filename);
                                Log.Information("Trackwechsel erkannt: {LastArtist} - {LastTitle} gespeichert als {mp3Path}", prevArtist, prevTitle, mp3Path);
                            }
                            else
                            {
                                Log.Information("Trackwechsel erkannt: {LastArtist} - {LastTitle} war zu kurz, wird nicht gespeichert.", prevArtist, prevTitle);
                            }
                        }

                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                    }

                    // Nach dem Vergleich: aktuellen Zustand übernehmen
                    lastArtist = artist;
                    lastTitle = title;

                    // Trackstart erkennen (einmalig, wenn Zeit auf 0 springt)
                    if (trackTime == "0:00" && lastTrackTime != "0:00")
                    {
                        string safeArtist = MakeFilenameSafe(artist);
                        string safeTitle = MakeFilenameSafe(title);
                        string filename = $"{safeArtist} - {safeTitle}.mp3";
                        string mp3Path = Path.Combine(outputDir, filename);

                        // Prüft, ob MP3 bereits existiert und überspringt ggf. die Aufnahme
                        if (File.Exists(mp3Path))
                        {
                            Log.Information("Trackstart erkannt, aber MP3 existiert bereits: {Mp3File}. Aufnahme wird übersprungen.", mp3Path);
                            waitingForTrackStart = true;
                            continue;
                        }

                        trackJustStarted = true;
                        millisecondsSinceStart = 0;
                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                        Log.Information("Trackstart erkannt → Zähler auf 0.");

                        ringBufferRecorder.SetMetadata(title, artist);
                        ringBufferRecorder.MarkSegmentStart();
                        Log.Information("🎙️ Aufnahme gestartet (in Memory) für: {Artist} - {Title}", artist, title);
                    }
                    else
                    {
                        millisecondsSinceStart += 100;
                        if (trackJustStarted) trackJustStarted = false;
                    }

                    // Trackende erkennen (Zeit bleibt stehen)
                    if (trackTime == lastTrackTime)
                    {
                        unchangedTimeCount++;
                        if (unchangedTimeCount >= 300)
                        {
                            Log.Information("Track zu Ende");
                            waitingForTrackStart = true;

                            var duration = (DateTime.UtcNow - trackStartTime).TotalMilliseconds;
                            Log.Information("Trackdauer (ms): {Duration}", duration);

                            string safeArtist = MakeFilenameSafe(artist);
                            string safeTitle = MakeFilenameSafe(title);
                            string filename = $"{safeArtist} - {safeTitle}.mp3";
                            string mp3Path = Path.Combine(outputDir, filename);

                            // Prüft, ob MP3 bereits existiert
                            if (File.Exists(mp3Path))
                            {
                                Log.Information("MP3 existiert bereits: {Mp3File}. Export wird komplett übersprungen.", mp3Path);
                            }
                            // Exportiert nur, wenn Track lang genug
                            else if (duration >= 15000)
                            {
                                ringBufferRecorder.ExportMarkedSegment(filename);
                                Log.Information("⏹️ Aufnahme gestoppt und MP3-Datei gespeichert: {Filename}", filename);
                            }
                            else
                            {
                                Log.Information("Track war zu kurz, wird nicht gespeichert.");
                            }

                            // wenn seit 30 Sekunden kein aktiver Track erkannt wurde, Browser schließen
                            Log.Information("🔚 Seit 30 Sekunden kein aktiver Track erkannt. Browser wird geschlossen...");
                            VkMetadataFetcher.CloseBrowser();
                            break;

                        }
                    }
                    else
                    {
                        unchangedTimeCount = 0;
                    }



                }
                else
                {
                    // Warten auf nächsten Trackstart (wenn Track zu Ende ist)
                    if (trackTime == "0:00" && lastTrackTime != "0:00")
                    {
                        waitingForTrackStart = false;
                        unchangedTimeCount = 0;
                        millisecondsSinceStart = 0;
                        currentTrackDuration = 0;
                        trackStartTime = DateTime.UtcNow;
                        //Log.Information("Trackstart erkannt, Segmentmarkierung wurde bereits gesetzt.");

                        //Log.Information("Trackstart: Artist = {Artist}, Title = {Title}", artist, title);
                        ringBufferRecorder.SetMetadata(title, artist);
                        ringBufferRecorder.MarkSegmentStart();
                        Log.Information("🎙️ Aufnahme gestartet (in Memory) für: {Artist} - {Title}", artist, title);
                    }
                }

                // Trackzeit für nächste Iteration merken
                lastTrackTime = trackTime;
                Thread.Sleep(100);
            }

        }

        /// <summary>
        /// Ersetzt alle ungültigen Zeichen in Dateinamen durch Unterstriche.
        /// </summary>
        private static string MakeFilenameSafe(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }
            return input;
        }
    }
}

