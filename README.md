# SlnLauncher

<img src="https://git.immo-electronics.ch/giona.imperatori/slnlauncher/-/raw/master/Icon/SlnLauncher.png" width="100">

*...from developers for developers...*

The SlnLauncher is a tool that allows to dyncamically and automatically create VisualStudio solution files (.sln).

As input, it takes a SlnX file that contains all the required information.

The SlnLauncher takes care of finding the C# projects, downloading the NuGet packages of your projects while ensuring package versions consistency and automatically formatting .csproj for a seamless integration.

The SlnLauncher allows developers to forget once for all issues related to VisualStudio like:

* Referenced assembly not found
* Project not found
* Multiple versions of the same NuGet package
* Debug of the own NuGet packages

It doesn't matter anymore where a repository is locally checked out, the SlnLauncher will find what's needed and plug all the components together for you.

Addtionally, the "[Package Debug Feature](#PackageDebugFeature)" allows to seamlessly debug your own NuGet packages from source code by simply adding one element in the SlnX file.
This great feature can be exploited to reduce the burdens of a multi-repo code base, leaving to the developers only the benefits of it!

# Definitions

| Name | Defintion |
| ---- | --------- |
| Test project | Every project with the name ending with ".Test.csproj", is treated as a Test project. |

<br>

# How it works?

The SlnX file feeded to the SlnLauncher contains all the necessary information to create a working VisualStudio solution file:

* Where to look for projects
* Where to store NuGet packages
* The required C# projects
* The required NuGet packages

The application searches all the specified projects&packages, evaluates all assembly and nuget references/dependencies.
With these information, it [generated a set of environment variables](#GenereatedEnvVars) that are used to "connect" projects and references before opening the generate VS Studio solution file.

## Projects

You can work with VisualStudio as usual. Adding new projects, adding new references among them etc. You simply have to add the new projects in the SlnX file, or ensure their name match the defined wildcards.

By adding a new project reference to a project, VisualStudio enters it with a relative path:

``` XML
<ProjectReference Include="..\..\My.First.Project.csproj" />
```
<br>
The SlnLauncher automatically rewrites it before launching VisualStudio in the following format:

``` XML
<ProjectReference Include="$(My_First_Project)\My.First.Project.csproj" />
```

and sets the variable "My\_First\_Project" accordingly.
<br>
## Assemblies and Packages

A similar approach is used for the referenced assembly files.
This is one of the reasons why with the SlnLauncher [NuGet package are not used as NuGet package](#NuGetNotAsNuGet), but simply as assemblies collection.

## <a name="NuGetNotAsNuGet"></a>NuGet packages, not as NuGet packages

- - -

**NOTE**: By default the tool downloads the NuGet packages dependencies automatically (see parameter *-nd*). In the log files warnings/infos will be generated for the packages not defined in the SlnX file.

- - -

The SlnLauncher finds the NuGet packages you specify, and it installs them. Why? Why not just giving a reference to the NuGet packages in the C# projects?

The reason is quite simple: Visual Studio doesn't do a very good job in managing different NuGet package versions.
By plugging together different packages with different dependencies, it can happen that a version-collision occures and Visual Studio is so kind that it doesn't mention that. Leaving you in not always knowing what you're actually debugging.

To avoid these issues, the SlnLauncher takes over the job, ensuring that only **one** version of a package-content is made avaialable to all the projects.

If a collision occures, tool makes you aware of that and then it is up to you to resolve it :-)

That said, simply reference the necessary DLL(s) coming from a NuGet package (from the *packagesPath* folder) via VisualStudio as for any other assembly.

The SlnLauncher will take over the task of rewriting the path with the necessary environmental variable.

## <a name="PackageDebugFeature"></a>Package Debug Feature

If in your company you have libraries packed and released in NuGet packages, this is feature is for sure for you.
One of the biggest hassle of using NuGet packages in application is their debuggability. This can be somehow solved by packging PDBs in NuGet packages.

But what about refactorings?

A simple change of method name can involve quite some work: fix the library, test it, create the nuget package for it, update (manually!) all piece of code that called that method.
That SlnX launcher helps you here in two simple steps.

### 1 Update your Directory.Build.props file

Manually (once!) add the following element to your Directory.Build.props file.

``` XML
<Import Project="<slnx-directory>\nuget.debug" Condition="Exists('<slnx-directory>\nuget.debug')" />
```

Usually, yoiur Directory.Build.props file will look like this:
<br>
``` XML
<Project>
 <PropertyGroup>
   <AssemblyVersion>1.2.3</AssemblyVersion>
   <Version>1.2.3</Version>
 </PropertyGroup>
 <Import Project="nuget.debug" Condition="Exists('nuget.debug')" />
</Project>
```

### 2 Add a \<debug> tag for the NuGet package

Add the debug element to your SlnX file, for all packages you intendend to debug/refactor.

- - -

**NOTE:** it is highly suggested to write these information in the \<yourApp>.slnx.user file, to avoid accidentals commits :-)

