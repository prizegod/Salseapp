# Step 1: Build Stage (.NET 6 SDK)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY ["ShopSaleAPI.csproj", "./"]
RUN dotnet restore "ShopSaleAPI.csproj"

COPY . .
RUN dotnet publish "ShopSaleAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime Stage (.NET 6 ASP.NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# 🎯 Render-க்காக Port 8080 அமைத்தல்
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ShopSaleAPI.dll"]