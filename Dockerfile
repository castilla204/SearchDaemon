# Imagen base para tiempo de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 7128

# Imagen para compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto y restaurar paquetes
COPY ["SearchDaemon.csproj", "./"]

# **Corrección**: Restaurar paquetes en una ubicación accesible dentro del contenedor
RUN dotnet nuget locals all --clear && \
    dotnet restore "SearchDaemon.csproj" --no-cache --packages /root/.nuget/packages

# Copiar todo el código después de restaurar (optimización de caché)
COPY . .

# Compilar la aplicación
RUN dotnet build "SearchDaemon.csproj" -c Release -o /app/build --no-restore

# Publicar la aplicación
FROM build AS publish
RUN dotnet publish "SearchDaemon.csproj" -c Release -o /app/publish --no-restore

# Imagen final para ejecución
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Punto de entrada
ENTRYPOINT ["dotnet", "SearchDaemon.dll"]
