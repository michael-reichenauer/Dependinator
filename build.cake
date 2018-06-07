#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#tool nuget:?package=Tools.InnoSetup&version=5.5.9
#addin nuget:?package=Cake.VersionReader

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define paths.
var name = "Dependinator";

var solutionPath = $"./{name}.sln";
var outputPath = $"{name}/bin/{configuration}/{name}.exe";
var setupPath = $"{name}Setup.exe";
var uninstallerPath = $"Setup/Sign/Uninstaller.exe";
var signedUninstallerPath = $"Setup/Sign/uninst-5.5.9 (u)-44666f8110.e32";

string signPassword = "";


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories($"./**/obj/{configuration}");
    CleanDirectories($"./**/bin/{configuration}");

    if (FileExists(setupPath))
    { 
        DeleteFile(setupPath);
    }
	
	if (FileExists(signedUninstallerPath))
    { 
        DeleteFile(signedUninstallerPath);
    }
});


Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(solutionPath, new NuGetRestoreSettings {
       Verbosity = NuGetVerbosity.Quiet 
    });
});


Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solutionPath, new MSBuildSettings {
        Configuration = configuration,
        Verbosity = Verbosity.Minimal,   
        ArgumentCustomization = args => args.Append("/nologo") 
        }  );
    }
    else
    {
      // Use XBuild
      XBuild(solutionPath, settings =>
        settings.SetConfiguration(configuration));
    }
});


Task("Build-Setup-File")
    .IsDependentOn("Build")
    .Does(() =>
{
    var version = GetFullVersionNumber(outputPath);
	string isSigning = string.IsNullOrWhiteSpace(signPassword) ? "False" : "True";

	InnoSetup("./Setup/Dependinator.iss", new InnoSetupSettings {
		QuietMode = InnoSetupQuietMode.QuietWithProgress,
		Defines = new Dictionary<string, string> { 
			{"AppVersion", ""},
			{"ProductVersion", version},
			{"IsSigning", isSigning},
		}
    });
	
});


Task("Sign-Uninstaller")
    .IsDependentOn("Clean")
	.IsDependentOn("Prompt-Sign-Password")
    .Does(() =>
{
	CopyFile(uninstallerPath, signedUninstallerPath);
	
	// Sign setup file
	var file = new FilePath(signedUninstallerPath);
    Sign(file, new SignToolSignSettings {
            TimeStampUri = new Uri("http://timestamp.digicert.com"),
            CertPath = @"C:\Users\micha\OneDrive\CodeSigning\SignCert.pfx",
            Password = signPassword
    });	
})
.OnError(exception =>
{
	RunTarget("Clean");
	throw exception;
});;


Task("Build-Setup")
    .IsDependentOn("Clean")
	.IsDependentOn("Prompt-Sign-Password")
	.IsDependentOn("Sign-Uninstaller")
    .IsDependentOn("Build-Setup-File")
    .Does(() =>
{
	// Sign setup file
	var file = new FilePath(setupPath);
    Sign(file, new SignToolSignSettings {
            TimeStampUri = new Uri("http://timestamp.digicert.com"),
            CertPath = @"C:\Users\micha\OneDrive\CodeSigning\SignCert.pfx",
            Password = signPassword
    });
	
	var version = GetFullVersionNumber(outputPath);
    Version v = Version.Parse(version);
    string shortVersion = string.Format("{0}.{1}", v.Major, v.Minor);

    Information("v{0}", version); 
    Information("Version {0} alpha", shortVersion); 

    Information("\n\n");  
})
.OnError(exception =>
{
	RunTarget("Clean");
	throw exception;
});;


Task("Build-Unsigned-Setup")
    .IsDependentOn("Build-Setup-File")
    .Does(() =>
{
	Warning("\nSetup file is not signed !!!");
	Error("----------------------------\n\n");
});


Task("Prompt-Sign-Password")
    .Does(() =>
{
	if(Environment.UserInteractive)
	{
		Console.WriteLine("Enter password for signing setup file:");
		signPassword = "";
		ConsoleKeyInfo key;
		do
		{
			key = Console.ReadKey(true);
			if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
			{
				signPassword += key.KeyChar;
				Console.Write("*");
			}
			else
			{
				if (key.Key == ConsoleKey.Backspace && signPassword.Length > 0)
				{
					signPassword = signPassword.Substring(0, (signPassword.Length - 1));
					Console.Write("\b \b");
				}
			}
		}
		while (key.Key != ConsoleKey.Enter);
        Information(" ");

		if (string.IsNullOrWhiteSpace(signPassword))
		{
			throw new Exception("Invalid sign password");
		}
	}
});


Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3($"./**/bin/{configuration}/*Test.dll", new NUnit3Settings {
        NoResults = true,
        NoHeader = true,
        });
});


Task("Default")
    .IsDependentOn("Run-Unit-Tests");



//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
