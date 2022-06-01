﻿namespace SdarotAPI.Model;

public class SeasonInformation
{
    public int SeasonNumber { get; set; }
    public int SeasonIndex { get; set; }
    public string SeasonName { get; set; }
    public SeriesInformation Series { get; set; }

    public string SeasonUrl => $"{Series.SeriesUrl}/season/{SeasonNumber}";

    public SeasonInformation(int seasonNumber, int seasonIndex, string seasonName, SeriesInformation series)
    {
        SeasonNumber = seasonNumber;
        SeasonIndex = seasonIndex;
        SeasonName = seasonName;
        Series = series;
    }
}