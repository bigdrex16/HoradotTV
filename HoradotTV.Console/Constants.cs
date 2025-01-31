﻿namespace HoradotTV.Console;

internal static class Constants
{
    public const string SOFTWARE_VERSION = "1.4.0";

    public const int QUERY_MIN_LENGTH = 2;

    public static readonly string DEFAULT_DOWNLOAD_LOCATION = KnownFolders.Downloads.Path;

    public const string ChromeDownloadProblemGuide = "https://github.com/yairp03/HoradotTV/wiki/Chrome-download-problem";
    public const string SdarotTVConnectionProblemGuide = "https://github.com/yairp03/HoradotTV/wiki/SdarotTV-connection-problem";
}

internal static class Menus
{
    public const string MODES_MENU = "-- Download Modes --\n" +
                                      "[0] Back to start\n" +
                                      "[1] Download Episode\n" +
                                      "[2] Download Episodes\n" +
                                      "[3] Download Season\n" +
                                      "[4] Download Series";

    public const string FAILED_MENU = "[0] Nothing\n" +
                                      "[1] Try again\n" +
                                      "[2] Export to file";
}

internal enum Modes
{
    None,
    Episode,
    Episodes,
    Season,
    Series
}

internal enum FailedOptions
{
    None,
    TryAgain,
    Export
}