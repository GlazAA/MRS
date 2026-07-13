FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/MRS.Api/MRS.Api.csproj", "src/MRS.Api/"]
COPY ["src/MRS.Application/MRS.Application.csproj", "src/MRS.Application/"]
COPY ["src/MRS.Infrastructure/MRS.Infrastructure.csproj", "src/MRS.Infrastructure/"]
RUN dotnet restore "src/MRS.Api/MRS.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/MRS.Api/MRS.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080

ENTRYPOINT ["dotnet", "MRS.Api.dll"]
