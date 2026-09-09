FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gloria.Incentive.slnx ./
COPY src/Gloria.Incentive.Api/Gloria.Incentive.Api.csproj src/Gloria.Incentive.Api/
COPY tests/Gloria.Incentive.Tests/Gloria.Incentive.Tests.csproj tests/Gloria.Incentive.Tests/
RUN dotnet restore src/Gloria.Incentive.Api/Gloria.Incentive.Api.csproj

COPY . .
RUN dotnet publish src/Gloria.Incentive.Api/Gloria.Incentive.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Default="Data Source=/app/db/incentive.db"
ENV DataDirectory=/app/data

RUN mkdir -p /app/db /app/data
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gloria.Incentive.Api.dll"]
