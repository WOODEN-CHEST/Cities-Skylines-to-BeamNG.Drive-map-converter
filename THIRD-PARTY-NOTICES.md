# Third-party notices

CsToBeamNG is licensed under the MIT License (see [LICENSE](LICENSE)). It uses the third-party packages below. Only packages marked as redistributed end up in what the tool ships; the others are used only to build or test it.

Every dependency must be listed here, with its license and purpose (see `agents/conventions.md`, Dependencies). Versions are pinned in `Directory.Packages.props`.

## Packages

| Package | Version | License | Used by | Purpose | Redistributed |
|---|---|---|---|---|---|
| [System.CommandLine](https://github.com/dotnet/command-line-api) | 2.0.15 | MIT | Cli | Command-line parsing | yes |
| [Microsoft.NETFramework.ReferenceAssemblies](https://github.com/Microsoft/dotnet/tree/master/releases/reference-assemblies) (with `.net35`) | 1.0.3 | MIT | Dump (net35), Exporter | .NET Framework 3.5 reference assemblies, so the net35 code compiles on any OS | no (build only) |
| [xunit.v3](https://github.com/xunit/xunit) (with its `xunit.v3.*` packages) | 4.0.3 | Apache-2.0 | test projects | Test framework | no (tests only) |
| [xunit.analyzers](https://github.com/xunit/xunit.analyzers) | 2.3.0 | Apache-2.0 | test projects | Analyzers for test code (dependency of xunit.v3) | no (tests only) |
| [Microsoft.Testing.Platform](https://github.com/microsoft/testfx) (with `.MSBuild`, `Extensions.Telemetry`, `Extensions.TrxReport.Abstractions`) | 2.5.2 | MIT | test projects | Test runner (dependency of xunit.v3) | no (tests only) |
| Microsoft.ApplicationInsights, Microsoft.Bcl.AsyncInterfaces, Microsoft.Win32.Registry, System.Security.AccessControl | various | MIT | test projects | Dependencies of the test platform | no (tests only) |

PNG images, such as the debug renders of `cstobeamng inspect`, are written by the converter's own code (`src/CsToBeamNG.Converter/Imaging/`), which implements the W3C PNG specification and compresses with .NET's built-in `System.IO.Compression`. No package is involved.

## Game assemblies

The exporter compiles against assemblies of the user's own Cities: Skylines installation (`Assembly-CSharp`, `ColossalManaged`, `ICities`, `UnityEngine`). They are not part of this repository and are never copied or redistributed; see [docs/legal.md](docs/legal.md).

## License texts of redistributed packages

### System.CommandLine

```text
The MIT License (MIT)

Copyright © .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
