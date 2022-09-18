﻿namespace SdarotAPI;

public class SdarotDriver
{
    ChromeDriver? webDriver;
    readonly HttpClient httpClient = new();

    public bool IsInitialized => webDriver is not null;

    public SdarotDriver()
    {
    }

    public async Task Initialize(bool headless = true)
    {
        if (IsInitialized)
        {
            throw new DriverAlreadyInitializedException();
        }

        await ChromeDriverHelper.Install();

        var driverService = ChromeDriverService.CreateDefaultService();
        driverService.HideCommandPromptWindow = true;
        ChromeOptions options = new();
        options.AddArgument("user-agent=" + Constants.UserAgent);
        if (headless)
        {
            options.AddArgument("headless");
            // options.AddArgument("--remote-debugging-port=9222"); // Sometimes cause the driver to not load
        }

        webDriver = new ChromeDriver(driverService, options);

        Constants.SdarotUrls.BaseDomain = await SdarotHelper.RetrieveSdarotDomain();
        httpClient.DefaultRequestHeaders.Referrer = new Uri(Constants.SdarotUrls.HomeUrl);

        try
        {
            await NavigateAsync(Constants.SdarotUrls.TestUrl);
        }
        catch (WebDriverException)
        {
            throw new SdarotBlockedException();
        }

        if (webDriver.Title == "Privacy error")
        {
            throw new SdarotBlockedException();
        }
    }

    public async Task<bool> IsLoggedIn()
    {
        await NavigateAsync(Constants.SdarotUrls.HomeUrl);
        var loginPanelButton = await FindElementAsync(By.XPath(Constants.XPathSelectors.MainPageLoginPanelButton));
        return loginPanelButton != null ? loginPanelButton.Text != Constants.LoginMessage : throw new ElementNotFoundException(nameof(loginPanelButton));
    }

    public async Task<bool> Login(string username, string password)
    {
        if (await IsLoggedIn())
            return true;

        var loginPanelButton = await FindElementAsync(By.XPath(Constants.XPathSelectors.MainPageLoginPanelButton));
        if (loginPanelButton is null)
            throw new ElementNotFoundException(nameof(loginPanelButton));
        loginPanelButton.Click();
        var usernameInput = await FindElementAsync(By.XPath(Constants.XPathSelectors.MainPageFormUsername));
        if (usernameInput is null)
            throw new ElementNotFoundException(nameof(usernameInput));
        var passwordInput = await FindElementAsync(By.XPath(Constants.XPathSelectors.MainPageFormPassword));
        if (passwordInput is null)
            throw new ElementNotFoundException(nameof(passwordInput));
        usernameInput.SendKeys(username);
        passwordInput.SendKeys(password);
        var loginButton = await FindElementAsync(By.XPath(Constants.XPathSelectors.MainPageLoginButton));
        if (loginButton is null)
            throw new ElementNotFoundException(nameof(loginButton));
        await Task.Delay(1000);
        loginButton.Click();

        return await IsLoggedIn();
    }

    async Task NavigateAsync(string url) => await Task.Run(() => webDriver!.Navigate().GoToUrl(url));

    async Task NavigateToSeriesAsync(SeriesInformation series) => await NavigateAsync(series.SeriesUrl);

    async Task NavigateToEpisodeAsync(EpisodeInformation episode) => await NavigateAsync(episode.EpisodeUrl);

    async Task<IWebElement?> FindElementAsync(By by, int timeout = 2)
    {
        return await Task.Run(() =>
        {
            try
            {
                return new WebDriverWait(webDriver, TimeSpan.FromSeconds(timeout)).Until(ExpectedConditions.ElementIsVisible(by));
            }
            catch
            {
                return null;
            }
        });
    }

    static async Task<IWebElement> FindElementAsync(By by, ISearchContext context) => await Task.Run(() => context.FindElement(by));

