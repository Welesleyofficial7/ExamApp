FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
COPY ./publish/ExamApp . 
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExamApp.dll"]