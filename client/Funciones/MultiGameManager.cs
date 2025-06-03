using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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

        private Dictionary<int, GameWindowInstance> _ventanasAbiertas;
        private string _usuario;

        private GameWindowManager()
        {
            _ventanasAbiertas = new Dictionary<int, GameWindowInstance>();
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
                Debug.WriteLine($"[WINDOW_MANAGER] Creando nueva ventana para partida {partidaId}");

                if (_ventanasAbiertas.ContainsKey(partidaId))
                {
                    Debug.WriteLine($"[WINDOW_MANAGER] La partida {partidaId} ya tiene ventana abierta");
                    return;
                }

                // **CREAR NUEVA VENTANA DE JUEGO EN HILO SEPARADO**
                Thread windowThread = new Thread(() =>
                {
                    try
                    {
                        Debug.WriteLine($"[WINDOW_THREAD] Iniciando ventana para partida {partidaId}");

                        // Crear nueva instancia del juego
                        var gameInstance = new GameWindowInstance(_usuario, partidaId, socketBase);
                        _ventanasAbiertas[partidaId] = gameInstance;

                        // Ejecutar el juego (esto abrirá una nueva ventana)
                        gameInstance.RunOneFrame();

                        Debug.WriteLine($"[WINDOW_THREAD] Ventana de partida {partidaId} iniciada");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] Error en hilo de ventana: {ex.Message}");
                    }
                });

                windowThread.SetApartmentState(ApartmentState.STA);
                windowThread.Start();

                Debug.WriteLine($"[WINDOW_MANAGER] Hilo de ventana iniciado para partida {partidaId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al crear ventana: {ex.Message}");
            }
        }

        public void CerrarVentanaPartida(int partidaId)
        {
            try
            {
                if (_ventanasAbiertas.ContainsKey(partidaId))
                {
                    _ventanasAbiertas[partidaId].Dispose();
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
            return new List<int>(_ventanasAbiertas.Keys);
        }

        public bool TieneVentanaAbierta(int partidaId)
        {
            return _ventanasAbiertas.ContainsKey(partidaId);
        }
    }

    // **CLASE PARA INSTANCIA DE VENTANA DE JUEGO**
    public class GameWindowInstance : Game
    {
        private GraphicsDeviceManager _graphics;
        private string _usuario;
        private int _partidaId;
        private Socket _socket;
        private Duska.Screens.GameCardScreen _gameScreen;

        public GameWindowInstance(string usuario, int partidaId, Socket socket)
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            _usuario = usuario;
            _partidaId = partidaId;
            _socket = socket;

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

                // Crear y configurar la pantalla de juego
                _gameScreen = new Duska.Screens.GameCardScreen(this, _usuario);
                _gameScreen.SetPartidaId(_partidaId);
                _gameScreen.SetExistingSocket(_socket);

                // Cargar contenido de la pantalla
                _gameScreen.LoadContent();

                Debug.WriteLine($"[GAME_INSTANCE] Contenido cargado para partida {_partidaId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cargar contenido: {ex.Message}");
            }
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                if (_gameScreen != null)
                {
                    _gameScreen.Update(gameTime);
                }
                base.Update(gameTime);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Update: {ex.Message}");
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.DarkBlue);

                if (_gameScreen != null)
                {
                    _gameScreen.Draw(gameTime);
                }

                base.Draw(gameTime);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Draw: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Debug.WriteLine($"[GAME_INSTANCE] Dispose de partida {_partidaId}");

                _gameScreen?.Dispose();

                // Remover de la lista de ventanas activas
                GameWindowManager.Instance.CerrarVentanaPartida(_partidaId);

                base.Dispose(disposing);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Dispose: {ex.Message}");
            }
        }
    }
}