- - -

``` XML
<debug package="MyPackage" slnx="<PathToMyPackageRepo>\MyPackageRepo.slnx" />
```

<span class="colour" style="color:var(--vscode-unotes-wsyText)">Done. Nothing more is required from your side.</span>

With these information, the SlnLauncher will search for the additional projects needed to be added in the VS Solution.

- - -

**NOTE:** The Test projects will not be imported.

- - -

But thre is more to it: the tool will also create a file called <span class="colour" style="color:var(--vscode-unotes-wsyText)">nuget.debug, this file contains all the msbuild properties necessary to properly and **automatically** update the project dependencies in your solution. </span>It works seamlessy for you.

## Known limitations and issues

* Only projects in the Microsoft.NET.Sdk format are supported.
Workaround: Update your project files, you and your team will only profit of it :-)
* Only one nuspec can be generated per SlnX file
Workaround: use multiple SlnX files, one per application. It's anyway the better choice :-)
* Package Debug feature: the debug-SlnX file will not override variables set by the main SlnX file. This can cause troubles in loading finding projects etc.
Workaround:
    * Use unique environment variable names or use them only with static values, for example your company NuGet server, etc.
    * Do not use environment variable in the *searchPath* or similar elements, the chances of collision are big (developers love the Copy&Paste feature :-) )
* Woks best Git, can be troublesome with SVN
If you have an SVN repo with trunk/ branches/ tags/ in the *searchPath*, the tool will find multiple versions of the specified projects. Therefore it will not be able to produce the sln file.
Workaround 1: check out only the branch your currently working on
Workarounf 2: migrate to Git :-)
* <span class="colour" style="color:var(--vscode-unotes-wsyText)">Currently all projects in the solution will be getting a reference to the projects loaded via a debug package, not only the one that are really referencing it.
Workaround: not really needed.
Enhancement:
The tool could be enhanced to create the package.debug more </span>specifically (e.g. \<project>.debug) in the project's folder for all projects referencing a DLL from that package.
Additionally, it could be adding the import statement in the csproject automatically. As a nice result, the [manual Step1](#PackageDebugFeature) will become obsolete!
Who is up to the challange? :-)
* If you want to build on your build server using (for example) dotnet, you need to prepare the command shell with all the necessary environment variable.
No worries, that's why the tool allows you to create a Batch/Python/MsBuild file exactly to do so :-) !
* The tool ensures that the specified NuGet package target framework is compatible with all the dependencies of the package itself.
There seem to be packages (e.g. Newtonsoft.Json) that have invalid dependencies settings:
Newtonsoft.Json 9.0.1 -> .netstandard1.0 depends on Microsoft.CSharp 4.01 -> netstandard1.3
Workaround:
For now, the tool allow dependencies starting with "Microsoft." and "System." to have a non-match in the framework.
The assumption here is that the dependency is probably coming from a nuget package delivered with .Net framework itself, and therefore do not have to be referenced manually in the projects (at least it is the case with Microsoft.CSharp)

Possible limitation?

* Only one framework per NuGet package can specified. This "limitation" is explained best by an example:
The NuGet package Newtonsoft.Json package provides its APIs built for multiple frameworks (e.g. net45 and netstandard1.0)
You have your own library NuGet package built on .NET Standard 2.0 called "CoreLib" that references Newtonsoft-nestandard1.0
You have an application built on .NET Framework 4.5 called "App1" that references CoreLib and Newtonsoft-net45
The SlnLauncher will not be able to uniquely set the reference to the packages and therefore it will raise an exception.

# Command line arguments

