#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Majorsilence.CrystalCmd.CoreServer/Majorsilence.CrystalCmd.CoreServer.csproj", "Majorsilence.CrystalCmd.CoreServer/"]
RUN dotnet restore "Majorsilence.CrystalCmd.CoreServer/Majorsilence.CrystalCmd.CoreServer.csproj"
COPY . .
WORKDIR "/src/Majorsilence.CrystalCmd.CoreServer"
RUN dotnet build "Majorsilence.CrystalCmd.CoreServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Majorsilence.CrystalCmd.CoreServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Majorsilence.CrystalCmd.CoreServer.dll"]