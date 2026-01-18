# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["AdminApp/AdminApp.csproj", "AdminApp/"]
COPY ["AvnDataGenie/AvnDataGenie.csproj", "AvnDataGenie/"]
COPY ["QueryGenerator/QueryGenerator.csproj", "QueryGenerator/"]
COPY ["ServiceDefaults/ServiceDefaults.csproj", "ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "AdminApp/AdminApp.csproj"

# Copy source files
COPY . .

# Build the application
WORKDIR "/src/AdminApp"
RUN dotnet build "AdminApp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "AdminApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdminApp.dll"]
