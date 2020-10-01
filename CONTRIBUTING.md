- [Introduction](#introduction)
- [The contribution process](#the-contribution-process)
  * [Preparing pull requests](#preparing-pull-requests)
    1. [Checking the coding style](#checking-the-coding-style)
    1. [Test projects](#test-projects)
    1. [Building the documentation](#building-the-documentation)
  * [Submitting pull requests](#submitting-pull-requests)
- [The code reviewing process (for the maintainers)](#the-code-reviewing-process)
  * [Reviewing pull requests](#reviewing-pull-requests)
- [Admin tasks (for the maintainers)](#admin-tasks)
  * [Releasing a new version](#release-a-new-version)

## Introduction


This documentation is intended for individuals and institutions interested in contributing to Clara DICOM Adapter. Clara DICOM Adapter is an open-source project and, as such, its success relies on its community of contributors willing to keep improving it. Your contribution will be a valued addition to the code base; we simply ask that you read this page and understand our contribution process, whether you are a seasoned open-source contributor or whether you are a first-time contributor.

### Communicate with us

We are happy to talk with you about your needs for Clara DICOM Adapter and your ideas for contributing to the project. One way to do this is to create an issue discussing your thoughts. It might be that a very similar feature is under development or already exists, so an issue is a great starting point.

## The contribution process

_Pull request early_

We encourage you to create pull requests early. It helps us track the contributions under development, whether they are ready to be merged or not. Change your pull request's title to begin with `[WIP]` until it is ready for formal review.

### Preparing pull requests

This section highlights all the necessary preparation steps required before sending a pull request.
To collaborate efficiently, please read through this section and follow them.

* [Checking the coding style](#checking-the-coding-style)
* [Test Projects](#test-projects)
* [Building documentation](#building-the-documentation)

#### Checking the coding style

##### C# Coding Style

We follow the same [coding style](https://github.com/dotnet/runtime/blob/master/docs/coding-guidelines/coding-style.md) as described by [dotnet](https://github.com/dotnet)/[runtime](https://github.com/dotnet/runtime) project.


The general rule we follow is "use Visual Studio defaults".

1. We use [Allman style](http://en.wikipedia.org/wiki/Indent_style#Allman_style) braces, where each brace begins on a new line. A single line statement block can go without braces but the block must be properly indented on its own line and must not be nested in other statement blocks that use braces (See rule 17 for more details). One exception is that a `using` statement is permitted to be nested within another `using` statement by starting on the following line at the same indentation level, even if the nested `using` contains a controlled block.
2. We use four spaces of indentation (no tabs).
3. We use `_camelCase` for internal and private fields and use `readonly` where possible. Prefix internal and private instance fields with `_`, static fields with `s_` and thread static fields with `t_`. When used on static fields, `readonly` should come after `static` (e.g. `static readonly` not `readonly static`).  Public fields should be used sparingly and should use PascalCasing with no prefix when used.
4. We avoid `this.` unless absolutely necessary.
5. We always specify the visibility, even if it's the default (e.g.
   `private string _foo` not `string _foo`). Visibility should be the first modifier (e.g.
   `public abstract` not `abstract public`).
6. Namespace imports should be specified at the top of the file, *outside* of
   `namespace` declarations, and should be sorted alphabetically, with the exception of `System.*` namespaces, which are to be placed on top of all others.
7. Avoid more than one empty line at any time. For example, do not have two
   blank lines between members of a type.
8. Avoid spurious free spaces.
   For example avoid `if (someVar == 0)...`, where the dots mark the spurious free spaces.
   Consider enabling "View White Space (Ctrl+R, Ctrl+W)" or "Edit -> Advanced -> View White Space" if using Visual Studio to aid detection.
9. If a file happens to differ in style from these guidelines (e.g. private members are named `m_member`
   rather than `_member`), the existing style in that file takes precedence.
10. We only use `var` when it's obvious what the variable type is (e.g. `var stream = new FileStream(...)` not `var stream = OpenStandardInput()`).
11. We use language keywords instead of BCL types (e.g. `int, string, float` instead of `Int32, String, Single`, etc) for both type references as well as method calls (e.g. `int.Parse` instead of `Int32.Parse`). See issue [#13976](https://github.com/dotnet/runtime/issues/13976) for examples.
12. We use PascalCasing to name all our constant local variables and fields. The only exception is for interop code where the constant value should exactly match the name and value of the code you are calling via interop.
13. We use ```nameof(...)``` instead of ```"..."``` whenever possible and relevant.
14. Fields should be specified at the top within type declarations.
15. When including non-ASCII characters in the source code use Unicode escape sequences (\uXXXX) instead of literal characters. Literal non-ASCII characters occasionally get garbled by a tool or editor.
16. When using labels (for goto), indent the label one less than the current indentation.
17. When using a single-statement if, we follow these conventions:
    - Never use single-line form (for example: `if (source == null) throw new ArgumentNullException("source");`)
    - Using braces is always accepted, and required if any block of an `if`/`else if`/.../`else` compound statement uses braces or if a single statement body spans multiple lines.
    - Braces may be omitted only if the body of *every* block associated with an `if`/`else if`/.../`else` compound statement is placed on a single line.

An [EditorConfig](https://editorconfig.org "EditorConfig homepage") file (`.editorconfig`) has been provided at the root of the runtime repository, enabling C# auto-formatting conforming to the above guidelines.

We also use the [.NET Codeformatter Tool](https://github.com/dotnet/codeformatter) to ensure the code base maintains a consistent style over time, the tool automatically fixes the code base to conform to the guidelines outlined above.


##### License information

All source code files should start with this paragraph:

```
# Apache License, Version 2.0
# Copyright 2019-2020 NVIDIA Corporation
# 
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# 
#     http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
```

#### Test Projects

Clara DICOM Adapter test projects can be found under `Test/` of each C# Project.

##### Unit Tests

- src/API/Test/Nvidia.Clara.Dicom.API.Test.csproj
- src/Common/Test/Nvidia.Clara.Dicom.Common.Test.csproj
- src/Configuration/Test/Nvidia.Clara.Dicom.Configuration.Test.csproj
- src/Server/Test/Unit/Nvidia.Clara.DicomAdapter.Test.Unit.csproj

##### Integration Tests

Integration test depends on [dcmtk](https://dicom.offis.de/dcmtk.php.en) binaries to test the 
implemented DICOM DIMSE services between Clara DICOM Adapter and external DICOM devices.

- src/Server/Test/Integration/Nvidia.Clara.DicomAdapter.Test.Integration.csproj
- src/Server/Test/IntegrationCrd/Nvidia.Clara.DicomAdapter.Test.IntegrationCrd.csproj


A bash script (`src/run-tests.sh`) is provided to run all tests locally or in a docker
container (`src/run-tests-in-docker.sh`).


Before submitting a pull request, we recommend that all unit tests and integration tests
should pass, by running the following command locally:


```bash
./src/run-tests-in-docker.sh
```

_If it's not tested, it's broken_

All new functionality should be accompanied by an appropriate set of tests.
Clara DICOM Adapter functionality has plenty of unit tests from which you can draw inspiration,
and you can reach out to us if you are unsure of how to proceed with testing.


#### Building the documentation
Clara DICOM Adapter's documentation is located at `docs/` and requires [DocFX](https://dotnet.github.io/docfx/) to build.

Please following the [instructions](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html#2-use-docfx-as-a-command-line-tool) to install Mono and download DocFX command line tool to build the documentation.

```bash
mono [path-to]/docfx.exe docs/docfx.json
```


### Submitting pull requests
All code changes to the `main` branch must be done via [pull requests](https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/proposing-changes-to-your-work-with-pull-requests).
1. Create a new ticket or take a known ticket from [the issue list][issue list].
1. Check if there's already a branch dedicated to the task.
1. If the task has not been taken, [create a new branch in your fork](https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request-from-a-fork)
of the codebase named `[ticket_id]-[task_name]`.
For example, branch name `19-ci-pipeline-setup` corresponds to issue #19.
Ideally, the new branch should be based on the latest `main` branch.
1. Make changes to the branch ([use detailed commit messages if possible](https://chris.beams.io/posts/git-commit/)).
1. Make sure that new tests cover the changes and the changed codebase [passes all tests locally](#test-projects).
1. [Create a new pull request](https://help.github.com/en/desktop/contributing-to-projects/creating-a-pull-request) from the task branch to the `main` branch, with detailed descriptions of the purpose of this pull request.
1. Check [the CI/CD status of the pull request][github ci], make sure all CI/CD tests passed.
1. Wait for reviews; if there are reviews, make point-to-point responses, make further code changes if needed.
1. If there are conflicts between the pull request branch and the `main` branch, pull the changes from the `main` branch and resolve the conflicts locally.
1. Reviewer and contributor may have discussions back and forth until all comments addressed.
1. Wait for the pull request to be merged.

## The code reviewing process


### Reviewing pull requests
All code review comments should be specific, constructive, and actionable.
1. Check [the CI/CD status of the pull request][github ci], make sure all CI/CD tests passed before reviewing (contact the branch owner if needed).
1. Read carefully the descriptions of the pull request and the files changed, write comments if needed.
1. Make in-line comments to specific code segments, [request for changes](https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/about-pull-request-reviews) if needed.
1. Review any further code changes until all comments addressed by the contributors.
1. Merge the pull request to the master branch.
1. Close the corresponding task ticket on [the issue list][issue list].

[github ci]: https://github.com/NVIDIA/clara-dicom-adapter/actions
[issue list]: https://github.com/NVIDIA/clara-dicom-adapter/issues


## Admin tasks

### Release a new version
- Prepare [a release note](https://github.com/NVIDIA/clara-dicom-adapter/releases).
- Checkout a new branch `releases/[version number]` locally from the master branch and push to the codebase.
- Create a tag, for example `git tag -a 0.1a -m "version 0.1a"`.
- Push the tag to the codebase, for example `git push origin 0.1a`.
  This step will trigger package building and testing.
  The resultant assets are automatically uploaded to [NGC Test Repo](https://ngc.nvidia.com/).  
- Obtain QA report.
- Submit NGC production publishing request.
- Publish build to NGC production environment.
- Generate a new release with release notes.