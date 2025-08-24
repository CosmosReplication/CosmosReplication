FROM mcr.microsoft.com/dotnet/aspnet:8.0-chiseled

WORKDIR /app

COPY ./artifacts/CosmosReplication.Host/ ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "CosmosReplication.Host.dll"]