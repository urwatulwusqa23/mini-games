FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Connect4.Engine/Connect4.Engine.csproj Connect4.Engine/
COPY Connect4.Bot/Connect4.Bot.csproj Connect4.Bot/
COPY Connect4.Server/Connect4.Server.csproj Connect4.Server/
RUN dotnet restore Connect4.Server/Connect4.Server.csproj

COPY Connect4.Engine/ Connect4.Engine/
COPY Connect4.Bot/ Connect4.Bot/
COPY Connect4.Server/ Connect4.Server/
RUN dotnet publish Connect4.Server/Connect4.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Connect4.Server.dll"]
