using System;
using Serilog;

/// <summary>
/// Stellt Hilfsmethoden zur Überprüfung des verfügbaren Arbeitsspeichers bereit.
/// </summary>
public static class MemoryHelper
{
    /// <summary>
    /// Prüft, ob ausreichend RAM im System verfügbar ist (mindestens 1024 MB).
    /// Nutzt .NET GC-Informationen, um den aktuell verfügbaren Speicher zu bestimmen.
    /// </summary>
    /// <param name="availableMb">Gibt die aktuell verfügbaren Megabyte RAM zurück.</param>
    /// <returns>True, wenn mindestens 1024 MB verfügbar sind, sonst false.</returns>
    public static bool IsRamSufficient(out long availableMb)
    {
        // Ermittelt den verfügbaren Speicher in MB
        availableMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        return availableMb >= 1024;
    }
}

