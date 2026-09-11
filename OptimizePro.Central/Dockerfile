# Build a partir da raiz do repositório (o contexto do build precisa enxergar
# OptimizePro.Core, referenciado por OptimizePro.Central) — na Railway, configure
# "Dockerfile Path" = OptimizePro.Central/Dockerfile e mantenha o Root Directory como a raiz
# do repo, não esta pasta.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OptimizePro.Core/OptimizePro.Core.csproj OptimizePro.Core/
COPY OptimizePro.Central/OptimizePro.Central.csproj OptimizePro.Central/
RUN dotnet restore OptimizePro.Central/OptimizePro.Central.csproj

COPY OptimizePro.Core/ OptimizePro.Core/
COPY OptimizePro.Central/ OptimizePro.Central/
RUN dotnet publish OptimizePro.Central/OptimizePro.Central.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "OptimizePro.Central.dll"]
