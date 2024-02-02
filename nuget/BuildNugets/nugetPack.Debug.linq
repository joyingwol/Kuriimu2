<Query Kind="Program">	
  <Reference>&lt;RuntimeDirectory&gt;\Microsoft.VisualBasic.dll</Reference>	
  <Namespace>Microsoft.VisualBasic</Namespace>	
</Query>	

void Main()	
{	
	/// Kuriimu2 Nuget Packer (Debug)	

	Directory.SetCurrentDirectory(Path.GetDirectoryName(Util.CurrentQueryPath));	

	var version = File.ReadAllText("version.txt", Encoding.ASCII);	

	// Ask for the new version	
	var newVersion = Interaction.InputBox("Enter the new version:", "New Version", version);	

	if (Regex.IsMatch(newVersion, @"\d\.\d\.\d"))	
	{	
		File.WriteAllText("version.txt", newVersion, Encoding.ASCII);	

		// The project files to update with the new version.	
		var libraries = new List<string> {	
			@"..\..\src\lib\Kontract\Kontract.csproj",	
			@"..\..\src\lib\Komponent\Komponent.csproj",	
			@"..\..\src\lib\Kanvas\Kanvas.csproj",	
			@"..\..\src\lib\Kryptography\Kryptography.csproj",	
			@"..\..\src\lib\Kompression\Kompression.csproj",	
			@"..\..\src\lib\Kore\Kore.csproj"	
		};	

		// Set the new version in the project files.	
		foreach (var library in libraries)	
		{	
			var content = File.ReadAllText(library, Encoding.UTF8);	
			content = Regex.Replace(content, @"<PackageVersion>\d.\d.\d</PackageVersion>", "<PackageVersion>"+newVersion+"</PackageVersion>");	
			File.WriteAllText(library, content, Encoding.UTF8);	
		}	

		// Generate the NuGet packages.	
		var batch = Process.Start(@"nugetPack.Debug.bat");	
		batch.WaitForExit();	

		// Restore the project files to v2.0.0.	
		foreach (var library in libraries)	
		{	
			var content = File.ReadAllText(library, Encoding.UTF8);	
			content = Regex.Replace(content, @"<PackageVersion>\d.\d.\d</PackageVersion>", "<PackageVersion>2.0.0</PackageVersion>");	
			File.WriteAllText(library, content, Encoding.UTF8);	
		}	
	}	
} 