FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/IBankingTuition.PaymentOrchestrator/IBankingTuition.PaymentOrchestrator.csproj"
RUN dotnet publish "src/IBankingTuition.PaymentOrchestrator/IBankingTuition.PaymentOrchestrator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IBankingTuition.PaymentOrchestrator.dll"]
