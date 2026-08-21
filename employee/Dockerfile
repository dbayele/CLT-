FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY employee/CltPlusPlus.Employee.csproj employee/
RUN dotnet restore employee/CltPlusPlus.Employee.csproj
COPY employee/ employee/
RUN dotnet publish employee/CltPlusPlus.Employee.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5090
EXPOSE 5090
ENTRYPOINT ["dotnet","CltPlusPlus.Employee.dll"]
