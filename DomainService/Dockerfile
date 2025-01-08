FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
COPY ./publish/DomainService . 
EXPOSE 7014
ENTRYPOINT ["dotnet", "DomainService.dll"]