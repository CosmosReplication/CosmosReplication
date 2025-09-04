FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled
EXPOSE 8080
WORKDIR /app
COPY ./artifacts/CosmosReplication.Host/ ./
ENTRYPOINT ["dotnet", "CosmosReplication.Host.dll"]
