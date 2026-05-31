using System.Diagnostics;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace XcavateMobileApp.UITests;

[TestFixture]
[NonParallelizable]
public class AndroidUiTests
{
    private const string AppPackage = "com.xcavate.realxmarket";
    private AndroidDriver? driver;
    private string screenshotDirectory = string.Empty;
    private int screenshotIndex;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var appPath = ResolveAppPath(out var errorMessage);
        if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
        {
            Assert.Fail(errorMessage ?? "ANDROID_APP_PATH environment variable must be set to the built APK path. Build the APK and set ANDROID_APP_PATH before running UI tests.");
        }

        var serverUrl = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723/wd/hub";

        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AddAdditionalAppiumOption("automationName", "UIAutomator2");
        options.AddAdditionalAppiumOption("deviceName", Environment.GetEnvironmentVariable("ANDROID_DEVICE_NAME") ?? "Android Emulator");
        options.AddAdditionalAppiumOption("app", appPath);
        options.AddAdditionalAppiumOption("appPackage", AppPackage);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);

        driver = new AndroidDriver(new Uri(serverUrl), options, TimeSpan.FromMinutes(3));
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        screenshotDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDirectory);
    }

    [SetUp]
    public void SetUp()
    {
        RestartApp();
        WaitForAccessibilityId("WelcomeBrowsePropertiesButton", 60);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (driver is null)
        {
            return;
        }

        driver.Quit();
        driver.Dispose();
        driver = null;
    }

    [Test]
    public void BrowsePropertiesNavigatesToMainPage()
    {
        TapAccessibilityId("WelcomeBrowsePropertiesButton");
        WaitForAccessibilityId("MainTopNavMenuButton", 60);
        CaptureScreenshot("browse-properties-main");
    }

    [Test]
    public void CreateAccountOpensUserTypeSelection()
    {
        TapAccessibilityId("WelcomeCreateAccountButton");
        WaitForAccessibilityId("UserTypeSelectionPage", 120);
        CaptureScreenshot("user-type-selection");
    }

    [Test]
    public void EndToEndSmokeFlow()
    {
        WaitForAccessibilityId("WelcomeBrowsePropertiesButton", 60);
        CaptureScreenshot("welcome");

        TapAccessibilityId("WelcomeBrowsePropertiesButton");
        WaitForAccessibilityId("MainTopNavMenuButton", 60);
        CaptureScreenshot("main-page");

        TapAccessibilityId("MainTopNavMenuButton");
        TryWaitForAccessibilityId("SettingsPage", 15);
        CaptureScreenshot("settings-or-menu");
        NavigateBack();

        TapAccessibilityId("MainTopNavMessagingButton");
        CaptureScreenshot("messaging");
        NavigateBack();

        TapAccessibilityId("MainTopNavQrButton");
        CaptureScreenshot("qr");
        NavigateBack();

        RestartApp();
        WaitForAccessibilityId("WelcomeCreateAccountButton", 60);
        CaptureScreenshot("welcome-return");

        TapAccessibilityId("WelcomeCreateAccountButton");
        WaitForAccessibilityId("UserTypeSelectionPage", 120);
        CaptureScreenshot("user-type-selection");

        TapAccessibilityId("UserTypeInvestorCard");
        WaitForAccessibilityId("ModifyUserProfilePopup", 30);
        CaptureScreenshot("user-profile-popup");
    }

    private void RestartApp()
    {
        if (driver is null)
        {
            Assert.Fail("Driver is not initialized.");
        }

        driver.TerminateApp(AppPackage);
        driver.ActivateApp(AppPackage);
    }

    private void NavigateBack()
    {
        if (driver is null)
        {
            Assert.Fail("Driver is not initialized.");
        }

        driver.Navigate().Back();
    }

    private void TapAccessibilityId(string accessibilityId)
    {
        var element = WaitForAccessibilityId(accessibilityId, 30);
        element.Click();
    }

    private AppiumElement WaitForAccessibilityId(string accessibilityId, int timeoutSeconds)
    {
        if (driver is null)
        {
            Assert.Fail("Driver is not initialized.");
        }

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return wait.Until(_ => driver.FindElement(MobileBy.AccessibilityId(accessibilityId)));
    }

    private bool TryWaitForAccessibilityId(string accessibilityId, int timeoutSeconds)
    {
        try
        {
            WaitForAccessibilityId(accessibilityId, timeoutSeconds);
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    private void CaptureScreenshot(string name)
    {
        if (driver is null)
        {
            Assert.Fail("Driver is not initialized.");
        }

        var safeName = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9\\-_]+", "-").Trim('-');
        var fileName = $"{++screenshotIndex:000}-{safeName}.png";
        var path = Path.Combine(screenshotDirectory, fileName);

        var screenshot = driver.GetScreenshot();
        screenshot.SaveAsFile(path);

        TestContext.AddTestAttachment(path);
    }

    private static string? ResolveAppPath(out string? errorMessage)
    {
        errorMessage = null;
        var appPath = Environment.GetEnvironmentVariable("ANDROID_APP_PATH");
        if (!string.IsNullOrWhiteSpace(appPath) && File.Exists(appPath))
        {
            return appPath;
        }

        var discoveredPath = FindBuiltApkPath();
        if (!string.IsNullOrWhiteSpace(discoveredPath))
        {
            Environment.SetEnvironmentVariable("ANDROID_APP_PATH", discoveredPath);
            return discoveredPath;
        }

        var builtApkPath = BuildAndroidApk(out var buildError);
        if (!string.IsNullOrWhiteSpace(builtApkPath))
        {
            Environment.SetEnvironmentVariable("ANDROID_APP_PATH", builtApkPath);
            return builtApkPath;
        }

        errorMessage = buildError ?? "ANDROID_APP_PATH environment variable must be set to the built APK path. Build the APK and set ANDROID_APP_PATH before running UI tests.";
        return null;
    }

    private static string? BuildAndroidApk(out string? errorMessage)
    {
        errorMessage = null;
        var repoRoot = FindRepositoryRoot();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            errorMessage = "Repository root could not be located to build the Android APK.";
            return null;
        }

        var projectPath = Path.Combine(repoRoot, "XcavateMobileApp", "XcavateMobileApp.csproj");
        if (!File.Exists(projectPath))
        {
            errorMessage = $"Android project file not found at {projectPath}.";
            return null;
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("net10.0-android");
        startInfo.ArgumentList.Add("-p:TargetFramework=net10.0-android");
        startInfo.ArgumentList.Add("-p:TargetFrameworks=net10.0-android");
        startInfo.ArgumentList.Add("-p:AndroidPackageFormat=apk");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            errorMessage = "Failed to start the Android APK build.";
            return null;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = outputTask.GetAwaiter().GetResult();
        var errorOutput = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            errorMessage = $"dotnet build failed (exit code {process.ExitCode}).{Environment.NewLine}{output}{Environment.NewLine}{errorOutput}".Trim();
            return null;
        }

        var builtApkPath = FindBuiltApkPath();
        if (string.IsNullOrWhiteSpace(builtApkPath))
        {
            errorMessage = "Android APK was not produced after building.";
            return null;
        }

        return builtApkPath;
    }

    private static string? FindBuiltApkPath()
    {
        var repoRoot = FindRepositoryRoot();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return null;
        }

        var binRoot = Path.Combine(repoRoot, "XcavateMobileApp", "bin");
        if (!Directory.Exists(binRoot))
        {
            return null;
        }

        var signedApk = Directory.EnumerateFiles(binRoot, "*Signed.apk", SearchOption.AllDirectories).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(signedApk))
        {
            return signedApk;
        }

        return Directory.EnumerateFiles(binRoot, "*.apk", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindRepositoryRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XcavateMobileApp.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
