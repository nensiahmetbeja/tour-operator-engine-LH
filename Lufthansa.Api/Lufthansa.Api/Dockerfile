# --- Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy solution + restore
COPY ./Lufthansa.Api.sln ./
COPY ./Lufthansa.Domain/Lufthansa.Domain.csproj ./Lufthansa.Domain/
COPY ./Lufthansa.Application/Lufthansa.Application.csproj ./Lufthansa.Application/
COPY ./Lufthansa.Infrastructure/Lufthansa.Infrastructure.csproj ./Lufthansa.Infrastructure/
COPY ./Lufthansa.Api/Lufthansa.Api.csproj ./Lufthansa.Api/
RUN dotnet restore ./Lufthansa.Api/Lufthansa.Api.csproj

# copy everything and publish
COPY . .
WORKDIR /src/Lufthansa.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# listen on 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Lufthansa.Api.dll"]
