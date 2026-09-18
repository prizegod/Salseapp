# ShopSale Management System - ASP.NET Core 6.0 Web API

## Architecture Overview
- **Framework**: ASP.NET Core 6.0 Web API
- **ORM**: Entity Framework Core 6.0 (SQLite provider)
- **Database**: SQLite (`ShopSale.db`)
- **API Endpoints**:
  - `POST /api/DocScanner/scan` - Scans uploaded receipt images/PDFs and extracts structured text fields (Receipt No, Total Amount, Date, Line items).
  - `GET /api/products` & `POST /api/products` & `PUT /api/products/{id}` & `DELETE /api/products/{id}` - Product management.
  - `GET /api/stores` & `POST /api/stores` - Store locations.
  - `GET /api/sales` & `POST /api/sales` - Sales record entry with automated inventory deduction.
  - `GET /api/reports/summary` & `GET /api/reports/daily-sales` - Analytics & sales reports.

## Running the .NET Web API
```bash
cd ShopSaleAPI
dotnet restore
dotnet build
dotnet run
```
The API will launch and automatically create and seed the SQLite database `ShopSale.db` with default Store (`StoreID: 1, Name: "Main Store", Location: "Headquarters"`) and default inventory items.
Swagger UI is accessible at `http://localhost:5000/swagger`.
