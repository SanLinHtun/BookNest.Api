FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BookNest.Api/BookNest.Api.csproj BookNest.Api/
RUN dotnet restore BookNest.Api/BookNest.Api.csproj
COPY BookNest.Api/ BookNest.Api/
RUN dotnet publish BookNest.Api/BookNest.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "BookNest.Api.dll"]
