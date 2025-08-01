﻿using Serilog;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.IO;
using System.Linq;

namespace VkAudioRecorderCLI
{
    /// <summary>
    /// Stellt Methoden bereit, um Metadaten (Titel, Künstler, Trackzeit) von VK Music über einen Chrome-Browser mit Selenium auszulesen.
    /// Verwaltet die Lebensdauer des ChromeDriver und das User-Profil für persistente Logins.
    /// </summary>
    internal class VkMetadataFetcher
    {
        /// <summary>
        /// Singleton-Instanz des ChromeDriver für die Browserautomatisierung.
        /// </summary>
        private static ChromeDriver? _driver;

        /// <summary>
        /// Synchronisationsobjekt für Thread-Sicherheit.
        /// </summary>
        private static readonly object _lock = new();

        public static void StartBrowser()
        {
            lock (_lock)
            {
                if (_driver != null)
                {
                    Log.Information("ChromeDriver ist bereits initialisiert.");
                    return;
                }

                try
                {
                    // Fester Ordner für das User-Profil, damit der VK-Login erhalten bleibt
                    var userDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chrome-profile");
                    Directory.CreateDirectory(userDir);

                    var options = new ChromeOptions();
                    options.AddArgument($"--user-data-dir={userDir}");
                    options.AddArgument("--no-first-run");
                    options.AddArgument("--disable-popup-blocking");
                    options.AddArgument("--disable-extensions");
                    options.AddArgument("--remote-debugging-port=9222");
                    options.AddArgument("--disable-dev-shm-usage");
                    options.AddArgument("--no-sandbox");
                    // options.AddArgument("--headless=new"); // entfernt, damit GUI sichtbar bleibt


                    try
                    {
                        _driver = new ChromeDriver(options);
                        Log.Information("ChromeDriver mit eigenem User-Profil gestartet. UserDir: {UserDir}", userDir);
                    }
                    catch (WebDriverException ex)
                    {
                        Log.Warning("Standard-ChromeDriver fehlgeschlagen: {Message}", ex.Message);
                        Log.Information("Versuche lokalen chromedriver.exe aus 'chromedriverlocal' zu verwenden...");

                        string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromedriverlocal");
                        string exePath = Path.Combine(driverPath, "chromedriver.exe");

                        if (!File.Exists(exePath))
                        {
                            Log.Error("Lokaler chromedriver.exe nicht gefunden! Erwartet unter: {Path}", exePath);
                            throw new FileNotFoundException("Lokaler chromedriver.exe nicht gefunden.", exePath);
                        }

                        var service = ChromeDriverService.CreateDefaultService(driverPath, "chromedriver.exe");
                        _driver = new ChromeDriver(service, options);
                        Log.Information("Lokaler ChromeDriver verwendet. Pfad: {DriverPath}", driverPath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Fehler beim Starten von ChromeDriver: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                }
            }
        }


        /// <summary>
        /// Navigiert den Browser zur angegebenen URL.
        /// </summary>
        /// <param name="url">Die Ziel-URL, zu der navigiert werden soll.</param>
        public static void NavigateToMusicPage(string url)
        {
            lock (_lock)
            {
                if (_driver == null)
                {
                    Log.Warning("ChromeDriver ist nicht initialisiert.");
                    return;
                }

                try
                {
                    _driver.Navigate().GoToUrl(url);
                    Log.Information("Navigiere zu URL: {Url}", url);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Navigieren zu {Url}", url);
                }
            }
        }

        /// Klickt auf den Play-Button im VK-Player, um die Wiedergabe zu starten.
        public static void TryClickPlayButton()
        {
            if (_driver == null)
            {
                Log.Warning("ChromeDriver ist nicht initialisiert.");
                return;
            }

            try
            {
                Thread.Sleep(3000); // kurz warten, damit Seite geladen ist

                var playButton = _driver.FindElement(By.CssSelector("button[data-testid='audio-player-controls-state-button']"));
                playButton.Click();

                Log.Information("▶️ Wiedergabe gestartet (Play-Button wurde geklickt).");
            }
            catch (NoSuchElementException)
            {
                Log.Warning("⚠️ Wiedergabe-Button wurde nicht gefunden.");
            }
            catch (Exception ex)
            {
                Log.Error("❌ Fehler beim Klicken auf den Wiedergabe-Button: {Message}", ex.Message);
            }
        }


        /// <summary>
        /// Liest die aktuelle Trackzeit (z.B. "0:42") aus dem VK-Player aus.
        /// </summary>
        /// <returns>Trackzeit als String oder "unknown" bei Fehler.</returns>
        public static string GetTrackTime()
        {
            // Aktualisierter VK-Selektor für aktuelle Trackzeit
            return GetElementText("span.vkitAudioPlayerPlaybackProgressTime__text--ftMtw");
        }

        /// <summary>
        /// Liest den aktuellen Künstlernamen aus dem VK-Player aus.
        /// </summary>
        /// <returns>Künstlername als String oder "unknown" bei Fehler.</returns>
        public static string GetArtist()
        {
            // Neuer VK-Selektor für Künstler
            return GetElementText("span.vkitgetColorClass__colorTextSecondary--AhvRj");
        }

        /// <summary>
        /// Liest den aktuellen Songtitel aus dem VK-Player aus.
        /// </summary>
        /// <returns>Songtitel als String oder "unknown" bei Fehler.</returns>
        public static string GetTitle()
        {
            // Neuer VK-Selektor für Titel
            return GetElementText("span.vkitgetColorClass__colorTextPrimary--AX4Wt");
        }

        /// <summary>
        /// Hilfsmethode, um den Text eines Elements anhand eines CSS-Selektors auszulesen.
        /// Gibt "unknown" zurück, falls das Element nicht gefunden wird oder ein Fehler auftritt.
        /// </summary>
        /// <param name="cssSelector">CSS-Selektor für das gewünschte Element.</param>
        /// <returns>Textinhalt des Elements oder "unknown".</returns>
        private static string GetElementText(string cssSelector)
        {
            lock (_lock)
            {
                if (_driver == null)
                {
                    Log.Warning("ChromeDriver ist nicht initialisiert.");
                    return "unknown";
                }

                try
                {
                    var element = _driver.FindElements(By.CssSelector(cssSelector)).FirstOrDefault();
                    if (element != null)
                    {
                        //Log.Information("Element gefunden für Selektor {Selector}: {Html}", cssSelector, element.GetAttribute("outerHTML"));
                        return element.Text;
                    }
                    else
                    {
                        Log.Warning("Element nicht gefunden: {Selector}", cssSelector);
                        return "unknown";
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Fehler beim Auslesen von {Selector}", cssSelector);
                    return "unknown";
                }
            }
        }

        // Schließt den Chrome-Browser und gibt den ChromeDriver frei.
        public static void CloseBrowser()
        {
            lock (_lock)
            {
                try
                {
                    _driver?.Quit();
                    _driver = null;
                    Log.Information("ChromeDriver wurde beendet und freigegeben.");
                }
                catch (Exception ex)
                {
                    Log.Error("Fehler beim Schließen des Browsers: {Message}", ex.Message);
                }
            }
        }

    }
}

