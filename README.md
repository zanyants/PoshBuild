# PoshBuild
PoshBuild generates PowerShell help files and display format files as part of the build process for PowerShell snap-ins and binary modules. Information can be gathered from standard attributes (eg, [Cmdlet] and [Parameter]), source code XML documentation comments and sidecar descriptor assemblies.
# History and Status
The original version of PoshBuild (0.1.0) was authored by Ludovic Chabant in 2008 (see http://poshbuild.codeplex.com/). This served as the basis of renewed development in 2015, leading to version 0.2.0. The current focus is on help file generation. Several new features have been added, and the project is under active development. At present we at Zany Ants are dogfooding our work and hope to have a more stable release available soon.
# Contributors
Contributions to the PoshBuild project have been made by the PoshBuild Contributors, jointly:
* For version 0.1.0:
  * Ludovic Chabant, (C) 2008 Ludovic Chabant. Note: The original 0.1.0 code indicates a different copyright - Ludovic has stated that this was an unintended error and that the copyright actually resides with him personally.
* For version 0.2.0
  * Tom Glastonbury, (C) 2015 Zany Ants Ltd.

# License
PoshBuild is released under the [Microsoft Public License (MS-PL)](http://opensource.org/licenses/MS-PL).

# NuGet Package
PoshBuild is distributed as a NuGet package at https://www.nuget.org/packages/PoshBuild

# Using PoshBuild
Add the PoshBuild NuGet package to your PowerShell snap-in or binary module C# (or other CLR language) project and an `{assembly}-Help.xml` file will be generated alongside the project's output assembly. To have PoshBuild pick up XML documentation comments from your source code, make sure that 'XML documentation file' generation is enabled in project settings, Build tab, Output section. Custom tags can be used within XML documentation comments to generate a wide variety of PowerShell documentation elements - see [this wiki page](https://github.com/zanyants/PoshBuild/wiki/XML-Documentation-Comments).

Note that the project is in a phase of active pre-release development, so there may be breaking changes prior to release.
