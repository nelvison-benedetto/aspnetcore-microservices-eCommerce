#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base   #image target presa da docker hub
USER app  #utente linux, che utilizzerai x i comandi here
WORKDIR /app  #set la directory di lavoro, /app è un percorso Linux interno al container, 
EXPOSE 9090
EXPOSE 9091


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build  #img che serve x compilare e pubblicare l'applicazione, non è l'img che girerà a runtime
ARG BUILD_CONFIGURATION=Release   #definisci una var disponibile solo durante il build, che puoi passare come argomento al comando docker build
WORKDIR /src  #dir di lavoro temporanea per la fase di build 
COPY ["eCommerce.API/eCommerce.API.csproj", "eCommerce.API/"]  #copia SOLO il file di progetto (cioè eCommerce.API.csproj) dal contesto di build alla directory /src/eCommerce.API/ del container. ricorda che in prj eCommerce.API nel program.cs chiami a sua volte anche e.g. eCommerce.Infrastructure e eCommerce.Core 
RUN dotnet restore "./eCommerce.API/./eCommerce.API.csproj"  #esegue il ripristino delle dipendenze NuGet del progetto
COPY . .  #il primo . è tutto il contenuto del build context sul PC, il secondo . è directory di lavoro corrente del container, quindi copia tutti gli altri file e li metti nel container. copia il contenuto del contesto di build nella directory di lavoro corrente del container
WORKDIR "/src/eCommerce.API"  #cmabi la directory di lavoro, e vai all'interno della cartella del progetto API, cosi i comandi successivi potranno quindi utilizzare eCommerce.API.csproj senza indicare il percorso completo
RUN dotnet build "./eCommerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/build  #buildi. -o /app/build indica la cartella di output


FROM build AS publish   #crea lo stage publish a partire dallo stage build. Quindi eredita i suoi file e gli strumenti, compresi i file copiati e gli strumenti SDK, il vantaggio è che puoi riutilizzare il risultato delle fasi precedenti senza ripartire da un'img pulita 
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./eCommerce.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false  #genera l'output pubblicabile, il risultato viene scritto in /app/publish, 🔥publish è orientato a produrre un output pronto per essere distribuito e avviato.
#/p:UseAppHost=false disabilita la generazione dell'eseguibile nativo specifico della piattaforma (apphost). In questo caso avvierai direttamente la DLL tramite il comando dotnet


FROM base AS final  #riprendi lo stage base, quello che contiene il runtime ASP.NET Core
WORKDIR /app
COPY --from=publish /app/publish .  #copia gli artefatti pubblicati nello stage publish nella directory corrente (here) dello stage finale, cioè /app.
ENV POSTGRES_HOST=localhost  #⚠️magari host.docker.internal, xk non hai un container x postgresql, quindi usi quello sul tuo pc
ENV POSTGRES_PASSWORD=admin
ENV POSTGRES_DATABASE=eCommerceUsers
ENV POSTGRES_USER=postgres
ENV POSTGRES_PORT=5432
#normalmente questi env li metti nel file .env, perche seno attualemente cmnq chi riceve questo container (magari anche sul web) puoi recuperare queste chiavi! cmnq io sto facenco demo quindi ok
ENTRYPOINT ["dotnet", "eCommerce.API.dll"]  #comando principale che viene eseguito quando il container viene avviato. In questo caso, avvia l'applicazione ASP.NET Core facendo 'dotnet eCommerce.API.dll'


#hai fatto Multi-Stage Build, cioè puoi usare un'immagine più grande per compilare il progetto e un'immagine più essenziale per eseguirlo.


