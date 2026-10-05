FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY LumenusErp/LumenusErp.csproj LumenusErp/
RUN dotnet restore LumenusErp/LumenusErp.csproj
COPY LumenusErp/ LumenusErp/
RUN dotnet publish LumenusErp/LumenusErp.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /out .
# ffmpeg извлекает аудио из загруженных записей созвонов (вкладка «Созвоны»)
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/keys /app/uploads && chown app:app /app/keys /app/uploads
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "LumenusErp.dll"]
