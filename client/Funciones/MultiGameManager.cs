using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using Duska.Screens;

namespace Duska.Core
{
    public class MultiGameManager
    {
        private static MultiGameManager _instance;
        public static MultiGameManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MultiGameManager();
                return _instance;
            }
        }

        private Dictionary<int, GameCardScreen> _ventanasJuego;
        private Dictionary<int, Socket> _socketsPartida;
        private Game _game;
        private string _usuario;
        private ScreenManager _screenManager;
        private GraphicsDevice _graphicsDevice;

        private MultiGameManager()
        {
            _ventanasJuego = new Dictionary<int, GameCardScreen>();
            _socketsPartida = new Dictionary<int, Socket>();
        }

        public void Initialize(Game game, string usuario, ScreenManager screenManager, GraphicsDevice graphicsDevice)
        {
            _game = game;
            _usuario = usuario;
            _screenManager = screenManager;
            _graphicsDevice = graphicsDevice;
            Debug.WriteLine("[MULTI] MultiGameManager inicializado correctamente");
        }

        public void AbrirNuevaVentanaJuego(int partidaId, Socket socketPrincipal)
        {
            try
            {
                Debug.WriteLine($"[MULTI] Abriendo nueva ventana de juego para partida {partidaId}");

                if (_ventanasJuego.ContainsKey(partidaId))
                {
                    Debug.WriteLine($"[MULTI] La partida {partidaId} ya está abierta");
                    return;
                }

                // **CREAR NUEVA PANTALLA DE JUEGO**
                var gameScreen = new GameCardScreen(_game, _usuario);
                gameScreen.SetPartidaId(partidaId);

                // **USAR EL SOCKET EXISTENTE PARA ESTA NUEVA PANTALLA**
                gameScreen.SetExistingSocket(socketPrincipal);

                // **GUARDAR REFERENCIA**
                _ventanasJuego[partidaId] = gameScreen;
                _socketsPartida[partidaId] = socketPrincipal;

                // **CAMBIAR A LA PANTALLA DE JUEGO**
                if (_screenManager != null)
                {
                    _screenManager.LoadScreen(gameScreen, new FadeTransition(_graphicsDevice, Color.Black, 0.5f));
                    Debug.WriteLine($"[MULTI] Cambiando a pantalla de juego para partida {partidaId}");
                }
                else
                {
                    Debug.WriteLine("[ERROR] ScreenManager es null - no se puede cambiar de pantalla");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al abrir ventana de juego: {ex.Message}");
            }
        }

        public void CerrarVentanaJuego(int partidaId)
        {
            try
            {
                if (_ventanasJuego.ContainsKey(partidaId))
                {
                    _ventanasJuego[partidaId]?.Dispose();
                    _ventanasJuego.Remove(partidaId);
                }

                if (_socketsPartida.ContainsKey(partidaId))
                {
                    // No cerrar el socket aquí ya que puede estar siendo usado por otras pantallas
                    _socketsPartida.Remove(partidaId);
                }

                Debug.WriteLine($"[MULTI] Ventana de juego {partidaId} cerrada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cerrar ventana: {ex.Message}");
            }
        }

        public List<int> GetPartidaActivas()
        {
            return new List<int>(_ventanasJuego.Keys);
        }

        public bool TienePartidaActiva(int partidaId)
        {
            return _ventanasJuego.ContainsKey(partidaId);
        }

        public GameCardScreen GetGameScreen(int partidaId)
        {
            if (_ventanasJuego.ContainsKey(partidaId))
            {
                return _ventanasJuego[partidaId];
            }
            return null;
        }
    }
}