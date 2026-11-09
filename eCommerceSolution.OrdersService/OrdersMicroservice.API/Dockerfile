
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base  #questp è l'app che runnera nel/nei container
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build  #questo ti serve solo x creare la build 
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OrdersMicroservice.API/OrdersMicroservice.API.csproj", "OrdersMicroservice.API/"]
COPY ["BusinessLogicLayer/BusinessLogicLayer.csproj", "BusinessLogicLayer/"]
COPY ["DataAccessLayer/DataAccessLayer.csproj", "DataAccessLayer/"]
RUN dotnet restore "./OrdersMicroservice.API/OrdersMicroservice.API.csproj"
COPY . .
WORKDIR "/src/OrdersMicroservice.API"
RUN dotnet build "./OrdersMicroservice.API.csproj" -c $BUILD_CONFIGURATION -o /app/build


FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OrdersMicroservice.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV MONGODB_HOST=localhost
ENV MONGODB_PORT=27017
ENV MONGODB_DATABASE=OrdersDatabase
ENV UsersMicroserviceName=localhost
ENV UsersMicroservicePort=9090
ENV ProductsMicroserviceName=localhost
ENV ProductsMicroservicePort=8080
ENTRYPOINT ["dotnet", "OrdersMicroservice.API.dll"]

