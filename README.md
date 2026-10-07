# NaviWalk

Reproductor de música para Android hecho con **.NET MAUI (.NET 9)**. Reproduce la música del dispositivo
o se conecta a un servidor **Navidrome** (API Subsonic). Diseño oscuro con acento naranja estilo Walkman.

## Cómo ejecutarlo

Requisitos: .NET 9 SDK con la carga de trabajo `android` (`dotnet workload install maui-android`) y un
emulador o teléfono con depuración USB.

```powershell
dotnet build -f net9.0-android            # compilar
dotnet build -f net9.0-android -t:Run     # compilar e instalar en el dispositivo/emulador conectado
```

También se puede abrir `NaviWalk.csproj` en Visual Studio 2022 y ejecutar con F5.

## Flujo de la app

1. **Startup** → intenta restaurar la sesión guardada. Si existe, entra directo a *Inicio* sin pedir login.
2. **Bienvenida** (primer uso o tras cerrar sesión) → *Música del dispositivo* o *Servidor Navidrome*.
3. **Login** → dirección (IP/dominio, con o sin `http://`), usuario y contraseña. Se valida con `ping`
   y recién entonces se guardan las credenciales.
4. **Inicio / Buscar / Biblioteca** + mini reproductor persistente → **Reproduciendo** a pantalla completa.

## Estructura del código

```
Models/            Track, Album, Artist, Playlist... (neutrales respecto a la fuente)
Services/
  IMusicSource.cs          Contrato que cumple cada origen de música
  SubsonicClient.cs        HTTP + autenticación por token (md5(pass+salt)) de la API Subsonic
  NavidromeMusicSource.cs  JSON de Navidrome -> modelos
  LocalMusicSource.cs      Archivos del dispositivo -> modelos (agrupa álbumes/artistas en memoria)
  SessionService.cs        SessionStore (SecureStorage) + fuente activa + login/logout
  PlaybackService.cs       Cola, shuffle, repeat, progreso. Usa IAudioPlayer
  NavigationService.cs     Rutas de Shell centralizadas
ViewModels/        Lógica de cada pantalla (CommunityToolkit.Mvvm)
Pages/             Pantallas (XAML + code-behind mínimo)
Views/             Controles reutilizables: CoverView, IconButton, AlbumCard, BottomChrome
Helpers/           Icons (SVG paths), converters, ServiceHelper
Platforms/Android/ MediaPlayer, servicio en primer plano, MediaStore, manifiesto
Resources/Styles/  Colors.xaml (paleta) y Styles.xaml
```

Para **agregar otra fuente** (ej. Jellyfin) basta con implementar `IMusicSource` y usarla en `SessionService`;
la UI no cambia.

## Seguridad de las credenciales

- Usuario, contraseña y URL se guardan con `SecureStorage` (Android Keystore) como un único JSON cifrado.
- La contraseña nunca viaja en claro: cada petición usa `t = md5(contraseña + salt)` y `s = salt`.
- `allowBackup=false` en el manifiesto evita restaurar datos cifrados sin su clave.
- El manifiesto tiene `usesCleartextTraffic=true` para servidores `http://` en la red local.
  Si solo usarás `https://`, cámbialo a `false`.

## Personalizar el diseño

- Colores: `Resources/Styles/Colors.xaml` (`Accent` es el naranja).
- Tipografías/tamaños/botones: `Resources/Styles/Styles.xaml`.
- Íconos: constantes en `Helpers/Icons.cs` (paths SVG de Material Icons).
- Ícono de la app y splash: `Resources/AppIcon/*.svg`, `Resources/Splash/splash.svg`.
- Antes de publicar en Google Play cambia `ApplicationId` en `NaviWalk.csproj`.

## Limitaciones conocidas / ideas siguientes

- La notificación de reproducción no tiene botones (pausa/siguiente) ni integración con `MediaSession`
  (controles de pantalla de bloqueo, auriculares Bluetooth). Es la mejora más importante pendiente.
- Sin caché offline ni descargas.
- Sin persistencia de la cola entre sesiones.
- En modo local no hay "reproducidos recientemente" ni "favoritos" (no hay historial); esas filas se ocultan.
