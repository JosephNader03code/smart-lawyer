# سيرفر الـ API للنشر (Render أو أي استضافة بتدعم Docker).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SmartLawyer.slnx ./
COPY src/SmartLawyer.Core/SmartLawyer.Core.csproj src/SmartLawyer.Core/
COPY src/SmartLawyer.Infrastructure/SmartLawyer.Infrastructure.csproj src/SmartLawyer.Infrastructure/
COPY src/SmartLawyer.Api/SmartLawyer.Api.csproj src/SmartLawyer.Api/
RUN dotnet restore src/SmartLawyer.Api/SmartLawyer.Api.csproj
COPY src/ src/
RUN dotnet publish src/SmartLawyer.Api/SmartLawyer.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER app
# Render بيحدد البورت في PORT (الافتراضي 10000).
ENV PORT=10000
EXPOSE 10000
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} exec dotnet SmartLawyer.Api.dll"]
