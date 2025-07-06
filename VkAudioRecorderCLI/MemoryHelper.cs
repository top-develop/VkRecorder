using System;
using Serilog;

public static class MemoryHelper
{
    public static bool IsRamSufficient(out long availableMb)
    {
        availableMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
        return availableMb >= 1024;
    }
}
