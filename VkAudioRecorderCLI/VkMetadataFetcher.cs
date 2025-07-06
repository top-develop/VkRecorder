using Serilog;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.IO;
using System.Linq;

namespace VkAudioRecorderCLI
{
    internal class VkMetadataFetcher
    {
        private static ChromeDriver? _driver;
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

                    _driver = new ChromeDriver(options);
                    Log.Information("ChromeDriver mit eigenem User-Profil gestartet. UserDir: {UserDir}", userDir);
                }
                catch (Exception ex)
                {
                    Log.Error("Fehler beim Starten von ChromeDriver: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                }
            }
        }

        public static string GetTrackTime()
        {
            // Aktualisierter VK-Selektor für aktuelle Trackzeit
            return GetElementText("span.vkitAudioPlayerPlaybackProgressTime__text--ftMtw");
        }

        public static string GetArtist()
        {
            // Neuer VK-Selektor für Künstler
            return GetElementText("span.vkitgetColorClass__colorTextSecondary--AhvRj");
        }

        public static string GetTitle()
        {
            // Neuer VK-Selektor für Titel
            return GetElementText("span.vkitgetColorClass__colorTextPrimary--AX4Wt");
        }

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
    }
}
