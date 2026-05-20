FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY . .

RUN dotnet publish OrderIntegrationService.csproj -c Release -o /app/published

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/published .

ENTRYPOINT ["dotnet", "OrderIntegrationService.dll"]