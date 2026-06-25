FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Phantom.sln", "./"]
COPY ["src/Phantom.Core/Phantom.Core.csproj", "src/Phantom.Core/"]
COPY ["src/Phantom.Infrastructure.Abstractions/Phantom.Infrastructure.Abstractions.csproj", "src/Phantom.Infrastructure.Abstractions/"]
COPY ["src/Phantom.CQRS/Phantom.CQRS.csproj", "src/Phantom.CQRS/"]
COPY ["src/Phantom.CQRS.SourceGenerator/Phantom.CQRS.SourceGenerator.csproj", "src/Phantom.CQRS.SourceGenerator/"]
COPY ["src/Phantom.Data/Phantom.Data.csproj", "src/Phantom.Data/"]
COPY ["src/Phantom.Messaging/Phantom.Messaging.csproj", "src/Phantom.Messaging/"]
COPY ["src/Phantom.Messaging.MessagePack/Phantom.Messaging.MessagePack.csproj", "src/Phantom.Messaging.MessagePack/"]
COPY ["src/Phantom.NET/Phantom.NET.csproj", "src/Phantom.NET/"]
RUN dotnet restore

COPY . .
RUN dotnet build Phantom.sln -c Release --no-restore

RUN dotnet test Phantom.sln -c Release --no-build --verbosity minimal

RUN dotnet pack src/Phantom.NET/Phantom.NET.csproj -c Release -o /artifacts --no-build

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /artifacts /artifacts

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Phantom.NET.dll"]
