# Set the base image as the .NET 5.0 SDK (this includes the runtime)
FROM mcr.microsoft.com/dotnet/sdk:6.0 as build-env

# Copy everything and publish the release (publish implicitly restores and builds)
COPY . ./
RUN dotnet publish ./Breaker.Action/Breaker.Action.csproj -c Release -o out --no-self-contained

# Label the container
LABEL maintainer="Daniel Zauner <daninator.zauner@gmail.com>"
LABEL repository="repo-url"
LABEL homepage="homepage-url"

# Label as GitHub action
LABEL com.github.actions.name="Breaker Action"
# Limit to 160 characters
LABEL com.github.actions.description="Checks an ASP .NET Core Web API for breaking changes and reports an error for each one found."
# See branding:
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="activity"
LABEL com.github.actions.color="orange"

# Relayer the .NET SDK, anew with the build output
FROM mcr.microsoft.com/dotnet/sdk:6.0
COPY --from=build-env /out .
ENTRYPOINT [ "dotnet", "/Breaker.Action.dll" ]