FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY LumenusErp/LumenusErp.csproj LumenusErp/
RUN dotnet restore LumenusErp/LumenusErp.csproj
COPY LumenusErp/ LumenusErp/
RUN dotnet publish LumenusErp/LumenusErp.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/keys && chown app:app /app/keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "LumenusErp.dll"]
