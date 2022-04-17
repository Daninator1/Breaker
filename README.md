# Breaker

## Description
Breaker is a library for detecting breaking changes in ASP .NET Core Web APIs.
It can be used either as a C# Source Analyzer or integrated into a GitHub Actions workflow.

## Installation

### NuGet Package
The current stable release of Breaker can be found on [NuGet](https://www.nuget.org/packages/Breaker.Analyzer/).

Alternatively, you can build the source yourself and find the NuGet package in the output directory of `Breaker.NuGet`.
After installation, the solution has to be reloaded for the changes to take effect.

Breaker needs to be added to every project inside of the solution that you want to analyze. Only these projects will be taken into account for the change detection (note the problem listed [here](#additional-information)).

### GitHub Action
The GitHub Action can be installed via the [GitHub Marketplace](https://github.com/marketplace/actions/breaker-analyzer).

Alternatively, you can fork the repository and make changes to `Breaker.Action` and `action.yml`.

## Usage

### NuGet Package
Breaker will, by default, use the current active branch of the repository as the version to compare against. If you want to change this, a `breaker.json` file can be added to the root of the solution.

Example:
```json
{
  "gitRef": "main"
}
```
This will set the version to compare against to the branch `main`.
Any git ref can be used to define specific branches, tags or commits.

Changes to the defined version of will only be updated after a reload of the solution.

After configuration, the analyzer will continuously check for breaking changes the current version of the code would introduce and show warnings inside the IDE.

### GitHub Action
The GitHub Action needs to be supplied with two parameters: `actual` and `expected`. These represent both the current version of the code and the one to compare against.

Example workflow:
```yaml
name: 'Breaker'

on:
  pull_request:
    branches: [master]

jobs:
  analysis:

    runs-on: ubuntu-latest

    steps:
      - name: Checkout expected
        uses: actions/checkout@v2
        with:
          ref: 'master'
          path: 'expected'
          
      - name: Checkout actual
        uses: actions/checkout@v2
        with:
          path: 'actual'

      - name: Run Breaker
        id: breaker
        uses: Daninator1/Breaker@1.0.1
        with:
          actual: /github/workspace/actual
          expected: /github/workspace/expected
```
This workflow will run on every pull request to the `master` branch.
Using the checkout action, both versions needed for the analysis are saved in the `actual` and `expected` directories. These then get passed to the Breaker action which performs the comparison.

Together with branch protection rules set up in the repository, this workflow can prevent breaking change-inducing changes to be merged into the `master` branch.

The action will also generate error messages and show them in the according places of the code when viewing the changed files of the PR.

## Checked changes

- endpoint no longer exists
- base route changed
- http method route changed
- http method no longer exists
- type changed
- type added
- type no longer exists
- attribute arguments changed
- attribute added
- attribute no longer exists

## Additional information

For the NuGet package to work, the version needed for the comparison has to be cloned locally to the `.breaker` directory. This means that the time to initialize the analyzer will depend on the size of the repository. Consequently, this folder should be added to the `.gitignore` file.

Log messages can be found inside the `.breaker/breaker.log` file.

## Known problems

- It is not possible to analyze changes made in external Assemblies/NuGet packages. This is due to the fact that Breaker performs the comparison based on the generated syntax trees of the available source files.

- Some breaking change warnings have unexpected locations where they are shown at in the IDE.

- Error objects (e. g. `ProblemDetails`) are not yet checked for breaking changes.

- When analyzing multiple multiple projects, diagnostics can only be reporting for the currently analyzed project. Due to this limitation it can happen that after opening up or reloading the solution, the analyzer will fail to create all diagnostic warnings until further changes to the code are made. Rebuilding the solution will fix this. A solution for this problem would be greatly appreciated.

- If an action return type gets wrapped into `ActionResult`, the analyzer will falsely report this as a breaking change.

- `[FromService]` injections get incorrectly detected as breaking changes.

- In certain scenarios where there are multiple controllers with the same name and route, once one gets moved to a new project, the analyzer can no longer find the correct one for the comparison.

## Contributions

Any contributions are welcome. If you want to contribute to the project, please open an issue or pull request. The discussion tab can also be used for anything related to the project.