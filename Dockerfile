# Imagen base para tiempo de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7128

# Imagen para compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto y restaurar paquetes antes de copiar todo el código
COPY ["SearchDaemon.csproj", "./"]

# **Solución**: Forzar restauración limpia y evitar rutas de Windows
RUN dotnet nuget locals all --clear && \
    dotnet restore "SearchDaemon.csproj" --no-cache

# Copiar todo el código después de restaurar para optimizar la caché de Docker
COPY . .

# Compilar la aplicación
RUN dotnet build "SearchDaemon.csproj" -c Release -o /app/build

# Publicar la aplicación
FROM build AS publish
RUN dotnet publish "SearchDaemon.csproj" -c Release -o /app/publish --no-restore

# Imagen final para ejecución
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Punto de entrada
ENTRYPOINT ["dotnet", "SearchDaemon.dll"]
