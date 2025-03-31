FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7128

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SearchDaemon.csproj", "./"]
RUN dotnet restore "SearchDaemon.csproj"
COPY . .
RUN dotnet build "SearchDaemon.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SearchDaemon.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SearchDaemon.dll"]