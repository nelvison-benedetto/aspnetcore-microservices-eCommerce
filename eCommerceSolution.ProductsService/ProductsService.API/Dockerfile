#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Linux container image: on Windows run it with Docker Desktop (WSL 2 backend, "Linux containers" mode).
# Build context must be the solution folder:
#   docker build -t products-microservice:latest -f ProductsService.API/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Defaults: override them with "docker run -e ..." or docker-compose.
# host.docker.internal = the Windows host machine as seen from inside the container (Docker Desktop)
ENV MYSQL_HOST=host.docker.internal
ENV MYSQL_PORT=3306
ENV MYSQL_DATABASE=ecommerceproductsdatabase
ENV MYSQL_USER=root
ENV MYSQL_PASSWORD=admin
ENV RabbitMQ_HostName=host.docker.internal
ENV RabbitMQ_Port=5672
ENV RabbitMQ_UserName=guest
ENV RabbitMQ_Password=guest
ENV RabbitMQ_Products_Exchange=products.exchange

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ProductsService.API/ProductsService.API.csproj", "ProductsService.API/"]
COPY ["BusinessLogicLayer/BusinessLogicLayer.csproj", "BusinessLogicLayer/"]
COPY ["DataAccessLayer/DataAccessLayer.csproj", "DataAccessLayer/"]
RUN dotnet restore "./ProductsService.API/ProductsService.API.csproj"
COPY . .
WORKDIR "/src/ProductsService.API"
RUN dotnet build "./ProductsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProductsService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductsService.API.dll"]
