# Plataforma Kids – Fase 1 (registro + login por avatar y PIN)

## Requisitos para desarrollar
.NET SDK 8 y MySQL Server 8.0 (servicio MYSQL80 en ejecución).

## Ejecutar
1. Edita `appsettings.json`: pon la clave de root de MySQL en `Password=`, cambia `Jwt:Key` y la contraseña de `Seed`.
2. `dotnet run` → abre la URL que muestra la consola.
3. Los niños entran por la pantalla principal; docentes por "Entrada para docentes"
   (usuario inicial: el de `Seed`). La base y el grupo "Párvulos" se crean solos.

## Publicar en IIS
1. Instala el .NET 8 Hosting Bundle y reinicia IIS.
2. `dotnet publish -c Release -o C:\inetpub\kids`
3. En el Administrador de IIS crea un sitio apuntando a esa carpeta (Application Pool: "Sin código administrado").
4. Dale permisos de lectura al usuario del pool y acceso a MySQL.
5. Activa HTTPS con un certificado.

## Pendiente (siguientes fases)
Límite de intentos del PIN, EF Migrations, juegos, progreso, comunicados y portal de padres.
