SWE40006 Deployment Portfolio Task 1

This repository contains the implementation for Tasks 1.1 through 1.4 of the
SWE40006 Software Deployment and Evolution deployment portfolio.

Projects

HelloWorld
  Task 1.1 sample console application.

HelloWorldInstaller
  WiX v4 MSI for the sample application.

HashGuard.Desktop
  Task 1.2 custom C# WinForms checksum utility.

HashGuard.Tests
  Automated tests for checksums, persistence, and CSV export.

HashGuardInstaller
  Task 1.3 WiX v4 MSI that explicitly packages the application and the
  Newtonsoft.Json, CsvHelper, and Humanizer third-party runtime DLLs.

Solution files

SWE40006.App.sln contains the three C# projects.
SWE40006.Installers.sln contains the two WiX installer projects.
SWE40006.Task1.sln contains all five projects for command-line builds.

Build requirements

Visual Studio with .NET desktop development tools
.NET 10 SDK
WiX Toolset 4.0.6

Build commands

dotnet test src/HashGuard.Tests/HashGuard.Tests.csproj -c Release
dotnet publish src/HashGuard.Desktop/HashGuard.Desktop.csproj -c Release -r win-x64 --self-contained false
dotnet build installer/HashGuardInstaller/HashGuardInstaller.wixproj -c Release

HashGuard Desktop uses a per-user MSI installation and deploys under
LocalAppData\Programs\HashGuard Desktop.

Licence

MIT License. See LICENSE.
