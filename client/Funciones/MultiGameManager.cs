using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using System.Net.Sockets;

namespace Duska.Core
{
    public class GameWindowManager
    {
        private static GameWindowManager _instance;
        public static GameWindowManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameWindowManager();
                return _instance;
            }
        }

        private Dictionary<int, Process> _ventanasAbiertas;
        private string _usuario;

        private GameWindowManager()
        {
            _ventanasAbiertas = new Dictionary<int, Process>();
        }

        public void Initialize(string usuario)
        {
            _usuario = usuario;
            Debug.WriteLine("[WINDOW_MANAGER] Inicializado para usuario: " + usuario);
        }

        public void AbrirNuevaVentanaPartida(int partidaId, Socket socketBase)
        {
            try
            {
                Debug.WriteLine($"[WINDOW_MANAGER] Intentando crear nueva ventana para partida {partidaId}");

                if (_ventanasAbiertas.ContainsKey(partidaId))
                {
                    Debug.WriteLine($"[WINDOW_MANAGER] La partida {partidaId} ya tiene ventana abierta");
                    return;
                }

                // **OPCIÓN 1: CREAR NUEVA INSTANCIA DEL EJECUTABLE**
                string ejecutableActual = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string directorioEjecutable = System.IO.Path.GetDirectoryName(ejecutableActual);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ejecutableActual,
                    Arguments = $"--partida={partidaId} --usuario={_usuario} --modo=juego",
                    UseShellExecute = true,
                    WorkingDirectory = directorioEjecutable
                };

                Process nuevaVentana = Process.Start(startInfo);

                if (nuevaVentana != null)
                {
                    _ventanasAbiertas[partidaId] = nuevaVentana;
                    Debug.WriteLine($"[WINDOW_MANAGER] Nueva ventana creada para partida {partidaId}");

                    // Monitorear cuando se cierre la ventana
                    nuevaVentana.EnableRaisingEvents = true;
                    nuevaVentana.Exited += (sender, e) =>
                    {
                        Debug.WriteLine($"[WINDOW_MANAGER] Ventana de partida {partidaId} cerrada");
                        _ventanasAbiertas.Remove(partidaId);
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al crear nueva ventana: {ex.Message}");

                // **PLAN DE RESPALDO: USAR THREAD CON NUEVA INSTANCIA DE GAME**
                CrearVentanaEnHiloSeparado(partidaId, socketBase);
            }
        }

        private void CrearVentanaEnHiloSeparado(int partidaId, Socket socketBase)
        {
            Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine($"[WINDOW_THREAD] Creando ventana en hilo separado para partida {partidaId}");

                    // Crear nueva instancia del juego que solo muestre GameCardScreen
                    using (var gameInstance = new GameInstanceForPartida(_usuario, partidaId))
                    {
                        gameInstance.RunOneFrame();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Error en hilo de ventana: {ex.Message}");
                }
            });
        }

        public void CerrarVentanaPartida(int partidaId)
        {
            try
            {
                if (_ventanasAbiertas.ContainsKey(partidaId))
                {
                    var proceso = _ventanasAbiertas[partidaId];
                    if (!proceso.HasExited)
                    {
                        proceso.CloseMainWindow();
                        proceso.WaitForExit(3000); // Esperar 3 segundos
                        if (!proceso.HasExited)
                        {
                            proceso.Kill();
                        }
                    }
                    _ventanasAbiertas.Remove(partidaId);
                    Debug.WriteLine($"[WINDOW_MANAGER] Ventana de partida {partidaId} cerrada");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cerrar ventana: {ex.Message}");
            }
        }

        public List<int> GetPartidasActivas()
        {
            // Limpiar procesos que ya no existen
            var partidasACerrar = new List<int>();
            foreach (var kvp in _ventanasAbiertas)
            {
                if (kvp.Value.HasExited)
                {
                    partidasACerrar.Add(kvp.Key);
                }
            }

            foreach (int partidaId in partidasACerrar)
            {
                _ventanasAbiertas.Remove(partidaId);
            }

            return new List<int>(_ventanasAbiertas.Keys);
        }

        public bool TieneVentanaAbierta(int partidaId)
        {
            return _ventanasAbiertas.ContainsKey(partidaId) &&
                   !_ventanasAbiertas[partidaId].HasExited;
        }
    }

    // **CLASE PARA INSTANCIA SEPARADA DEL JUEGO**
    public class GameInstanceForPartida : Game
    {
        private Microsoft.Xna.Framework.Graphics.GraphicsDeviceManager _graphics;
        private string _usuario;
        private int _partidaId;

        public GameInstanceForPartida(string usuario, int partidaId)
        {
            _graphics = new Microsoft.Xna.Framework.Graphics.GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            _usuario = usuario;
            _partidaId = partidaId;

            // Configurar ventana
            Window.Title = $"Duska - Partida #{partidaId} - {usuario}";
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;

            Debug.WriteLine($"[GAME_INSTANCE] Instancia creada para partida {partidaId}");
        }

        protected override void LoadContent()
        {
            try
            {
                Debug.WriteLine($"[GAME_INSTANCE] Cargando contenido para partida {_partidaId}");

                // Ir directamente a la pantalla de juego
                var screenManager = new MonoGame.Extended.Screens.ScreenManager();
                Components.Add(screenManager);

                var gameScreen = new Duska.Screens.GameCardScreen(this, _usuario);
                gameScreen.SetPartidaId(_partidaId);

                screenManager.LoadScreen(gameScreen);

                Debug.WriteLine($"[GAME_INSTANCE] Contenido cargado para partida {_partidaId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cargar contenido: {ex.Message}");
            }
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkBlue);
            base.Draw(gameTime);
        }
    }
}