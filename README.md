# Aurora Recorder

Aplicación de escritorio minimalista para Windows diseñada para capturar audio desde múltiples fuentes, transcribir en español chileno y generar minutas automáticas.

## Características principales

- Grabación simultánea de cualquier entrada disponible en Windows (micrófonos, loopback/sistema, capturas virtuales).
- Mezcla inteligente con control por canal (fader, solo y mute) y exportación directa a MP3 en la carpeta configurada.
- Transcripción automática con Whisper (o endpoint compatible) optimizada para español latino.
- Generación de minutas personalizables mediante prompts avanzados usando OpenAI o Gemini.
- Guardado automático de transcripción y resumen como `.txt` local y opcionalmente sincronización con Google Drive.
- Atajos globales configurables para iniciar/detener la grabación aun cuando la app está en segundo plano.

## Estructura del proyecto

```
AudioSummarizerApp.sln
└── src/AudioSummarizerApp
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml / MainWindow.xaml.cs
    ├── ViewModels/
    ├── Services/
    ├── Models/
    └── Themes/
```

## Configuración inicial

1. **Clonar el repositorio y restaurar dependencias**
   ```bash
   dotnet restore
   ```
2. **Compilar la solución (requiere Windows con .NET 8 SDK)**
   ```bash
   dotnet build AudioSummarizerApp.sln -c Release
   ```
3. **Ejecutar**
   ```bash
   dotnet run --project src/AudioSummarizerApp/AudioSummarizerApp.csproj
   ```

## Integraciones

- **Whisper / OpenAI**: configura las API keys desde la pestaña de configuración. Puedes personalizar modelos y prompts.
- **Gemini**: introduce la API key para alternar el proveedor de resúmenes.
- **Google Drive**: coloca el `client_secret.json` en tu equipo y referencia la ruta. El token OAuth se almacena bajo `%AppData%/AuroraRecorder`.

## Hotkeys

La combinación por defecto es `Ctrl+Alt+R`. Puedes cambiarla desde la configuración; la app intentará registrar el nuevo atajo de inmediato.

## Licencia

Uso interno / demo.
