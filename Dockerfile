# Step 1: Build Stage (.NET 6 SDK)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Project file-ஐ காப்பி செய்து Restore செய்தல்
COPY ["ShopSaleAPI.csproj", "./"]
RUN dotnet restore "ShopSaleAPI.csproj"

# மீதமுள்ள அனைத்து ஃபைல்களையும் காப்பி செய்து Publish செய்தல்
COPY . .
RUN dotnet publish "ShopSaleAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime Stage (.NET 6 ASP.NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# SQLite Database கோப்பு பயன்பாட்டிற்கு
EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "ShopSaleAPI.dll"]
