# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
COPY ["scripts/", "/scripts/"]
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Directory.Build.props", "."]
COPY ["src/Demo.WebApi/Demo.WebApi.csproj", "Demo.WebApi/"]
COPY ["src/Demo.Services/Demo.Services.csproj", "Demo.Services/"]
COPY ["src/Demo.Contracts/Demo.Contracts.csproj", "Demo.Contracts/"]
COPY ["src/Namh.Data.DynamicSql/Namh.Data.DynamicSql.csproj", "Namh.Data.DynamicSql/"]
COPY ["src/Demo.Share/Demo.Share.csproj", "Demo.Share/"]
COPY ["src/Demo.Repositories/Demo.Repositories.csproj", "Demo.Repositories/"]
RUN dotnet restore "./Demo.WebApi/Demo.WebApi.csproj"
COPY ["src/", "."]
COPY ["scripts/", "scripts/"]
WORKDIR "/src/Demo.WebApi"
RUN dotnet build "./Demo.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Demo.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /src/scripts /scripts
ENTRYPOINT ["dotnet", "Demo.WebApi.dll"]