    async Task<IWebElement?> FindClickableElementAsync(By by, int timeout = 2)
    {
        return await Task.Run(() =>
        {
            try
            {
                return new WebDriverWait(webDriver, TimeSpan.FromSeconds(timeout)).Until(ExpectedConditions.ElementToBeClickable(by));
            }
            catch
            {
                return null;
            }
        });
    }

    async Task<ReadOnlyCollection<IWebElement>?> FindElementsAsync(By by, int timeout = 2)
    {
        return await Task.Run(() =>
        {
            try
            {
                return new WebDriverWait(webDriver, TimeSpan.FromSeconds(timeout)).Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(by));
            }
            catch
            {
                return null;
            }
        });
    }

    CookieContainer RetrieveCookies()
    {
        CookieContainer cookies = new();
        foreach (var cookie in webDriver!.Manage().Cookies.AllCookies)
        {
            cookies.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
        }

        return cookies;
    }

    public void Shutdown()
    {
        if (IsInitialized)
        {
            webDriver?.Quit();
        }
    }

    public async Task<IEnumerable<SeriesInformation>> SearchSeries(string searchQuery)
    {
        var searchHtml = await httpClient.GetStringAsync($"{Constants.SdarotUrls.SearchUrl}{searchQuery}");
        var doc = new HtmlDocument();
        doc.LoadHtml(searchHtml);

        var seriesNameElement = doc.DocumentNode.SelectSingleNode(Constants.XPathSelectors.SeriesPageSeriesName);

        // In case there is only one result
        if (seriesNameElement is not null)
        {
            var seriesName = seriesNameElement.InnerText.Trim(new char[] { ' ', '/' });

            var imageUrlElement = doc.DocumentNode.SelectSingleNode(Constants.XPathSelectors.SeriesPageSeriesImage);
            if (imageUrlElement is null)
                throw new ElementNotFoundException(nameof(imageUrlElement));
            var imageUrl = imageUrlElement.GetAttributeValue("src", "");

            return new SeriesInformation[] { new(seriesName, imageUrl) };
        }

        var seriesElements = doc.DocumentNode.SelectNodes(Constants.XPathSelectors.SearchPageResult);

        // In case there are no results
        if (seriesElements is null || seriesElements.Count == 0)
            return Enumerable.Empty<SeriesInformation>();

        // In case there are more than one result

        var seriesList = new List<SeriesInformation>();
        foreach (var seriesElement in seriesElements)
        {
            var seriesNameHe = seriesElement.SelectSingleNode(Constants.XPathSelectors.SearchPageResultInnerSeriesNameHe).InnerText;
            var seriesNameEn = seriesElement.SelectSingleNode(Constants.XPathSelectors.SearchPageResultInnerSeriesNameEn).InnerText;
            var imageUrl = seriesElement.SelectSingleNode("img").GetAttributeValue("src", "");
            seriesList.Add(new(seriesNameHe, seriesNameEn, imageUrl));
        }

        return seriesList;
    }

    public async Task<IEnumerable<SeasonInformation>> GetSeasonsAsync(SeriesInformation series)
    {
        if (!IsInitialized)
        {
            throw new DriverNotInitializedException();
        }

        await NavigateToSeriesAsync(series);

        var seasonElements = await FindElementsAsync(By.XPath(Constants.XPathSelectors.SeriesPageSeason));

        if (seasonElements is null || seasonElements.Count == 0)
            return Enumerable.Empty<SeasonInformation>();

        List<SeasonInformation> seasons = new();
        for (var i = 0; i < seasonElements.Count; i++)
        {
            var element = seasonElements[i];
            var seasonNumber = int.Parse(element.GetAttribute("data-season"));
            var seasonName = (await FindElementAsync(By.TagName("a"), element)).Text;
            seasons.Add(new(seasonNumber, i, seasonName, series));
        }

        return seasons;
    }

    public async Task<IEnumerable<EpisodeInformation>> GetEpisodesAsync(SeasonInformation season)
    {
        var episodesHtml = await httpClient.GetStringAsync($"{Constants.SdarotUrls.AjaxUrl}?episodeList={season.Series.SeriesCode}&season={season.SeasonNumber}");
        var doc = new HtmlDocument();
        doc.LoadHtml(episodesHtml);

        var episodeElements = doc.DocumentNode.SelectNodes(Constants.XPathSelectors.AjaxEpisode);

        if (episodeElements is null || episodeElements.Count == 0)
            return Enumerable.Empty<EpisodeInformation>();

        List<EpisodeInformation> episodes = new();
        for (var i = 0; i < episodeElements.Count; i++)
        {
            var element = episodeElements[i];
            var episodeNumber = element.GetAttributeValue("data-episode", 0);
            var episodeName = element.SelectSingleNode("a").InnerText;
            episodes.Add(new(episodeNumber, i, episodeName, season));
        }

        return episodes;
    }

    public async Task<IEnumerable<EpisodeInformation>> GetEpisodesAsync(EpisodeInformation firstEpisode, int maxEpisodeAmount)
    {
        var episodesBuffer = new Queue<EpisodeInformation>((await GetEpisodesAsync(firstEpisode.Season)).ToArray()[firstEpisode.EpisodeIndex..]);
        var seasonBuffer = new Queue<SeasonInformation>((await GetSeasonsAsync(firstEpisode.Season.Series)).ToArray()[(firstEpisode.Season.SeasonIndex + 1)..]);

        List<EpisodeInformation> episodes = new();
        for (var i = 0; i < maxEpisodeAmount; i++)
        {
            if (episodesBuffer.Count == 0)
            {
                if (seasonBuffer.Count == 0)
                {
                    break;
                }

                episodesBuffer = new(await GetEpisodesAsync(seasonBuffer.Dequeue()));
                i--;
                continue;
            }

            episodes.Add(episodesBuffer.Dequeue());
        }

        return episodes;
    }

    public async Task<IEnumerable<EpisodeInformation>> GetEpisodesAsync(SeriesInformation series)
    {
        var seasons = await GetSeasonsAsync(series);

        List<EpisodeInformation> episodes = new();
        foreach (var season in seasons)
        {
            episodes.AddRange(await GetEpisodesAsync(season));
        }

        return episodes;
    }

    public async Task<EpisodeMediaDetails> GetEpisodeMediaDetailsAsync(EpisodeInformation episode, IProgress<float>? progress = null)
    {
        if (!IsInitialized)
        {
            throw new DriverNotInitializedException();
        }

        await NavigateToEpisodeAsync(episode);

        // Wait for button to show up
        var currSeconds = (float)Constants.WaitTime;
        while (currSeconds > 0)
        {
            var secondsLeft = await FindElementAsync(By.XPath(Constants.XPathSelectors.SeriesPageEpisodeWaitTime));
            if (secondsLeft is null)
                throw new ElementNotFoundException(nameof(secondsLeft));
            var newSeconds = float.Parse(secondsLeft.Text);
            if (newSeconds != currSeconds)
            {
                currSeconds = newSeconds;
                progress?.Report(30 - currSeconds);
            }
        }

        try
        {
            // Click button
            var proceedButton = await FindClickableElementAsync(By.Id(Constants.IdSelectors.ProceedButtonId));
            if (proceedButton is null)
                throw new ElementNotFoundException(nameof(proceedButton));
            proceedButton.Click();
        }
        catch
        {
            var errorMessage = await FindElementAsync(By.XPath(Constants.XPathSelectors.SeriesPageErrorMessage));
            if (errorMessage is null)
                throw new ElementNotFoundException(nameof(errorMessage));

            if (errorMessage.Text == Constants.Error2Message)
            {
                throw new Error2Exception();
            }

            throw new WebsiteErrorException();
        }

        var episodeMedia = await FindElementAsync(By.Id(Constants.IdSelectors.EpisodeMedia));
        if (episodeMedia is null)
            throw new ElementNotFoundException(nameof(episodeMedia));
        var mediaUrl = episodeMedia.GetAttribute("src");
        var cookies = RetrieveCookies();

        return new EpisodeMediaDetails(mediaUrl, cookies, episode);
    }
}