If an argument is not passed as command line argument, regardless of its type, the default value is applied.
If an argument is mandatory, but it's not set, an exception will be thrown.
Refer to [NDesk Options](http://www.ndesk.org/doc/ndesk-options/NDesk.Options/OptionSet.html) for more information.

## Argument types

### Boolean

A boolean argument (e.g. "Arg", alias "a") can be set (evaluated as true) in the following ways:

* -Arg, -a
* -Arg+, -a+

It can explicitly set to false in the following way:

* -Arg-, -a-

### String

A string argument (i.e. "Arg", alias "a") requires a value to be specified.
The value can be specified in the following ways:

* -Arg=\<value>, -a=\<value>
* -Arg \<value>, -a value

## Supported arguments

The following table shows all the command line arguments that are supported
by the current luancher version.

| Argument name | Alias | Type | Mandatory | Default | Description |
| ------------- | ----- | ---- | --------- | ------- | ----------- |
| version | v | boolean | No | false | If set, it displays the tool's version and exists. |
| dump | d | boolean | No | false | If set it dumps all project&packages information as well as the environment variables in dump.txt located in the SlnX location. |
| log |  | boolean | No | false | If set, it enables the logging to a file. If the SlnX file location exists, the log file will be create in there. Otherwise the location of the SlnLauncher.exe will be used. |
| quite | q | boolean | No | false | No pop-ups or windows will be shown (e.g. in case of exceptions, while loading NuGet packages, etc.) |
| msbuildModule | msb | boolean | No | false | If set, a MSBuild target file (MsBuildGeneratedProperties.targets) containing all defined environment variables is created in the SlnX file location. |
| pythonModule | py | string | No | null | If set, \<value> is used as relative (to the SlnX file) folder path for the creation of the python module (SetEnvVars.py) containing all defined environment variables. |
| batchModule | b | string | No | null | If set, \<value> is used as relative (to the SlnX file) folder path for the creation of the python module (SetEnvVars.bat) containing all defined environment variables. |
| nuspec | ns | string | No | null | If set, \<value> is used as relative (to the SlnX file) folder path in which all required files for a NuGet package will be copied and generated based on the current solution (projects DLLs/PDBs, .nusepc, dependencies).<br>Afterwards, the following command can be execute to create the NuGet package of the solution: >nuget pack \<value>\\\<SlnxName>.nuspec |
| choco | c | string | No | null | To be implemented.... |
| openSln | o | boolean | No | true | If set, it will open the generated Sln (with the default operating system tool) file before exiting the launcher. |
| nugetDependencies | nd | boolean | No | true | If set, the dependencies of the provided packages will be also automatically downloaded. |
| nugetForceMinVersion | nf | boolean | No | true | If set, the tool will check that all the packages dependencies fullfill the min-version provided in the NuGet package (not allowing newer versions).<br>If not, the version simply has to satisfy the [version range](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning) required by the package. |
|  |  | string | Yes | null | Any "unnamed" argument will be used a file path to the SlnX file to be parsed.<br>The last "unnamed" argument will be used as SlnX file path. All others will be ignored. |

# Environment variables and special keys

The SlnLauncher sets a series of environment variable and special keys based on the SlnX content.

## <a name="GenereatedEnvVars"></a>Generated environment variables

The generated environment variable are used by the tools (and VisualStudio) to locate the different components, include or not certain files etc.

- - -

**NOTE:**
The package id and project name related environment variable keys are always formatted with the following rule:

* Every '.' (dot) is replaced with '\_' (single underscore)

Example: "NuGet.Protocol" will become "NuGet\_Protocol"

- - -

| Key | Value |
| --- | ----- |
| \<package-id> | Set to the directory containing the package assemblies. See [PackageIdFormat](#PackageIdFormat) |
| \<package-id>\_version | *version* of the specified NuGet package. (Currently not really used) |
| \<package-id>\_framework | The *targetFramework* of the specified NuGet package. |
| \<package-id>\_debug | 1: if the NuGet package is markjed to be compiled via source code.<br>0: otherwise |
| \<project-id> | Path to the .csproj file of the C# project. |

## Special keys

These values can be used in some of the elements and attribute values of the SlnX file.
The tool will replace them with the corresponding value.

| Key | Value |
| --- | ----- |
| $(slnx) | Directory path of the specified SlnX file. |

# SlnX Format

``` XML
<?xml version="1.0"?>
<SlnX searchPath="" packagesPath="">
    <nuget>
        <targetConfig></targetConfig>
        <readme>README.md</readme>
        <info>
        </info>
    </nuget>
    <env name=""></env>
    <package id="" version="0.2.1" targetFramework="" source="%NUGET_URL%" />
    <project name="SlnLauncher" container="" />
    <debug package="" slnx="" />
</SlnX>
```

## Element descriptions

The default value is applied in case the specified element or attribute is not specified.
If the default value is set to "*none"*, the element or attribute is considered to be optional.
If the default value is set to "*-"*, the element or attribute are required.

### Slnx

Solution file description.

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| searchPath | - | Directory path in which the specified projects will be located. |
| packagesPath | $(slnx)\pack | Directory path in which the specified packages will be installed.<br>It can be seen as the "cache" of the NuGet packages in your local machine. <br>It can be shared among different libraries and applications (save disk space). |

### nuget

Input information for the nuspec file generation.

| Child node | Default | Description |
| ---------- | ------- | ----------- |
| targetConfig | Release | Folder name in the "bin" directory of the projects.<br>Usually "Debug" or "Release |
| readme | *none* | Path to the ReadMe file to be included in the NuGet package. |
| info | *none* | The child nodes included in the info element will be directly included in the nuspec file.<br>See the [nuspec documentation](https://docs.microsoft.com/en-us/nuget/reference/nuspec) for reference.<br>At least the following fields are requrired by the NuGet packager:<span class="colour" style="color:rgb(0, 0, 255)"></span><br><span class="colour" style="color:rgb(0, 0, 255)"></span>    \<description>\</description><br>    \<authors>\</authors><br>    \<projectUrl>\</projectUrl> |

### env

Value of the environment variable with the name specified in the attribute.

- - -

**NOTE**
For the main SlnX file and all other imported SlnX files (via import or debug), if an environment variable is already set, it will not be overriden by the value defined in the env element within the SlnX file.
On the other hand all variables in the User SlnX (.slnx.user file) file will override any previousely set value.

- - -

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| name | - | Name of the environment variable to be set. |

### <a name="PackageElement"></a>package

Defintion of a NuGet package to be installed.

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| id | - | Name of the NuGet package |
| version | - | Version of the NuGet package (see [Version specification](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning)) |
| targetFramework | *none* | Target .NET framework version of the NuGet package, that the application is going to link to. |
| source | - | The URI or local path in which the NuGet package will be searched. |
| IsDoNetLib | true | The tool will set the \<package-id> environment variable (assemblies path) based on this parameter.<br>If the package attribute IsDotNetLib is set, the path will be se to:<br>"\<package-install-path>\lib\\\<targetFramewok>" if the package is recognised as "implementation assembly" package<br>"\<package-install-path>\ref\\\<targetFramewok>" if the package is recognised as "compile time assembly" package<br>Otherwise to:<br>\<package-install-path>\\\<targetFramework><br><br>**NOTE:**<br>For packages not following the standard .NET NuGet package format, set this field to False and use \<targetFramework> to point to the disered directory. |
| var | *none* | Deprecated. Will be removed in future releases. |

### project

Defintion of a C# project to be added to the solution.

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| name | - | Name of the environment variable to be set.<br>Wild cards (\*) can be used to match multiple projects. |
| container | *none* | This value can be used to define a subdirectory in the VS Solution file, in which the project will be placed.<br>The container can have multiple sub-directories (ex: Libs/Bar/Foo)<br>If not set, all Test projects are automatically placed under a container called "Test". |
| packable | true | If set to false, this project is not being conisdered in the generation of the nuspec file.<br>Useful to avoid to include test projects. |

### debug

Defines the location of the SlnX sources  to be used to debug a NuGet package.

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| package | - | The name of the NuGet package required to be debugged. |
| slnx | - | Path to the SlnX file related to the specified NuGet package. |

### import

Definition of a set of NuGet packages.

| Attribute | Default | Description |
| --------- | ------- | ----------- |
| path | *none* | Path to another SlnX file to be inclued.<br>**NOTE:**<br>Only the defined *env* and *bundle* elements are imported into the main SlnX file.<br>This means that the import elements of an imported file are not evaluated (import is not recursive). |
| bundle | - | The name of the a defined *bundle* to imported.<br>If a package id imported via *bundle* is already known, it will not be overriden. |

### bundle

Definition of a set of NuGet packages.

| Child node | Default | Description |
| ---------- | ------- | ----------- |
| package | *none* | See [package](#PackageElement) |

## Examples

``` XML
<?xml version="1.0"?>
<SlnX searchPath="$(slnx)" packagesPath="C:\Nugetcache">
    <env name="NUGET_URL">https://api.nuget.org/v3/index.json</env>

    <package id="NDesk.Options" version="0.2.1" targetFramework="" source="%NUGET_URL%" />

    <package id="Newtonsoft.Json" version="9.0.1" targetFramework="netstandard1.0" source="%NUGET_URL%" />
    <package id="NuGet.Common" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Configuration" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Frameworks" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Packaging" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Packaging.Core" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Protocol" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Resolver" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />
    <package id="NuGet.Versioning" version="5.8.1" targetFramework="netstandard2.0" source="%NUGET_URL%" />

    <package id="NugetHelper" version="0.0.2" targetFramework="netstandard2.0" source="D:\GitRepositories" />

    <!-- Projects -->
    <project name="SlnLauncher" container="" />
    <project name="Slnx" container="Lib" />
</SlnX>
```